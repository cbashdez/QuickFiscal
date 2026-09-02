using Factura606.Core.Data;
using Factura606.Core.Services;
using Factura606.Web;
using Factura606.Web.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Rutas de esta instalación (PC de casa) --------------------------------------
builder.Services.Configure<RutasOptions>(builder.Configuration.GetSection(RutasOptions.SeccionConfig));
var rutas = builder.Configuration.GetSection(RutasOptions.SeccionConfig).Get<RutasOptions>() ?? new RutasOptions();
var contentRoot = builder.Environment.ContentRootPath;
var rutaBaseDeDatos = rutas.Resolver(contentRoot, rutas.BaseDeDatos);
var rutaArchivoPin = rutas.Resolver(contentRoot, rutas.ArchivoPin);
var rutaCarpetaFotos = rutas.Resolver(contentRoot, rutas.CarpetaFotos);
Directory.CreateDirectory(Path.GetDirectoryName(rutaBaseDeDatos)!);
Directory.CreateDirectory(rutaCarpetaFotos);

// ---- Claude OCR: appsettings, y si no hay ApiKey ahí, cae a la variable de entorno
// ANTHROPIC_API_KEY (recomendado para no comitear la key en appsettings.json) --------
builder.Services.Configure<ClaudeOcrOptions>(builder.Configuration.GetSection(ClaudeOcrOptions.SeccionConfig));
builder.Services.PostConfigure<ClaudeOcrOptions>(opciones =>
{
    if (string.IsNullOrWhiteSpace(opciones.ApiKey))
    {
        opciones.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
    }
});

// ---- Base de datos (SQLite, un solo archivo en App_Data — sección 3.2) ------------
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={rutaBaseDeDatos}"));

// ---- Servicios de negocio (Factura606.Core) ---------------------------------------
builder.Services.AddHttpClient<ClaudeOcrService>();
builder.Services.AddScoped<QrService>();
builder.Services.AddScoped<ConversorImagenService>();
builder.Services.AddScoped<ExtractorFactura>();
builder.Services.AddScoped<FacturaService>();
builder.Services.AddSingleton<ExportadorExcelService>();

// GestorPin es singleton a propósito: el bloqueo por intentos fallidos vive en
// memoria de la instancia (ver comentario en GestorPin.cs).
builder.Services.AddSingleton(new GestorPin(rutaArchivoPin));

// ---- Login por PIN vía cookie de sesión del servidor (sección 3.2 / 3.7.5) --------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "Factura606.Auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Las fotos de facturas (celular, varios MB c/u) superan el límite por defecto de
// InputFile/SignalR — se sube a 30MB para no truncar cargas en lote.
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(o =>
{
    o.MaximumReceiveMessageSize = 30 * 1024 * 1024;
});

var app = builder.Build();

// Crea el esquema de la base de datos si hace falta (no migra una BD ya existente
// — ver el gotcha de EnsureCreated en la sección 2.3 del documento maestro).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().AsegurarBaseDeDatos();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapApiEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
