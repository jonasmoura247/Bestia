using Bestia.Dominio;
using Bestia.Dominio.Bots;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;
using NUnit.Framework;

namespace Bestia.Testes.Dominio;

public class SimulacaoTestes
{
    private const int LimiteDeMaos = 5000;

    [Test]
    public void Deve_ConservarAsFichas_EAcabar_EmMilPartidasComBots([Range(1, 1000)] int semente)
    {
        var sorteador = new SorteadorComSemente(semente);
        var bots = new EstrategiaDeBot(sorteador);
        var quantidade = 3 + semente % 8;
        var jogadores = Enumerable.Range(0, quantidade).Select(i => new Jogador($"Bot{i}", ehBot: true)).ToList();
        var total = quantidade * ConfiguracaoDoJogo.FichasIniciais;
        var partida = new Partida(jogadores, sorteador);

        var maos = 0;
        while (!partida.Encerrada && maos++ < LimiteDeMaos)
        {
            var mao = partida.IniciarMao();
            while (mao.Fase != FaseDaMao.Concluida)
                bots.AgirNaVez(mao);
            partida.EncerrarMao();

            Assert.That(jogadores.Sum(j => j.Fichas) + partida.MontantePendente, Is.EqualTo(total));
            Assert.That(jogadores.All(j => j.Fichas >= 0), Is.True);
        }

        Assert.That(partida.Encerrada, Is.True);
    }
}
