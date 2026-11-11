using System.Diagnostics.CodeAnalysis;
using Bestia.Infraestrutura.Persistencia;
using Bestia.Web.Hubs;
using Microsoft.EntityFrameworkCore;
using Bestia.Web.Idioma;

namespace Bestia.Web.Configuracao;

public static class PipelineDoBestia
{
    public const string PoliticaDeConteudo =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "connect-src 'self' ws: wss:; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>Aplica as migrações ao subir (ambientes de desenvolvimento e contêiner); precisa de um SQL Server real.</summary>
    [ExcludeFromCodeCoverage]
    private static void MigrarSeConfigurado(WebApplication app)
    {
        if (!app.Configuration.GetValue("Bestia:MigrarAoIniciar", false))
            return;
        using var escopo = app.Services.CreateScope();
        escopo.ServiceProvider.GetRequiredService<BestiaDbContext>().Database.Migrate();
    }

    public static WebApplication UsarBestia(this WebApplication app)
    {
        MigrarSeConfigurado(app);
        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/erro");
        if (app.Configuration.GetValue("Bestia:ExigirHttps", !app.Environment.IsDevelopment()))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (http, proximo) =>
        {
            var cabecalhos = http.Response.Headers;
            cabecalhos["Content-Security-Policy"] = PoliticaDeConteudo;
            cabecalhos["X-Content-Type-Options"] = "nosniff";
            cabecalhos["Referrer-Policy"] = "no-referrer";
            cabecalhos["X-Frame-Options"] = "DENY";
            cabecalhos["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await proximo();
        });
        app.UseStatusCodePagesWithReExecute("/erro", "?codigo={0}");
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<MiddlewareDeIdioma>();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<MesaHub>("/hub/mesa");
        app.MapHealthChecks("/saude").AllowAnonymous();
        return app;
    }
}
