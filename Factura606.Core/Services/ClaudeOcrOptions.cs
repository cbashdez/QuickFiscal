namespace Factura606.Core.Services;

/// <summary>Configuración para <see cref="ClaudeOcrService"/> — se carga desde appsettings/env/user-secrets.</summary>
public class ClaudeOcrOptions
{
    public const string SeccionConfig = "ClaudeOcr";

    public string ApiKey { get; set; } = string.Empty;

    public string Modelo { get; set; } = "claude-sonnet-5";

    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// RNC de la propia empresa (compradora) — Transporte Carretón JH SRL por defecto.
    /// Se usa para que el prompt de OCR distinga el RNC del proveedor del propio, y
    /// también como "RNC declarante" al exportar (mismo dato, una sola fuente de verdad).
    /// </summary>
    public string RncPropio { get; set; } = "130865752";
}
