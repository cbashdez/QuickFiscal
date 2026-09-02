using Factura606.Core.Models;
using Microsoft.Extensions.Logging;

namespace Factura606.Core.Services;

/// <summary>
/// Orquesta el pipeline de extracción de una factura (sección 2.4 y decisión 3.7.4
/// del documento maestro):
///
///   1. QR (si el e-CF lo trae) — rápido y muy confiable para rnc/ncf/fecha/total.
///   2. Claude OCR — fuente principal para TODOS los campos, una sola llamada con
///      la imagen completa. Rellena lo que el QR no trajo (ITBIS) y sirve de
///      única fuente cuando no hay QR.
///
/// Tesseract ya NO es parte del pipeline en la versión web: en el servidor siempre
/// hay API key + internet, así que el respaldo local perdió su razón de ser. Si
/// Claude falla, la factura queda marcada como "pendiente, revisar a mano" en vez
/// de intentar un segundo motor de OCR (ver decisión 3.7.4).
/// </summary>
public class ExtractorFactura(QrService qrService, ClaudeOcrService claudeOcr, ILogger<ExtractorFactura> logger)
{
    public async Task<FacturaExtraida> ProcesarAsync(byte[] imagenBytes, string mediaType, string rutaFoto, CancellationToken ct = default)
    {
        FacturaOcrResult? qr = null;
        try
        {
            qr = qrService.LeerQr(imagenBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer QR de {Ruta} (puede que la factura simplemente no traiga uno)", rutaFoto);
        }

        FacturaOcrResult? claude = null;
        var dudas = new List<DudaCampo>();
        try
        {
            claude = await claudeOcr.ExtraerAsync(imagenBytes, mediaType, ct);
            dudas = claude.Dudas;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude OCR falló para {Ruta} — la factura queda pendiente de revisión manual", rutaFoto);
            dudas.Add(new DudaCampo
            {
                Campo = "*",
                Motivo = "El OCR automático falló (sin conexión, sin API key, o error del servicio). Revisar y completar a mano."
            });
        }

        // QR es la fuente más confiable cuando está presente; Claude rellena el resto.
        var factura = new FacturaExtraida
        {
            RutaFoto = rutaFoto,
            Rnc = qr?.Rnc ?? claude?.Rnc,
            Ncf = qr?.Ncf ?? claude?.Ncf,
            Fecha = qr?.Fecha ?? claude?.Fecha,
            Total = qr?.Total ?? claude?.Total,
            Itbis = claude?.Itbis,
            Dudas = dudas
        };

        factura.MontoFacturado = factura.Total;
        return factura;
    }
}
