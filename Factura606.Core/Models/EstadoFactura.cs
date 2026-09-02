namespace Factura606.Core.Models;

/// <summary>
/// Tri-estado de revisión de una factura. No se persiste: se calcula al vuelo
/// a partir de qué campos faltan o tienen dudas (ver <see cref="FacturaExtraida.Estado"/>).
/// </summary>
public enum EstadoFactura
{
    /// <summary>Completa, sin dudas del OCR.</summary>
    Completa,

    /// <summary>Completa pero con dudas del OCR, o con campos no críticos por revisar.</summary>
    RevisarDudas,

    /// <summary>Faltan campos críticos (RNC, NCF, fecha o total).</summary>
    CamposFaltantes
}
