namespace Factura606.Core.Models;

/// <summary>
/// Una duda de confianza reportada por el OCR sobre un campo puntual de la factura
/// (ej. "el último dígito estaba borroso"). Se guarda serializada dentro de
/// <see cref="FacturaExtraida.DudasJson"/>.
/// </summary>
public class DudaCampo
{
    public string Campo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}
