using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Bestia.Testes.Integracao;

/// <summary>Falha o build se um pacote de produção (direto ou transitivo) não declarar a licença MIT.</summary>
public class LicencasTestes
{
    private static string Raiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Bestia.sln")))
            pasta = pasta.Parent;
        return pasta!.FullName;
    }

    private static string PastaDoNuget() =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static string LicencaDe(string id, string versao)
    {
        var nuspec = Path.Combine(PastaDoNuget(), id.ToLowerInvariant(), versao, id.ToLowerInvariant() + ".nuspec");
        if (!File.Exists(nuspec))
            return "SEM-NUSPEC";
        var texto = File.ReadAllText(nuspec);
        var expressao = Regex.Match(texto, "<license type=\"expression\">([^<]+)</license>");
        if (expressao.Success)
            return expressao.Groups[1].Value.Trim();
        return Regex.IsMatch(texto, "<license type=\"file\">")
            ? "ARQUIVO-DE-LICENCA"
            : "DESCONHECIDA";
    }

    /// <summary>Sem expressão SPDX no NuGet, mas o licenseUrl aponta para o LICENSE.TXT MIT do dotnet/corefx.</summary>
    private static readonly HashSet<string> MitConferidosNaOrigem = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.CSharp", "System.CodeDom", "System.Memory", "System.Numerics.Vectors", "System.Threading.Tasks.Extensions",
    };

    /// <summary>Licença da Microsoft que não é MIT (.NET Library / SNI); cada uma precisa estar descrita em LICENCAS.md.</summary>
    private static readonly HashSet<string> ExcecoesDocumentadas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.NETCore.Platforms", "Microsoft.NETCore.Targets", "System.Runtime", "System.Text.Encoding",
        "Microsoft.Data.SqlClient.SNI.runtime",
    };

    [Test]
    public void PacotesDeProducao_DevemSerMIT()
    {
        var fora = new SortedSet<string>();
        var total = 0;
        foreach (var projeto in Directory.EnumerateFiles(Path.Combine(Raiz(), "src"), "project.assets.json", SearchOption.AllDirectories))
        {
            using var assets = JsonDocument.Parse(File.ReadAllText(projeto));
            foreach (var biblioteca in assets.RootElement.GetProperty("libraries").EnumerateObject())
            {
                if (biblioteca.Value.GetProperty("type").GetString() != "package")
                    continue;
                var partes = biblioteca.Name.Split('/');
                total++;
                var licenca = LicencaDe(partes[0], partes[1]);
                if (licenca != "MIT" && !MitConferidosNaOrigem.Contains(partes[0]) && !ExcecoesDocumentadas.Contains(partes[0]))
                    fora.Add($"{partes[0]} {partes[1]}: {licenca}");
            }
        }

        var documento = File.ReadAllText(Path.Combine(Raiz(), "LICENCAS.md"));
        Assert.That(ExcecoesDocumentadas.Where(p => !documento.Contains(p)), Is.Empty, "exceção sem registro em LICENCAS.md");
        Assert.That(total, Is.GreaterThan(10), "nenhum pacote encontrado: rode o restore antes");
        Assert.That(fora, Is.Empty, "pacotes de produção fora da regra MIT");
    }
}
