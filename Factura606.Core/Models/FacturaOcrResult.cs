namespace Factura606.Core.Models;

/// <summary>
/// Resultado crudo de una pasada de extracción (QR, Claude OCR, o el merge de ambos)
/// antes de convertirse en una <see cref="FacturaExtraida"/> persistida.
/// Todo es nullable a propósito: un campo que no aparece en la imagen debe llegar
/// como null, nunca inventado.
/// </summary>
public class FacturaOcrResult
{
    public string? Rnc { get; set; }
    public string? Ncf { get; set; }
    public DateOnly? Fecha { get; set; }
    public decimal? Itbis { get; set; }
    public decimal? Total { get; set; }
    public List<DudaCampo> Dudas { get; set; } = [];

    /// <summary>Fuente que produjo este resultado (para depuración / trazabilidad).</summary>
    public string Fuente { get; set; } = "desconocida";
}
