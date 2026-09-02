using System.Text.RegularExpressions;

namespace Factura606.Core.Utils;

public static partial class TextoUtils
{
    /// <summary>Deja solo dígitos de un string — usado para normalizar el RNC (sin guiones).</summary>
    public static string? SoloDigitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return valor;
        return SoloDigitosRegex().Replace(valor, string.Empty);
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex SoloDigitosRegex();
}
