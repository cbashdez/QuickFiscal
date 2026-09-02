using ImageMagick;

namespace Factura606.Core.Services;

/// <summary>
/// Convierte fotos HEIC/HEIF (formato nativo de iPhone) a JPEG, que es lo que
/// ZXing y la API de Claude esperan. Ver sección 2.8 del documento maestro.
/// </summary>
public class ConversorImagenService
{
    private static readonly HashSet<string> ExtensionesHeic = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif"
    };

    public bool EsHeic(string nombreArchivo) => ExtensionesHeic.Contains(Path.GetExtension(nombreArchivo));

    public byte[] ConvertirAJpeg(byte[] heicBytes)
    {
        using var imagen = new MagickImage(heicBytes);
        imagen.Format = MagickFormat.Jpeg;
        imagen.Quality = 90;
        using var salida = new MemoryStream();
        imagen.Write(salida);
        return salida.ToArray();
    }
}
