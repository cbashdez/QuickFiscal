using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Factura606.Core.Models;

/// <summary>
/// Una factura ya cargada y (parcial o totalmente) extraída. Es la entidad principal
/// de la app — ver sección 2.3 del documento maestro.
/// </summary>
public class FacturaExtraida
{
    public int Id { get; set; }

    /// <summary>Ruta de la imagen original (en el servidor, no en el cliente).</summary>
    public string RutaFoto { get; set; } = string.Empty;

    /// <summary>Solo dígitos, sin guiones — RNC del proveedor (nunca el de Transporte Carretón como comprador).</summary>
    public string? Rnc { get; set; }

    public string? Ncf { get; set; }

    /// <summary>Fecha de emisión del comprobante — no la de vencimiento ni la de vigencia del NCF.</summary>
    public DateOnly? Fecha { get; set; }

    /// <summary>ITBIS total de la factura, no la suma de ITBIS por línea.</summary>
    public decimal? Itbis { get; set; }

    public decimal? Total { get; set; }

    /// <summary>
    /// Monto que se exporta como "Total Monto Facturado". Se mantiene igual a
    /// <see cref="Total"/> salvo que el usuario lo corrija a mano en la revisión.
    /// </summary>
    public decimal? MontoFacturado { get; set; }

    /// <summary>Código DGII de "Tipo de Bienes y Servicios Comprados" (ver lista oficial de la plantilla).</summary>
    public string? Categoria { get; set; }

    /// <summary>Código DGII de forma de pago. Regla de negocio: siempre "02 Cheques/Transferencias/Depósito".</summary>
    public string? FormaPago { get; set; }

    /// <summary>Cuándo se cargó la foto — útil para ordenar la bandeja de revisión.</summary>
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    /// <summary>JSON serializado de <see cref="Dudas"/>. Columna TEXT en la BD.</summary>
    public string? DudasJson { get; set; }

    [NotMapped]
    public List<DudaCampo> Dudas
    {
        get => string.IsNullOrWhiteSpace(DudasJson)
            ? []
            : JsonSerializer.Deserialize<List<DudaCampo>>(DudasJson!) ?? [];
        set => DudasJson = value.Count == 0 ? null : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Tri-estado calculado (verde/amarillo/rojo) — nunca se guarda en la BD, se
    /// recalcula cada vez a partir de qué campos faltan o tienen dudas.
    /// </summary>
    [NotMapped]
    public EstadoFactura Estado
    {
        get
        {
            var faltaCritico = string.IsNullOrWhiteSpace(Rnc)
                || string.IsNullOrWhiteSpace(Ncf)
                || Fecha is null
                || Total is null
                || Itbis is null;

            if (faltaCritico) return EstadoFactura.CamposFaltantes;

            return Dudas.Count > 0 ? EstadoFactura.RevisarDudas : EstadoFactura.Completa;
        }
    }
}
