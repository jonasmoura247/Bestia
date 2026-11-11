using Bestia.Dominio.Cartas;
using Bestia.Dominio.Partidas;

namespace Bestia.Dominio.Bots;

/// <summary>Bot do protótipo: avalia a mão, decide baú e escolhe a carta. Só chame com a mão em Decisao ou Rodadas.</summary>
public sealed class EstrategiaDeBot(ISorteador sorteador)
{
    private const double ValorMinimoParaFicar = 1.7;
    private const double ValorMaximoParaTrocarPorBau = 1.5;
    private const double ValorMaximoParaSegundoBau = 1.4;

    public void AgirNaVez(Mao mao)
    {
        if (mao.Fase == FaseDaMao.Decisao)
            Decidir(mao);
        else
            JogarCarta(mao);
    }

    private void Decidir(Mao mao)
    {
        var bot = mao.JogadorDaVez!;
        if (!mao.PodeFicar(bot))
        {
            mao.Decidir(ficar: false);
            return;
        }

        var livres = mao.BausLivres().ToList();
        if (livres.Count > 0 && Valor(mao, bot) < ValorMaximoParaTrocarPorBau && Sortear(100) < 60)
        {
            mao.PegarBau(livres[0]);
            var restantes = mao.BausLivres().ToList();
            if (restantes.Count > 0 && Valor(mao, bot) < ValorMaximoParaSegundoBau && Sortear(100) < 50)
                mao.PegarBau(restantes[0]);
            // Pegar o último baú já encerra a decisão; só fecha aqui se ainda for a vez dele.
            if (mao.JogadorDaVez == bot && mao.Fase == FaseDaMao.Decisao)
                mao.Decidir(ficar: true);
            return;
        }

        mao.Decidir(Valor(mao, bot) >= ValorMinimoParaFicar || Sortear(100) < 15);
    }

    private static void JogarCarta(Mao mao)
    {
        var permitidas = mao.CartasPermitidas();
        var naMesa = mao.JogadasDaRodada.Select(j => j.Carta).ToList();
        var mania = mao.Mania;

        if (naMesa.Count == 0)
        {
            mao.JogarCarta(permitidas.MaxBy(c => ComparadorDeCartas.Forca(c, mania)));
            return;
        }

        // Joga a mais fraca que ainda vence; se nenhuma vence, descarta a mais fraca.
        var vencedoras = permitidas
            .Where(c => ComparadorDeCartas.IndiceDaVencedora([.. naMesa, c], mania) == naMesa.Count)
            .ToList();
        var candidatas = vencedoras.Count > 0 ? vencedoras : permitidas;
        mao.JogarCarta(candidatas.MinBy(c => ComparadorDeCartas.Forca(c, mania)));
    }

    private static double Valor(Mao mao, Jogadores.Jogador bot) =>
        mao.CartasDe(bot).Sum(c => (((int)c.Valor) + 1) / 12.0 * (c.Naipe == mao.Mania ? 2.2 : 1.0));

    private int Sortear(int limite) => sorteador.Sortear(limite);
}
