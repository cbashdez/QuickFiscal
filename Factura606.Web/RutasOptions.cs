namespace Factura606.Web;

/// <summary>
/// Dónde vive cada archivo en ESTE servidor (la PC de casa). Todas las rutas son
/// relativas al content root salvo que ya sean absolutas — ver sección "Rutas" en
/// appsettings.json.
/// </summary>
public class RutasOptions
{
    public const string SeccionConfig = "Rutas";

    public string CarpetaDatos { get; set; } = "App_Data";
    public string CarpetaFotos { get; set; } = "App_Data/fotos";
    public string ArchivoPin { get; set; } = "App_Data/pin.dat";
    public string BaseDeDatos { get; set; } = "App_Data/facturas606.db";
    public string PlantillaDgii { get; set; } = "App_Data/plantilla_606.xls";

    public string Resolver(string contentRootPath, string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(contentRootPath, ruta);
}
