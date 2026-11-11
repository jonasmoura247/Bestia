using System.Threading.RateLimiting;
using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Bestia.Aplicacao.Perfil;
using Bestia.Aplicacao.Privacidade;
using Bestia.Dominio;
using Bestia.Infraestrutura;
using Bestia.Infraestrutura.Email;
using Bestia.Infraestrutura.Persistencia;
using Bestia.Infraestrutura.Repositorios;
using Bestia.Infraestrutura.Seguranca;
using Bestia.Recursos;
using Bestia.Web.Hubs;
using Bestia.Web.Idioma;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace Bestia.Web.Configuracao;

public sealed class SaudeDoBanco(IVerificadorDeBanco verificador) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken ct = default) =>
        await verificador.EstaDisponivelAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("banco indisponível");
}

public static class ServicosDoBestia
{
    // Só para desenvolvimento: em produção a ausência das chaves impede a aplicação de subir.
    private const string ChaveMestraDeDesenvolvimento = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private const string ChaveDeIndiceDeDesenvolvimento = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8=";

    public static IServiceCollection AdicionarBestia(this IServiceCollection servicos, IConfiguration config, IHostEnvironment ambiente)
    {
        servicos.AddSingleton<ISorteador, SorteadorCriptografico>();
        servicos.AddSingleton<IRelogio, RelogioDoSistema>();
        servicos.AddSingleton<ITextos, ProvedorDeTextos>();
        servicos.AddSingleton<ICriptografia>(_ => CriarCriptografia(config, ambiente));
        servicos.AddSingleton<IHashDeSenha, HashDeSenhaIdentity>();

        servicos.AddDbContextFactory<BestiaDbContext>(o => o.UseSqlServer(config.GetConnectionString("Bestia")));
        servicos.AddScoped(p => p.GetRequiredService<IDbContextFactory<BestiaDbContext>>().CreateDbContext());
        servicos.AddScoped<IRepositorio, RepositorioEf>();
        servicos.AddSingleton<IUnidadeDeTrabalho, UnidadeDeTrabalhoEf>();
        servicos.AddSingleton<IVerificadorDeBanco, VerificadorDeBancoEf>();

        servicos.AddSingleton<CaixaDeEmails>();
        var opcoesDeEmail = config.GetSection("Email").Get<OpcoesDeEmail>() ?? new OpcoesDeEmail();
        servicos.AddSingleton(opcoesDeEmail);
        if (string.IsNullOrWhiteSpace(opcoesDeEmail.Servidor) && ambiente.IsDevelopment())
            servicos.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailNoLog>();
        else
            servicos.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailSmtp>();

        servicos.AddScoped<ContaServico>();
        servicos.AddSingleton<MesaServico>();
        servicos.AddSingleton<INotificadorDaMesa, NotificadorSignalR>();
        servicos.AddSingleton<ControleDeConexoes>();
        servicos.AddSingleton<LimitadorDeMensagens>();
        servicos.AddScoped<ContextoDeIdioma>();
        servicos.AddScoped<Tradutor>();

        servicos.AddControllersWithViews(opcoes =>
        {
            opcoes.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            opcoes.Filters.Add(new AuthorizeFilter());
        }).AddRazorOptions(razor => razor.ViewLocationFormats.Add("/Views/Compartilhado/{0}.cshtml"));
        servicos.AddScoped<PerfilServico>();
        servicos.AddScoped<PrivacidadeServico>();
        servicos.AddHostedService<RotinasDeRetencao>();
        servicos.AddSignalR();

        // Chaves dos cookies e dos tokens em volume persistente, para sobreviverem a novos deploys.
        var pastaDasChaves = config["DataProtection:Pasta"];
        if (!string.IsNullOrWhiteSpace(pastaDasChaves))
            servicos.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(pastaDasChaves)).SetApplicationName("Bestia");
        servicos.AddHealthChecks().AddCheck<SaudeDoBanco>("banco");
        AdicionarLimiteDeRequisicoes(servicos, config);
        AdicionarAutenticacao(servicos, config);
        return servicos;
    }

    public static ICriptografia CriarCriptografia(IConfiguration config, IHostEnvironment ambiente)
    {
        var mestra = config["Seguranca:ChaveMestra"];
        var indice = config["Seguranca:ChaveDeIndice"];
        if (string.IsNullOrWhiteSpace(mestra) || string.IsNullOrWhiteSpace(indice))
        {
            if (!ambiente.IsDevelopment())
                throw new InvalidOperationException("Configure Seguranca:ChaveMestra e Seguranca:ChaveDeIndice (32 bytes em base64).");
            mestra = ChaveMestraDeDesenvolvimento;
            indice = ChaveDeIndiceDeDesenvolvimento;
        }

        var chaves = new Dictionary<byte, byte[]> { [1] = Convert.FromBase64String(mestra) };
        return new CriptografiaAesGcm(chaves, 1, Convert.FromBase64String(indice));
    }

    private static void AdicionarLimiteDeRequisicoes(IServiceCollection servicos, IConfiguration config)
    {
        var limite = config.GetValue("Bestia:RequisicoesPorMinutoNasContas", 10);
        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy("conta", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    private static void AdicionarAutenticacao(IServiceCollection servicos, IConfiguration config)
    {
        var autenticacao = servicos.AddAuthentication(Sessao.EsquemaDoCookie)
            .AddCookie(Sessao.EsquemaDoCookie, opcoes =>
            {
                opcoes.Cookie.Name = "bestia.sessao";
                opcoes.Cookie.HttpOnly = true;
                opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opcoes.Cookie.SameSite = SameSiteMode.Lax;
                opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
                opcoes.SlidingExpiration = true;
                opcoes.LoginPath = "/conta/entrar";
                opcoes.AccessDeniedPath = "/conta/entrar";
                opcoes.Events.OnValidatePrincipal = async contexto =>
                {
                    var contas = contexto.HttpContext.RequestServices.GetRequiredService<ContaServico>();
                    if (!await Sessao.EhValidaAsync(contas, contexto.Principal))
                    {
                        contexto.RejectPrincipal();
                        await contexto.HttpContext.SignOutAsync(Sessao.EsquemaDoCookie);
                    }
                };
            })
            .AddCookie(Sessao.EsquemaExterno, opcoes =>
            {
                opcoes.Cookie.Name = "bestia.externo";
                opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opcoes.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            });

        var clientId = config["Google:ClientId"];
        var segredo = config["Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(segredo))
        {
            autenticacao.AddGoogle(Sessao.Google, opcoes =>
            {
                opcoes.ClientId = clientId;
                opcoes.ClientSecret = segredo;
                opcoes.SignInScheme = Sessao.EsquemaExterno;
                opcoes.Scope.Add("email");
                opcoes.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
        }
    }
}
