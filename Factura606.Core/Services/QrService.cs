using System.Drawing;
using Factura606.Core.Models;
using Factura606.Core.Utils;
using ZXing;
using ZXing.Windows.Compatibility;

namespace Factura606.Core.Services;

/// <summary>
/// Lee el QR de un e-CF dominicano cuando la factura lo trae. Cuando está presente,
/// es la fuente más confiable para RNC, NCF, fecha y total (sección 2.4).
///
/// El QR de un e-CF de la DGII apunta a una URL de consulta con esos datos como
/// query string, típicamente con parámetros como RncEmisor, ENCF, FechaEmision y
/// MontoTotal — el formato exacto puede variar entre generadores, así que el parseo
/// es tolerante y solo llena lo que reconoce.
/// </summary>
public class QrService
{
    public FacturaOcrResult? LeerQr(byte[] imagenBytes)
    {
        using var stream = new MemoryStream(imagenBytes);
        using var bitmap = new Bitmap(stream);

        var lector = new BarcodeReader
        {
            AutoRotate = true,
            Options = { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] }
        };

        var resultado = lector.Decode(bitmap);
        if (resultado is null) return null;

        return ParsearContenidoQr(resultado.Text);
    }

    private static FacturaOcrResult? ParsearContenidoQr(string contenido)
    {
        if (!Uri.TryCreate(contenido, UriKind.Absolute, out var uri)) return null;

        var query = ParsearQueryString(uri.Query);

        var rnc = TextoUtils.SoloDigitos(BuscarValor(query, "RncEmisor", "RNCEmisor"));
        var ncf = BuscarValor(query, "ENCF", "NCF");
        var montoTexto = BuscarValor(query, "MontoTotal", "Monto");
        var fechaTexto = BuscarValor(query, "FechaEmision");

        if (rnc is null && ncf is null && montoTexto is null) return null;

        decimal? monto = decimal.TryParse(montoTexto, out var m) ? m : null;
        DateOnly? fecha = DateOnly.TryParse(fechaTexto, out var f) ? f : null;

        return new FacturaOcrResult
        {
            Rnc = rnc,
            Ncf = ncf,
            Fecha = fecha,
            Total = monto,
            Fuente = "qr"
        };
    }

    private static Dictionary<string, string> ParsearQueryString(string query)
    {
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var limpio = query.TrimStart('?');
        if (string.IsNullOrEmpty(limpio)) return resultado;

        foreach (var par in limpio.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split('=', 2);
            var clave = Uri.UnescapeDataString(partes[0]);
            var valor = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : string.Empty;
            resultado[clave] = valor;
        }

        return resultado;
    }

    private static string? BuscarValor(Dictionary<string, string> query, params string[] claves)
    {
        foreach (var clave in claves)
            if (query.TryGetValue(clave, out var valor))
                return valor;
        return null;
    }
}
