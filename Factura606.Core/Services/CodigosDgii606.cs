namespace Factura606.Core.Services;

/// <summary>
/// Listas oficiales de códigos del formato 606, extraídas literalmente de las columnas
/// de ayuda (AD y AF) de la plantilla oficial de la DGII
/// ("Formato-de-Envio-606-(NG-07-2018-y-05-2019).xls", hoja "Herramienta Formato 606",
/// validación de datos de las columnas D y Z). No inventar ni tocar estas listas —
/// si la DGII publica una plantilla nueva con códigos distintos, hay que volver a
/// extraerlos de ahí, no adivinarlos (ver sección 2.7 del documento maestro).
/// </summary>
public static class CodigosDgii606
{
    public static readonly string[] TipoBienesYServiciosComprados =
    [
        "01-GASTOS DE PERSONAL",
        "02-GASTOS POR TRABAJOS, SUMINISTROS Y SERVICIOS",
        "03-ARRENDAMIENTOS",
        "04-GASTOS DE ACTIVOS FIJO",
        "05 -GASTOS DE REPRESENTACIÓN",
        "06 -OTRAS DEDUCCIONES ADMITIDAS",
        "07 -GASTOS FINANCIEROS",
        "08 -GASTOS EXTRAORDINARIOS",
        "09 -COMPRAS Y GASTOS QUE FORMARAN PARTE DEL COSTO DE VENTA",
        "10 -ADQUISICIONES DE ACTIVOS",
        "11- GASTOS DE SEGUROS"
    ];

    public static readonly string[] FormaDePago =
    [
        "01 - EFECTIVO",
        "02 - CHEQUES/TRANSFERENCIAS/DEPÓSITO",
        "03 - TARJETA CRÉDITO/DÉBITO",
        "04 - COMPRA A CREDITO",
        "05 - PERMUTA",
        "06 - NOTA DE CREDITO",
        "07 - MIXTO"
    ];

    /// <summary>Regla de negocio fija (sección 2.6): casi siempre "02".</summary>
    public const string CategoriaPorDefecto = "02-GASTOS POR TRABAJOS, SUMINISTROS Y SERVICIOS";

    /// <summary>Regla de negocio fija (sección 2.6): siempre "02", sin importar el método real de pago.</summary>
    public const string FormaPagoPorDefecto = "02 - CHEQUES/TRANSFERENCIAS/DEPÓSITO";
}
