using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Factura606.Core.Models;
using Factura606.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Factura606.Core.Services;

/// <summary>
/// Fuente principal de extracción (sección 2.4/2.5 del documento maestro): una sola
/// llamada a Claude por factura, con la imagen completa, pidiendo un JSON estricto
/// con todos los campos + un reporte de dudas de confianza.
/// </summary>
public class ClaudeOcrService(HttpClient http, IOptions<ClaudeOcrOptions> options, ILogger<ClaudeOcrService> logger)
{
    private readonly ClaudeOcrOptions _opciones = options.Value;

    private const string UrlMensajes = "https://api.anthropic.com/v1/messages";

    public async Task<FacturaOcrResult> ExtraerAsync(byte[] imagenBytes, string mediaType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.ApiKey))
            throw new InvalidOperationException("No hay ApiKey de Anthropic configurada (ClaudeOcr:ApiKey).");

        var prompt = ConstruirPrompt();
        var base64 = Convert.ToBase64String(imagenBytes);

        var cuerpo = new
        {
            model = _opciones.Modelo,
            max_tokens = _opciones.MaxTokens,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new { type = "base64", media_type = mediaType, data = base64 }
                        },
                        new { type = "text", text = prompt }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, UrlMensajes);
        request.Headers.Add("x-api-key", _opciones.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(cuerpo);

        using var response = await http.SendAsync(request, ct);
        var crudo = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Claude OCR falló ({Status}): {Cuerpo}", response.StatusCode, crudo);
            throw new HttpRequestException($"Anthropic API respondió {(int)response.StatusCode}: {crudo}");
        }

        var respuesta = JsonSerializer.Deserialize<AnthropicResponse>(crudo, JsonOpts)
            ?? throw new InvalidOperationException("Respuesta de Anthropic vacía o con formato inesperado.");

        var textoJson = respuesta.Content.FirstOrDefault(c => c.Type == "text")?.Text
            ?? throw new InvalidOperationException("La respuesta de Claude no trajo un bloque de texto.");

        return MapearResultado(LimpiarJson(textoJson));
    }

    private string ConstruirPrompt() => $$"""
        Eres un asistente que extrae datos de facturas fiscales dominicanas (comprobantes
        fiscales electrónicos o físicos) a partir de una foto. Responde ÚNICAMENTE con un
        objeto JSON válido, sin texto adicional, sin backticks ni bloques de markdown.

        Extrae estos campos:
        - "rnc": el RNC del PROVEEDOR (quien vende / emite la factura), NUNCA el RNC del
          comprador. La empresa compradora en estas facturas es Transporte Carretón JH SRL,
          RNC {{_opciones.RncPropio}} — si ves ese RNC en la imagen, es el comprador, no lo
          devuelvas como resultado. Solo dígitos, sin guiones.
        - "ncf": el número de comprobante fiscal (NCF).
        - "fecha": la fecha de EMISIÓN del comprobante (no la de vencimiento del pago ni la
          de vigencia del NCF — algunas facturas traen hasta 3 fechas distintas), en
          formato "YYYY-MM-DD".
        - "itbis": el ITBIS TOTAL de la factura (no la suma de ITBIS por línea si hay varios
          ítems — busca el total de ITBIS, generalmente al pie).
        - "total": el monto total de la factura.
        - "dudas": un arreglo de objetos { "campo": "...", "motivo": "..." } por cada campo
          del que no estés 100% seguro, con un motivo breve (ej. "el último dígito estaba
          borroso"). Arreglo vacío si no hay dudas.

        La foto puede ser un recibo POS térmico (formato compacto, con o sin QR) o una
        factura de servicios profesionales (formato de página completa) — no asumas un
        layout fijo, cubre ambos casos.

        Si un campo no aparece o no se puede leer en la imagen, devuélvelo como null.
        Nunca inventes un valor.
        """;

    private static string LimpiarJson(string texto)
    {
        var t = texto.Trim();
        if (t.StartsWith("```"))
        {
            var primerSalto = t.IndexOf('\n');
            t = primerSalto >= 0 ? t[(primerSalto + 1)..] : t;
            var cierre = t.LastIndexOf("```", StringComparison.Ordinal);
            if (cierre >= 0) t = t[..cierre];
        }
        return t.Trim();
    }

    private static FacturaOcrResult MapearResultado(string json)
    {
        var dto = JsonSerializer.Deserialize<ClaudeFacturaDto>(json, JsonOpts)
            ?? throw new InvalidOperationException("Claude devolvió un JSON que no se pudo interpretar.");

        DateOnly? fecha = null;
        if (!string.IsNullOrWhiteSpace(dto.Fecha) && DateOnly.TryParse(dto.Fecha, out var f))
            fecha = f;

        return new FacturaOcrResult
        {
            Rnc = TextoUtils.SoloDigitos(dto.Rnc),
            Ncf = dto.Ncf,
            Fecha = fecha,
            Itbis = dto.Itbis,
            Total = dto.Total,
            Dudas = dto.Dudas ?? [],
            Fuente = "claude"
        };
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private class ClaudeFacturaDto
    {
        public string? Rnc { get; set; }
        public string? Ncf { get; set; }
        public string? Fecha { get; set; }
        public decimal? Itbis { get; set; }
        public decimal? Total { get; set; }
        public List<DudaCampo>? Dudas { get; set; }
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock> Content { get; set; } = [];
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
