using Factura606.Core.Data;
using Factura606.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Factura606.Web;

/// <summary>
/// Endpoints de datos/archivos que no encajan como componentes Blazor: servir la
/// foto de una factura (protegido — son documentos privados), exportar a Excel, y
/// descargar el respaldo de la base de datos (sección 2.8 / 3.4 del documento maestro).
/// Todos requieren sesión iniciada.
/// </summary>
public static class ApiEndpoints
{
    private static readonly Dictionary<string, string> TiposMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/fotos/{id:int}", async (int id, AppDbContext db) =>
        {
            var factura = await db.Facturas.FindAsync(id);
            if (factura is null || string.IsNullOrWhiteSpace(factura.RutaFoto) || !File.Exists(factura.RutaFoto))
                return Results.NotFound();

            var ext = Path.GetExtension(factura.RutaFoto);
            var mime = TiposMime.GetValueOrDefault(ext, "application/octet-stream");
            return Results.File(factura.RutaFoto, mime);
        });

        api.MapGet("/exportar", async (FacturaService facturas, ExportadorExcelService exportador,
            IOptions<RutasOptions> rutasOpt, IOptions<ClaudeOcrOptions> ocrOpt, IWebHostEnvironment env) =>
        {
            var rutas = rutasOpt.Value;
            var rutaPlantilla = rutas.Resolver(env.ContentRootPath, rutas.PlantillaDgii);
            var todas = await facturas.ListarAsync();

            var nombreSalida = $"606_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
            var rutaSalida = Path.Combine(Path.GetTempPath(), nombreSalida);

            try
            {
                exportador.Exportar(todas, rutaPlantilla, rutaSalida, ocrOpt.Value.RncPropio);
            }
            catch (FileNotFoundException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var bytes = await File.ReadAllBytesAsync(rutaSalida);
            File.Delete(rutaSalida);
            return Results.File(bytes, "application/vnd.ms-excel", nombreSalida);
        });

        api.MapGet("/respaldo", (IOptions<RutasOptions> rutasOpt, IWebHostEnvironment env) =>
        {
            var rutas = rutasOpt.Value;
            var rutaDb = rutas.Resolver(env.ContentRootPath, rutas.BaseDeDatos);
            if (!File.Exists(rutaDb)) return Results.NotFound();

            var nombre = $"facturas606_respaldo_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            return Results.File(rutaDb, "application/octet-stream", nombre);
        });

        return app;
    }
}
