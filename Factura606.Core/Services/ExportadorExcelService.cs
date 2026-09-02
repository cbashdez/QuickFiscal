using Factura606.Core.Models;
using Factura606.Core.Utils;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace Factura606.Core.Services;

/// <summary>
/// Exporta las facturas al formato 606 escribiendo sobre una COPIA de la plantilla
/// .xls oficial de la DGII (nunca toca el archivo original), preservando sus macros
/// — igual que en la versión de escritorio (sección 2.7 del documento maestro).
///
/// Verificado contra la plantilla real ("Formato-de-Envio-606-(NG-07-2018-y-05-2019).xls",
/// hoja "Herramienta Formato 606"):
/// - La fila de encabezados de detalle está en la fila 11 (1-indexed) y las columnas
///   coinciden con <see cref="ColumnasEsperadas"/>.
/// - "Fecha Comprobante" y "Fecha Pago" están formateadas como TEXTO ('@'), no como
///   fecha de Excel — el instructivo de la DGII (LlenadoyEnvioFormato606.pdf, sección
///   6-7) pide "seleccione el año y mes (AAAAMM) y digite el día (DD)", o sea que el
///   valor esperado es el texto literal "AAAAMMDD" (8 dígitos, sin separadores).
/// - El encabezado del formulario (fuera de la tabla de detalle) tiene 3 campos que
///   hay que llenar: "RNC o Cédula" (el RNC de quien declara, no el del proveedor),
///   "Periodo" (AAAAMM del mes que se declara) y "Cantidad Registros" (debe coincidir
///   con la cantidad de filas de detalle o la DGII rechaza el archivo).
/// </summary>
public class ExportadorExcelService
{
    // Orden y nombres exactos de columnas, sección 2.7 del documento maestro.
    private static readonly string[] ColumnasEsperadas =
    [
        "Líneas", "RNC o Cédula", "Tipo Id", "Tipo Bienes y Servicios Comprados",
        "NCF", "NCF ó Documento Modificado", "Fecha Comprobante", "Fecha Pago",
        "Monto Facturado en Servicios", "Monto Facturado en Bienes", "Total Monto Facturado",
        "ITBIS Facturado", "ITBIS Retenido", "ITBIS sujeto a Proporcionalidad (Art. 349)",
        "ITBIS llevado al Costo", "ITBIS por Adelantar", "ITBIS percibido en compras",
        "Tipo de Retención en ISR", "Monto Retención Renta", "ISR Percibido en compras",
        "Impuesto Selectivo al Consumo", "Otros Impuesto/Tasas", "Monto Propina Legal",
        "Forma de Pago", "Estatus"
    ];

    public const int TipoIdEmpresa = 1;

    // Etiquetas del encabezado del formulario (fuera de la tabla de detalle) — se
    // busca la celda con este texto en la columna 0 y se escribe 2 columnas a la
    // derecha (así vino en la plantilla real: label en A:B combinado, valor en C).
    private const string EtiquetaRncDeclarante = "RNC o Cédula";
    private const string EtiquetaPeriodo = "Periodo";
    private const string EtiquetaCantidadRegistros = "Cantidad Registros";
    private const int ColumnaValorEncabezado = 2;

    /// <summary>
    /// Genera el archivo de exportación. <paramref name="rutaPlantilla"/> debe apuntar
    /// a la plantilla .xls oficial de la DGII; el resultado se escribe en
    /// <paramref name="rutaSalida"/> sin modificar la plantilla.
    /// </summary>
    /// <param name="rncDeclarante">RNC de quien declara (Transporte Carretón JH SRL), solo dígitos.</param>
    /// <param name="periodoAaaamm">
    /// Mes que se declara en formato AAAAMM (ej. 202606). Si es null, se usa el
    /// año-mes más frecuente entre las fechas de las facturas exportadas.
    /// </param>
    public void Exportar(IEnumerable<FacturaExtraida> facturas, string rutaPlantilla, string rutaSalida,
        string rncDeclarante, string? periodoAaaamm = null)
    {
        if (!File.Exists(rutaPlantilla))
            throw new FileNotFoundException(
                "No se encontró la plantilla oficial .xls de la DGII. Colócala en la ruta configurada " +
                "(ver appsettings: Rutas:PlantillaDgii) — no se puede generar el 606 sin ella.",
                rutaPlantilla);

        var listaFacturas = facturas.OrderBy(f => f.FechaCarga).ToList();

        using var entrada = File.OpenRead(rutaPlantilla);
        var libro = new HSSFWorkbook(entrada);
        var hoja = EncontrarHojaDeDetalle(libro);

        EscribirEncabezadoFormulario(hoja, rncDeclarante, periodoAaaamm ?? DeterminarPeriodo(listaFacturas), listaFacturas.Count);

        var (filaEncabezado, columnas) = EncontrarEncabezados(hoja);

        var filaEscritura = filaEncabezado + 1;
        var linea = 1;

        foreach (var f in listaFacturas)
        {
            var fila = hoja.GetRow(filaEscritura) ?? hoja.CreateRow(filaEscritura);

            EscribirCelda(fila, columnas, "Líneas", linea);
            EscribirCelda(fila, columnas, "RNC o Cédula", TextoUtils.SoloDigitos(f.Rnc));
            EscribirCelda(fila, columnas, "Tipo Id", TipoIdEmpresa);
            EscribirCelda(fila, columnas, "Tipo Bienes y Servicios Comprados", f.Categoria ?? CodigosDgii606.CategoriaPorDefecto);
            EscribirCelda(fila, columnas, "NCF", f.Ncf);

            // Texto literal AAAAMMDD, no un valor de fecha de Excel — ver comentario de clase.
            EscribirCelda(fila, columnas, "Fecha Comprobante", FormatearFechaTexto(f.Fecha));

            // Monto Facturado no se reparte entre Servicios/Bienes: el total de la factura
            // va completo a "Total Monto Facturado" Y TAMBIÉN a "Monto Facturado en
            // Servicios" (mismo valor en ambas columnas — sección 2.6).
            var montoFacturado = f.MontoFacturado ?? f.Total;
            EscribirCelda(fila, columnas, "Monto Facturado en Servicios", montoFacturado);
            EscribirCelda(fila, columnas, "Total Monto Facturado", montoFacturado);

            // ITBIS va en ambas columnas, mismo valor (sección 2.6).
            EscribirCelda(fila, columnas, "ITBIS Facturado", f.Itbis);
            EscribirCelda(fila, columnas, "ITBIS llevado al Costo", f.Itbis);

            EscribirCelda(fila, columnas, "Forma de Pago", f.FormaPago ?? CodigosDgii606.FormaPagoPorDefecto);

            linea++;
            filaEscritura++;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(rutaSalida)!);
        using var salida = File.Create(rutaSalida);
        libro.Write(salida, leaveOpen: false);
    }

    private static string? FormatearFechaTexto(DateOnly? fecha) => fecha?.ToString("yyyyMMdd");

    /// <summary>Año-mes (AAAAMM) más frecuente entre las facturas, como valor por defecto razonable del período declarado.</summary>
    private static string DeterminarPeriodo(List<FacturaExtraida> facturas)
    {
        var conFecha = facturas.Where(f => f.Fecha is not null).Select(f => f.Fecha!.Value).ToList();
        if (conFecha.Count == 0) return DateTime.Now.ToString("yyyyMM");

        return conFecha
            .GroupBy(f => f.ToString("yyyyMM"))
            .OrderByDescending(g => g.Count())
            .First().Key;
    }

    private static void EscribirEncabezadoFormulario(ISheet hoja, string rncDeclarante, string periodoAaaamm, int cantidadRegistros)
    {
        EscribirValorDeEtiqueta(hoja, EtiquetaRncDeclarante, TextoUtils.SoloDigitos(rncDeclarante) ?? rncDeclarante);
        EscribirValorDeEtiqueta(hoja, EtiquetaPeriodo, periodoAaaamm);
        EscribirValorDeEtiqueta(hoja, EtiquetaCantidadRegistros, cantidadRegistros);
    }

    private static void EscribirValorDeEtiqueta(ISheet hoja, string etiqueta, object valor)
    {
        for (var r = 0; r <= Math.Min(hoja.LastRowNum, 10); r++)
        {
            var fila = hoja.GetRow(r);
            var celdaEtiqueta = fila?.GetCell(0);
            if (celdaEtiqueta is null || !TextoLiteralCelda(celdaEtiqueta).Trim().Equals(etiqueta, StringComparison.OrdinalIgnoreCase))
                continue;

            var celdaValor = fila!.GetCell(ColumnaValorEncabezado) ?? fila.CreateCell(ColumnaValorEncabezado);
            switch (valor)
            {
                case string s: celdaValor.SetCellValue(s); break;
                case int i: celdaValor.SetCellValue(i); break;
                default: celdaValor.SetCellValue(valor.ToString()); break;
            }
            return;
        }

        // No se encontró la etiqueta — no es fatal (podría ser una versión de plantilla
        // distinta), pero sin esto la DGII probablemente rechace el archivo igual.
    }

    private static ISheet EncontrarHojaDeDetalle(IWorkbook libro)
    {
        for (var i = 0; i < libro.NumberOfSheets; i++)
        {
            var hoja = libro.GetSheetAt(i);
            if (TieneEncabezadosConocidos(hoja)) return hoja;
        }

        throw new InvalidOperationException(
            "No se encontró en la plantilla una hoja con las columnas de detalle esperadas (NCF, RNC, ITBIS, etc.). " +
            "Verifica que sea la plantilla 606 oficial de la DGII.");
    }

    private static bool TieneEncabezadosConocidos(ISheet hoja)
    {
        for (var r = 0; r <= Math.Min(hoja.LastRowNum, 20); r++)
        {
            var fila = hoja.GetRow(r);
            if (fila is null) continue;
            var textos = fila.Cells.Select(c => TextoLiteralCelda(c).Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (textos.Contains("NCF") && textos.Any(t => t.Contains("RNC", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static (int filaEncabezado, Dictionary<string, int> columnas) EncontrarEncabezados(ISheet hoja)
    {
        for (var r = 0; r <= Math.Min(hoja.LastRowNum, 20); r++)
        {
            var fila = hoja.GetRow(r);
            if (fila is null) continue;

            var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var celda in fila.Cells)
            {
                var texto = TextoLiteralCelda(celda).Trim();
                if (texto.Length > 0) columnas[texto] = celda.ColumnIndex;
            }

            // Basta con encontrar la mayoría de las columnas esperadas para considerar
            // que esta es la fila de encabezados (algunas plantillas varían tildes/mayúsculas).
            var encontradas = ColumnasEsperadas.Count(c => columnas.ContainsKey(c));
            if (encontradas >= ColumnasEsperadas.Length / 2)
                return (r, columnas);
        }

        throw new InvalidOperationException(
            "No se pudo ubicar la fila de encabezados de la plantilla 606. Revisa que las columnas " +
            "coincidan con las de la sección 2.7 del documento maestro.");
    }

    private static void EscribirCelda(IRow fila, Dictionary<string, int> columnas, string nombreColumna, object? valor)
    {
        if (!columnas.TryGetValue(nombreColumna, out var indice)) return; // columna no está en esta plantilla, se omite
        var celda = fila.GetCell(indice) ?? fila.CreateCell(indice);

        switch (valor)
        {
            case null:
                celda.SetCellValue(string.Empty);
                break;
            case string s:
                celda.SetCellValue(s);
                break;
            case int i:
                celda.SetCellValue(i);
                break;
            case decimal dec:
                celda.SetCellValue((double)dec);
                break;
            case DateOnly d:
                celda.SetCellValue(d.ToDateTime(TimeOnly.MinValue));
                break;
            default:
                celda.SetCellValue(valor.ToString());
                break;
        }
    }

    /// <summary>
    /// Texto literal de una celda sin pasar por <see cref="ICell.ToString()"/> — ese método
    /// usa el <c>DataFormatter</c> de NPOI, que en este entorno falla por depender de
    /// SkiaSharp para el formateo numérico. Solo nos interesa leer encabezados de texto,
    /// así que basta con mirar el tipo de celda directamente.
    /// </summary>
    private static string TextoLiteralCelda(ICell celda)
    {
        try
        {
            return celda.CellType switch
            {
                CellType.String => celda.StringCellValue,
                CellType.Numeric => celda.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellType.Formula when celda.CachedFormulaResultType == CellType.String => celda.StringCellValue,
                _ => ""
            };
        }
        catch
        {
            return "";
        }
    }
}
