using System.Security.Cryptography;

namespace Factura606.Core.Services;

/// <summary>
/// Login por PIN — hash con PBKDF2 + sal (agnóstico de plataforma, igual que en la
/// versión de escritorio). En la versión web el archivo <c>pin.dat</c> vive junto a la
/// base de datos del servidor y el bloqueo por intentos fallidos se aplica del lado
/// del servidor (sección 3.2 del documento maestro), no solo en el cliente.
///
/// IMPORTANTE: registrar como singleton en DI — el contador de intentos fallidos y
/// el bloqueo viven en memoria de la instancia; un lifetime scoped/transient
/// resetearía el bloqueo en cada request.
/// </summary>
public class GestorPin(string rutaArchivoPin)
{
    private const int Iteraciones = 100_000;
    private const int TamanoSalBytes = 16;
    private const int TamanoHashBytes = 32;

    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromSeconds(30);
    private const int IntentosMaximos = 5;

    private int _intentosFallidos;
    private DateTime? _bloqueadoHasta;

    public bool ExistePin() => File.Exists(rutaArchivoPin);

    public void CrearPin(string pin)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSalBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHashBytes);

        Directory.CreateDirectory(Path.GetDirectoryName(rutaArchivoPin)!);
        var contenido = $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
        File.WriteAllText(rutaArchivoPin, contenido);
    }

    public ResultadoValidacionPin Validar(string pin)
    {
        if (_bloqueadoHasta is { } hasta)
        {
            if (DateTime.UtcNow < hasta)
            {
                var restante = hasta - DateTime.UtcNow;
                return ResultadoValidacionPin.Bloqueado(restante);
            }

            _bloqueadoHasta = null;
            _intentosFallidos = 0;
        }

        var partes = File.ReadAllText(rutaArchivoPin).Split('.');
        var iteraciones = int.Parse(partes[0]);
        var sal = Convert.FromBase64String(partes[1]);
        var hashEsperado = Convert.FromBase64String(partes[2]);

        var hashIngresado = Rfc2898DeriveBytes.Pbkdf2(pin, sal, iteraciones, HashAlgorithmName.SHA256, hashEsperado.Length);

        if (CryptographicOperations.FixedTimeEquals(hashIngresado, hashEsperado))
        {
            _intentosFallidos = 0;
            return ResultadoValidacionPin.Correcto();
        }

        _intentosFallidos++;
        if (_intentosFallidos >= IntentosMaximos)
        {
            _bloqueadoHasta = DateTime.UtcNow + DuracionBloqueo;
            return ResultadoValidacionPin.Bloqueado(DuracionBloqueo);
        }

        return ResultadoValidacionPin.Incorrecto(IntentosMaximos - _intentosFallidos);
    }
}

public readonly struct ResultadoValidacionPin
{
    public bool EsCorrecto { get; private init; }
    public bool EstaBloqueado { get; private init; }
    public int IntentosRestantes { get; private init; }
    public TimeSpan TiempoRestanteBloqueo { get; private init; }

    public static ResultadoValidacionPin Correcto() => new() { EsCorrecto = true };

    public static ResultadoValidacionPin Incorrecto(int intentosRestantes) => new()
    {
        EsCorrecto = false,
        IntentosRestantes = intentosRestantes
    };

    public static ResultadoValidacionPin Bloqueado(TimeSpan restante) => new()
    {
        EsCorrecto = false,
        EstaBloqueado = true,
        TiempoRestanteBloqueo = restante
    };
}
