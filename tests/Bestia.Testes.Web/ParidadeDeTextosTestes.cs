using System.Text.RegularExpressions;
using Bestia.Recursos;
using NUnit.Framework;

namespace Bestia.Testes.Web;

/// <summary>Garante, a cada build, que nenhum texto visível fica fora do arquivo mestre de idiomas.</summary>
public partial class ParidadeDeTextosTestes
{
    private static string Raiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Bestia.sln")))
            pasta = pasta.Parent;
        return pasta!.FullName;
    }

    private static IEnumerable<string> Arquivos(string extensao, string subpasta = "src") =>
        Directory.EnumerateFiles(Path.Combine(Raiz(), subpasta), extensao, SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !a.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}")
                     && !a.EndsWith("Textos.cs"));

    [GeneratedRegex(@"Textos\.([A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+)")]
    private static partial Regex ChaveTipada();

    [GeneratedRegex("[\"'](?:Menu|Conta|Sala|Partida|Regras|Perfil|Config|Privacidade|Legal|Comum|Emote|Email|Erro)_[A-Za-z0-9_]+[\"']")]
    private static partial Regex ChaveEmTexto();

    private static readonly HashSet<string> Chaves = ProvedorDeTextos.Mestre.Keys.ToHashSet();

    [Test]
    public void Toda_ChaveUsadaNosFontes_ExisteNoArquivoMestre()
    {
        var faltando = new List<string>();
        foreach (var arquivo in Arquivos("*.cs").Concat(Arquivos("*.cshtml")).Concat(Arquivos("*.js")))
        {
            var texto = File.ReadAllText(arquivo);
            foreach (Match m in ChaveTipada().Matches(texto))
            {
                if (!Chaves.Contains(m.Groups[1].Value))
                    faltando.Add($"{Path.GetFileName(arquivo)}: {m.Groups[1].Value}");
            }
            foreach (Match m in ChaveEmTexto().Matches(texto))
            {
                var chave = m.Value.Trim('"', '\'');
                if (!Chaves.Contains(chave))
                    faltando.Add($"{Path.GetFileName(arquivo)}: {chave}");
            }
        }

        Assert.That(faltando, Is.Empty);
    }

    [Test]
    public void Chaves_DasRegras_DevemTerTodosOsParagrafosDeclarados()
    {
        int[] paragrafos = [2, 2, 2, 2, 2, 4, 5, 4, 2, 4, 2, 3];
        for (var secao = 1; secao <= paragrafos.Length; secao++)
        {
            Assert.That(Chaves, Does.Contain($"Regras_Sec{secao:00}_Titulo"));
            for (var p = 1; p <= paragrafos[secao - 1]; p++)
                Assert.That(Chaves, Does.Contain($"Regras_Sec{secao:00}_P{p}"));
            Assert.That(Chaves, Does.Not.Contain($"Regras_Sec{secao:00}_P{paragrafos[secao - 1] + 1}"));
        }
    }

    [Test]
    public void Views_NaoDevemTerTextoVisivelSolto()
    {
        var soltos = new List<string>();
        foreach (var arquivo in Arquivos("*.cshtml"))
        {
            var profundidade = 0;
            var numero = 0;
            foreach (var linha in File.ReadLines(arquivo))
            {
                numero++;
                var corte = linha.Trim();
                var estavaEmCodigo = profundidade > 0;
                if (corte.StartsWith("@{") || estavaEmCodigo)
                    profundidade += corte.Count(c => c == '{') - corte.Count(c => c == '}');
                if (estavaEmCodigo || corte.StartsWith("@{") || corte.StartsWith('@') || corte is "{" or "}" || corte.Length == 0)
                    continue;

                if (corte.StartsWith("else"))
                    continue;
                var semMarcacao = Regex.Replace(RemoverExpressoesRazor(corte), "<[^>]+>", " ");
                semMarcacao = Regex.Replace(semMarcacao, @"[{}]", " ");
                if (Regex.IsMatch(semMarcacao, @"[A-Za-zÀ-ÖØ-öø-ÿ]{2,}"))
                    soltos.Add($"{Path.GetFileName(arquivo)}:{numero}: {corte}");
            }
        }

        Assert.That(soltos, Is.Empty);
    }

    /// <summary>Tira de uma linha as expressões <c>@x</c>, <c>@x.y(...)</c>, <c>@x[...]</c> e <c>@(...)</c>.</summary>
    private static string RemoverExpressoesRazor(string linha)
    {
        var resultado = new System.Text.StringBuilder();
        var i = 0;
        while (i < linha.Length)
        {
            if (linha[i] != '@')
            {
                resultado.Append(linha[i++]);
                continue;
            }

            i++;
            if (i < linha.Length && linha[i] == '(')
                i = PularBalanceado(linha, i);
            while (i < linha.Length && (char.IsLetterOrDigit(linha[i]) || linha[i] == '_' || linha[i] == '.' || linha[i] == '!'))
            {
                i++;
                while (i < linha.Length && (linha[i] == '(' || linha[i] == '['))
                    i = PularBalanceado(linha, i);
            }
            resultado.Append(' ');
        }
        return resultado.ToString();
    }

    private static int PularBalanceado(string texto, int inicio)
    {
        var abre = texto[inicio];
        var fecha = abre == '(' ? ')' : ']';
        var nivel = 0;
        for (var i = inicio; i < texto.Length; i++)
        {
            if (texto[i] == abre) nivel++;
            if (texto[i] == fecha && --nivel == 0) return i + 1;
        }
        return texto.Length;
    }

    [Test]
    public void ScriptsDoNavegador_NaoDevemTerTextoEmPortuguesNoCodigo()
    {
        var achados = new List<string>();
        foreach (var arquivo in Arquivos("*.js", "src/Bestia.Web/wwwroot/js"))
        {
            var codigo = Regex.Replace(File.ReadAllText(arquivo), @"/\*.*?\*/|//[^\n]*", "", RegexOptions.Singleline);
            foreach (Match literal in Regex.Matches(codigo, @"'[^'\n]*[À-ÖØ-öø-ÿ][^'\n]*'"))
                achados.Add($"{Path.GetFileName(arquivo)}: {literal.Value}");
        }

        Assert.That(achados, Is.Empty);
    }
}
