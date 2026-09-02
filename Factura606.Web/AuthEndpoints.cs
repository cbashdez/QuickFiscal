using System.Security.Claims;
using Factura606.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Factura606.Web;

/// <summary>
/// Endpoints de login por PIN — formularios HTML simples (no componentes Blazor
/// interactivos) para poder emitir la cookie de autenticación con un POST normal,
/// sin pelear con el ciclo de vida de SignalR. Ver sección 3.2/3.7.5 del documento maestro.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/account/crear-pin", async (HttpContext ctx, GestorPin gestorPin) =>
        {
            if (gestorPin.ExistePin())
            {
                return Results.Redirect("/login?error=ya-existe");
            }

            var form = await ctx.Request.ReadFormAsync();
            var pin = form["pin"].ToString();
            var confirmacion = form["confirmacion"].ToString();

            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
                return Results.Redirect("/login?modo=crear&error=corto");

            if (pin != confirmacion)
                return Results.Redirect("/login?modo=crear&error=no-coincide");

            gestorPin.CrearPin(pin);
            await FirmarSesionAsync(ctx);
            return Results.Redirect("/");
        });

        app.MapPost("/account/login", async (HttpContext ctx, GestorPin gestorPin) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var pin = form["pin"].ToString();

            var resultado = gestorPin.Validar(pin);

            if (resultado.EsCorrecto)
            {
                await FirmarSesionAsync(ctx);
                return Results.Redirect("/");
            }

            if (resultado.EstaBloqueado)
            {
                var segundos = (int)Math.Ceiling(resultado.TiempoRestanteBloqueo.TotalSeconds);
                return Results.Redirect($"/login?error=bloqueado&segundos={segundos}");
            }

            return Results.Redirect($"/login?error=incorrecto&intentos={resultado.IntentosRestantes}");
        });

        app.MapPost("/account/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });

        return app;
    }

    private static Task FirmarSesionAsync(HttpContext ctx)
    {
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Usuario Factura606")
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
    }
}
