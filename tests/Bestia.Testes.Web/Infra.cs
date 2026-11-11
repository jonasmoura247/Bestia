using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bestia.Testes.Aplicacao
{
    internal sealed class VerificadorDeBancoFalso : IVerificadorDeBanco
    {
        public bool Disponivel { get; set; } = true;

        public Task<bool> EstaDisponivelAsync(CancellationToken ct = default) => Task.FromResult(Disponivel);
    }
}

namespace Bestia.Testes.Web
{
    using Bestia.Testes.Aplicacao;

    internal class BestiaFactory : WebApplicationFactory<Program>
    {
        public RepositorioEmMemoria Repositorio { get; } = new();

        public EnviadorDeEmailFalso Email { get; } = new();

        public RelogioFalso Relogio { get; } = new();

        public VerificadorDeBancoFalso Banco { get; } = new();

        public string Ambiente { get; init; } = "Development";

        public Dictionary<string, string?> Configuracoes { get; } = new()
        {
            ["Bestia:ExigirHttps"] = "false",
            ["Bestia:RequisicoesPorMinutoNasContas"] = "1000",
            ["Privacidade:EncarregadoEmail"] = "dpo@exemplo.com",
            ["Seguranca:ChaveMestra"] = Convert.ToBase64String(Enumerable.Range(10, 32).Select(i => (byte)i).ToArray()),
            ["Seguranca:ChaveDeIndice"] = Convert.ToBase64String(Enumerable.Range(60, 32).Select(i => (byte)i).ToArray()),
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Ambiente);
            foreach (var (chave, valor) in Configuracoes)
                builder.UseSetting(chave, valor);
            builder.ConfigureServices(servicos =>
            {
                servicos.RemoveAll<IRepositorio>();
                servicos.AddSingleton<IRepositorio>(Repositorio);
                servicos.RemoveAll<IUnidadeDeTrabalho>();
                servicos.AddSingleton<IUnidadeDeTrabalho>(new UnidadeDeTrabalhoFalsa(Repositorio));
                servicos.RemoveAll<IEnviadorDeEmail>();
                servicos.AddSingleton<IEnviadorDeEmail>(Email);
                servicos.RemoveAll<IRelogio>();
                servicos.AddSingleton<IRelogio>(Relogio);
                servicos.RemoveAll<IVerificadorDeBanco>();
                servicos.AddSingleton<IVerificadorDeBanco>(Banco);
                servicos.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, FiltroDeIp>();
                servicos.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
            });
        }

        public ClienteDeTeste NovoCliente(string? idioma = null) =>
            new(CreateDefaultClient(new RedirecionamentoDesligado()), idioma);

        private sealed class RedirecionamentoDesligado : DelegatingHandler;
    }

    /// <summary>Define o IP da conexão quando o pedido traz o cabeçalho X-Teste-Ip (o TestServer não tem IP).</summary>
    internal sealed class FiltroDeIp : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> proximo) => app =>
        {
            app.Use(async (http, seguinte) =>
            {
                if (http.Request.Headers.TryGetValue("X-Teste-Ip", out var ip))
                    http.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                await seguinte();
            });
            proximo(app);
        };
    }

    /// <summary>HttpClient com cookies manuais, para também montar o cabeçalho do SignalR.</summary>
    internal sealed class ClienteDeTeste(HttpClient http, string? idioma = null)
    {
        private readonly Dictionary<string, string> _cookies = [];

        public string CookieHeader => string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}"));

        public bool TemCookie(string nome) => _cookies.ContainsKey(nome);

        public Task<HttpResponseMessage> GetAsync(string url) => EnviarAsync(new HttpRequestMessage(HttpMethod.Get, url));

        public async Task<string> TextoAsync(string url) => await (await GetAsync(url)).CorpoAsync();

        /// <summary>Busca o token anti-falsificação em <paramref name="paginaDoToken"/> e envia o formulário.</summary>
        public async Task<HttpResponseMessage> PostAsync(string url, Dictionary<string, string> campos, string paginaDoToken = "/conta/entrar")
        {
            var html = await TextoAsync(paginaDoToken);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            return await PostSemTokenAsync(url, new Dictionary<string, string>(campos) { ["__RequestVerificationToken"] = token });
        }

        public Task<HttpResponseMessage> PostSemTokenAsync(string url, Dictionary<string, string> campos) =>
            EnviarAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(campos) });

        public async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage pedido)
        {
            if (_cookies.Count > 0)
                pedido.Headers.Add("Cookie", CookieHeader);
            if (idioma is not null)
                pedido.Headers.Add("Accept-Language", idioma);
            var resposta = await http.SendAsync(pedido);
            if (resposta.Headers.TryGetValues("Set-Cookie", out var definidos))
            {
                foreach (var definido in definidos)
                {
                    var par = definido.Split(';')[0];
                    var indice = par.IndexOf('=');
                    var nome = par[..indice];
                    var valor = par[(indice + 1)..];
                    if (valor.Length == 0 || definido.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                        _cookies.Remove(nome);
                    else
                        _cookies[nome] = valor;
                }
            }
            return resposta;
        }

        public async Task<HttpResponseMessage> CadastrarAsync(string apelido, string email, string senha = "uma-senha-bem-longa") =>
            await PostAsync("/conta/criar", new()
            {
                ["Apelido"] = apelido,
                ["Email"] = email,
                ["Senha"] = senha,
                ["AceitoTermos"] = "true",
                ["DeclaroMaioridade"] = "true",
            });

        public async Task<HttpResponseMessage> EntrarAsync(string email, string senha = "uma-senha-bem-longa") =>
            await PostAsync("/conta/entrar", new() { ["Email"] = email, ["Senha"] = senha });
    }

    internal static class Extensoes
    {
        /// <summary>Caminho do redirecionamento, mesmo quando o servidor o devolve absoluto.</summary>
        public static string Local(this HttpResponseMessage resposta)
        {
            var destino = resposta.Headers.Location;
            return destino is null ? "" : destino.IsAbsoluteUri ? destino.PathAndQuery : destino.OriginalString;
        }

        /// <summary>Corpo da resposta com as entidades HTML do Razor (como &amp;#xE1;) já decodificadas.</summary>
        public static async Task<string> CorpoAsync(this HttpResponseMessage resposta) =>
            System.Net.WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        public static HttpStatusCode Status(this HttpResponseMessage resposta) => resposta.StatusCode;
    }
}
