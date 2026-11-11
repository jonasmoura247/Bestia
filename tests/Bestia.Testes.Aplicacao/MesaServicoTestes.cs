using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;
using DesfechoDaMao = Bestia.Dominio.Partidas.DesfechoDaMao;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

public class MesaServicoTestes
{
    private RepositorioEmMemoria _repositorio = null!;
    private RelogioFalso _relogio = null!;
    private NotificadorFalso _notificador = null!;
    private UnidadeDeTrabalhoFalsa _trabalho = null!;

    [SetUp]
    public void Preparar()
    {
        _repositorio = new RepositorioEmMemoria();
        _relogio = new RelogioFalso();
        _notificador = new NotificadorFalso();
        _trabalho = new UnidadeDeTrabalhoFalsa(_repositorio);
    }

    private MesaServico Novo(ISorteador? sorteador = null) =>
        new(_trabalho, _relogio, sorteador ?? new SorteadorComSemente(1), _notificador);

    /// <summary>
    /// Espera a ação agendada, com limite: se a mesa já reagendou outra, a tarefa lida pode nunca completar
    /// sem que o relógio avance, então o laço do teste simplesmente avança de novo.
    /// </summary>
    private static async Task EsperarAgendadaAsync(MesaServico servico, string codigo) =>
        await Task.WhenAny(servico.TarefaAgendadaDe(codigo), Task.Delay(200));

    private static PerfilDoJogador Perfil(string nome = "Ana", bool confirmado = true) =>
        new(Guid.NewGuid(), nome, "#e8c05a", "pt-BR", confirmado);

    private static async Task<string> SalaComAsync(MesaServico servico, PerfilDoJogador perfil, int maximo = 6, bool privada = false) =>
        (await servico.CriarSalaAsync(perfil, "Mesa do canto", maximo, privada)).Valor!;

    // ---------- sala de espera ----------

    [Test]
    public async Task Deve_CriarSala_GravarNoBanco_ENotificarOCriador()
    {
        var servico = Novo();
        var ana = Perfil();

        var resultado = await servico.CriarSalaAsync(ana, "  Mesa do canto ", 6, privada: false);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(resultado.Valor, Has.Length.EqualTo(6));
        var sala = _repositorio.Todos<Sala>().Single();
        Assert.That(sala.Nome, Is.EqualTo("Mesa do canto"));
        Assert.That(_repositorio.Todos<SalaJogador>().Single().JogadorId, Is.EqualTo(ana.ContaId));
        var visao = _notificador.Ultimas[ana.ContaId];
        Assert.That(visao.Fase, Is.EqualTo("espera"));
        Assert.That(visao.SouCriador, Is.True);
        Assert.That(visao.Assentos.Single().Nome, Is.EqualTo("Ana"));
        Assert.That(servico.CodigoDaSalaDe(ana.ContaId), Is.EqualTo(resultado.Valor));
        Assert.That(servico.CodigoDaSalaDe(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Deve_ListarSoSalasPublicasAbertas()
    {
        var servico = Novo();
        await servico.CriarSalaAsync(Perfil("Ana"), "Zeta", 6, privada: false);
        await servico.CriarSalaAsync(Perfil("Bia"), "Alfa", 4, privada: false);
        await servico.CriarSalaAsync(Perfil("Caio"), "Secreta", 4, privada: true);

        var lista = servico.ListarSalasPublicas();

        Assert.That(lista.Select(s => s.Nome), Is.EqualTo(new[] { "Alfa", "Zeta" }));
        Assert.That(lista[0], Is.EqualTo(new ResumoDaSala(lista[0].Codigo, "Alfa", 1, 4, false)));
    }

    [TestCase("ab", 6)]
    [TestCase("um nome de sala muito mas muito comprido", 6)]
    [TestCase("Mesa boa", 2)]
    [TestCase("Mesa boa", 11)]
    public async Task Deve_RecusarSalaInvalida(string nome, int maximo)
    {
        var resultado = await Novo().CriarSalaAsync(Perfil(), nome, maximo, false);

        Assert.That(resultado.ChaveDoErro, Is.EqualTo(ChavesDeErro.SalaInvalida));
    }

    [Test]
    public async Task Deve_ExigirEmailConfirmado_ENaoDeixarEstarEmDuasSalas()
    {
        var servico = Novo();
        var ana = Perfil();
        await SalaComAsync(servico, ana);

        var naoConfirmado = await servico.CriarSalaAsync(Perfil(confirmado: false), "Mesa boa", 4, false);
        var duas = await servico.CriarSalaAsync(ana, "Outra mesa", 4, false);
        var entrarSemConfirmar = await servico.EntrarAsync(Perfil(confirmado: false), "ABCDEF");

        Assert.That(naoConfirmado.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmailNaoConfirmado));
        Assert.That(duas.ChaveDoErro, Is.EqualTo(ChavesDeErro.JaEstaEmSala));
        Assert.That(entrarSemConfirmar.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmailNaoConfirmado));
    }

    [Test]
    public async Task Deve_GerarOutroCodigo_QuandoOSorteadoJaEstaEmUso()
    {
        var roteiro = Enumerable.Repeat(0, 12).Concat(Enumerable.Repeat(1, 6));
        var servico = Novo(new SorteadorRoteirizado(roteiro));

        var primeira = await SalaComAsync(servico, Perfil("Ana"));
        var segunda = await SalaComAsync(servico, Perfil("Bia"));

        Assert.That(primeira, Is.EqualTo("AAAAAA"));
        Assert.That(segunda, Is.EqualTo("BBBBBB"));
    }

    [Test]
    public async Task Deve_EntrarNaSala_PeloCodigo_SemImportarMaiusculas()
    {
        var servico = Novo();
        var codigo = await SalaComAsync(servico, Perfil("Ana"));
        var bia = Perfil("Bia");

        var resultado = await servico.EntrarAsync(bia, " " + codigo.ToLowerInvariant());
        var repetido = await servico.EntrarAsync(bia, codigo);

        Assert.That(resultado.Valor, Is.EqualTo(codigo));
        Assert.That(repetido.Sucesso, Is.True);
        Assert.That(_repositorio.Todos<SalaJogador>().Count(), Is.EqualTo(2));
        Assert.That(servico.ObterVisao(bia.ContaId)!.Assentos.Select(a => a.Nome), Is.EqualTo(new[] { "Ana", "Bia" }));
    }

    [Test]
    public async Task Deve_RecusarEntradaEmSalaInexistenteOuOutraSalaOuCheiaOuEmAndamento()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana, maximo: 3);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);
        await servico.EntrarAsync(Perfil("Caio"), codigo);

        var inexistente = await servico.EntrarAsync(Perfil("Duda"), "ZZZZZZ");
        var outraSala = await servico.EntrarAsync(bia, await SalaComAsync(servico, Perfil("Edu")));
        var cheia = await servico.EntrarAsync(Perfil("Fe"), codigo);
        await servico.IniciarPartidaAsync(ana.ContaId);
        var andamento = await servico.EntrarAsync(Perfil("Gabi"), codigo);

        Assert.That(inexistente.ChaveDoErro, Is.EqualTo(ChavesDeErro.SalaNaoEncontrada));
        Assert.That(outraSala.ChaveDoErro, Is.EqualTo(ChavesDeErro.JaEstaEmSala));
        Assert.That(cheia.ChaveDoErro, Is.EqualTo(ChavesDeErro.SalaCheia));
        Assert.That(andamento.ChaveDoErro, Is.EqualTo(ChavesDeErro.PartidaEmAndamento));
    }

    [Test]
    public async Task Deve_PassarACriacaoAoProximo_QuandoOCriadorSaiDaEspera()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);

        var saida = await servico.SairAsync(ana.ContaId);

        Assert.That(saida.Sucesso, Is.True);
        Assert.That(servico.ObterVisao(bia.ContaId)!.SouCriador, Is.True);
        Assert.That(servico.ObterVisao(ana.ContaId), Is.Null);
        Assert.That(_repositorio.Todos<SalaJogador>().Single(s => s.JogadorId == ana.ContaId).SaiuEm, Is.Not.Null);
    }

    [Test]
    public async Task Deve_ManterOCriador_QuandoOutroSaiDaEspera()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);

        await servico.SairAsync(bia.ContaId);

        Assert.That(servico.ObterVisao(ana.ContaId)!.SouCriador, Is.True);
        Assert.That(servico.ObterVisao(ana.ContaId)!.Assentos, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Deve_FecharASala_QuandoOUltimoSai()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);

        await servico.SairAsync(ana.ContaId);

        Assert.That(servico.ListarSalasPublicas(), Is.Empty);
        Assert.That((await servico.EntrarAsync(Perfil("Bia"), codigo)).ChaveDoErro, Is.EqualTo(ChavesDeErro.SalaNaoEncontrada));
        Assert.That(_repositorio.Todos<Sala>().Single().Situacao, Is.EqualTo(SituacaoDaSala.Encerrada));
        Assert.That((await servico.SairAsync(ana.ContaId)).ChaveDoErro, Is.EqualTo(ChavesDeErro.NaoEstaEmSala));
    }

    [Test]
    public async Task Deve_PreencherComBots_SoOCriador_ComLimiteDaSala()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana, maximo: 4);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);

        var naoCriador = await servico.PreencherComBotsAsync(bia.ContaId, 1);
        var invalido = await servico.PreencherComBotsAsync(ana.ContaId, 0);
        var ok = await servico.PreencherComBotsAsync(ana.ContaId, 9);

        Assert.That(naoCriador.ChaveDoErro, Is.EqualTo(ChavesDeErro.SomenteOCriador));
        Assert.That(invalido.ChaveDoErro, Is.EqualTo(ChavesDeErro.SalaInvalida));
        Assert.That(ok.Sucesso, Is.True);
        var visao = servico.ObterVisao(ana.ContaId)!;
        Assert.That(visao.Assentos, Has.Count.EqualTo(4));
        Assert.That(visao.Bots, Is.EqualTo(2));
        Assert.That((await servico.PreencherComBotsAsync(Guid.NewGuid(), 1)).ChaveDoErro, Is.EqualTo(ChavesDeErro.NaoEstaEmSala));
    }

    [Test]
    public async Task Deve_IniciarPartida_SoComTresOuMais_ESoOCriador()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);

        var naoCriador = await servico.IniciarPartidaAsync(bia.ContaId);
        var poucos = await servico.IniciarPartidaAsync(ana.ContaId);
        await servico.PreencherComBotsAsync(ana.ContaId, 1);
        var ok = await servico.IniciarPartidaAsync(ana.ContaId);
        var denovo = await servico.IniciarPartidaAsync(ana.ContaId);
        var bots = await servico.PreencherComBotsAsync(ana.ContaId, 1);

        Assert.That(naoCriador.ChaveDoErro, Is.EqualTo(ChavesDeErro.SomenteOCriador));
        Assert.That(poucos.ChaveDoErro, Is.EqualTo(ChavesDeErro.JogadoresInsuficientes));
        Assert.That(ok.Sucesso, Is.True);
        Assert.That(denovo.ChaveDoErro, Is.EqualTo(ChavesDeErro.PartidaEmAndamento));
        Assert.That(bots.ChaveDoErro, Is.EqualTo(ChavesDeErro.PartidaEmAndamento));
        Assert.That((await servico.IniciarPartidaAsync(Guid.NewGuid())).ChaveDoErro, Is.EqualTo(ChavesDeErro.NaoEstaEmSala));
        Assert.That(servico.ListarSalasPublicas().Single().EmPartida, Is.True);
        var partida = _repositorio.Todos<Partida>().Single();
        Assert.That(partida.Situacao, Is.EqualTo(SituacaoDaPartida.EmAndamento));
        Assert.That(_repositorio.Todos<PartidaJogador>().Count(), Is.EqualTo(3));
    }

    // ---------- comandos e emotes ----------

    private async Task<(MesaServico Servico, PerfilDoJogador Ana, string Codigo)> PartidaComBotsAsync(int bots = 2, ISorteador? sorteador = null)
    {
        var servico = Novo(sorteador);
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        await servico.PreencherComBotsAsync(ana.ContaId, bots);
        await servico.IniciarPartidaAsync(ana.ContaId);
        return (servico, ana, codigo);
    }

    [Test]
    public async Task Deve_RecusarAcoesForaDaVezOuFora_DaPartida()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        await SalaComAsync(servico, ana);
        var foraDaPartida = await servico.DecidirAsync(ana.ContaId, true);
        var semSala = await servico.DecidirAsync(Guid.NewGuid(), true);
        await servico.PreencherComBotsAsync(ana.ContaId, 2);
        await servico.IniciarPartidaAsync(ana.ContaId);

        var resultados = new List<Resultado>();
        for (var i = 0; i < 6 && servico.ObterVisao(ana.ContaId)!.Vez != servico.ObterVisao(ana.ContaId)!.MeuAssento; i++)
            resultados.Add(await servico.DecidirAsync(ana.ContaId, true));

        Assert.That(foraDaPartida.ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
        Assert.That(semSala.ChaveDoErro, Is.EqualTo(ChavesDeErro.NaoEstaEmSala));
        Assert.That(resultados, Is.Not.Empty);
        Assert.That(resultados.Select(r => r.ChaveDoErro), Has.All.EqualTo(ChavesDeErro.NaoEhSuaVez));
    }

    [Test]
    public async Task Deve_EnviarEmotes_ComLimiteDeFrequencia()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);

        var primeiro = await servico.EnviarEmoteAsync(ana.ContaId, 1);
        var rapido = await servico.EnviarEmoteAsync(ana.ContaId, 2);
        _relogio.Avancar(TimeSpan.FromSeconds(3));
        var depois = await servico.EnviarEmoteAsync(ana.ContaId, 2);
        var invalido = await servico.EnviarEmoteAsync(ana.ContaId, 9);
        var semSala = await servico.EnviarEmoteAsync(Guid.NewGuid(), 1);

        Assert.That(primeiro.Sucesso, Is.True);
        Assert.That(rapido.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmoteMuitoRapido));
        Assert.That(depois.Sucesso, Is.True);
        Assert.That(invalido.ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
        Assert.That(semSala.ChaveDoErro, Is.EqualTo(ChavesDeErro.NaoEstaEmSala));
        Assert.That(_notificador.Emotes.Where(e => e.Conta == bia.ContaId).Select(e => (e.Assento, e.Emote)),
            Is.EqualTo(new[] { (0, 1), (0, 2) }));
    }

    [Test]
    public async Task Deve_EnviarEmoteTambemDuranteAPartida_ESeguirMesmoSeONotificadorFalhar()
    {
        var (servico, ana, _) = await PartidaComBotsAsync();
        _notificador.Falhar = true;

        var resultado = await servico.EnviarEmoteAsync(ana.ContaId, 0);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(servico.UltimoErroDe(servico.CodigoDaSalaDe(ana.ContaId)!), Is.Not.Null);
    }

    [Test]
    public async Task Deve_MostrarSoAsCartasDoProprioJogador()
    {
        var (servico, ana, _) = await PartidaComBotsAsync();

        var visao = servico.ObterVisao(ana.ContaId)!;

        Assert.That(visao.Fase, Is.EqualTo("partida"));
        Assert.That(visao.MinhaMao, Has.Count.EqualTo(3));
        Assert.That(visao.Assentos, Has.Count.EqualTo(3));
        Assert.That(visao.Baus, Has.Count.EqualTo(2));
        Assert.That(visao.Baus.All(b => b.Dono is null), Is.True);
        Assert.That(visao.Montante, Is.EqualTo(3));
        Assert.That(visao.Mania, Is.Not.Null);
        Assert.That(visao.Diario, Is.Not.Empty);
    }

    // ---------- partidas completas ----------

    private enum Estrategia { SempreFica, PegaBaus, Ausente, FicaENaoJoga }

    private async Task<(string Codigo, Guid Conta)> JogarAteOFimAsync(
        int semente, Estrategia estrategia, int bots = 2, ISorteador? sorteador = null)
    {
        var servico = Novo(sorteador ?? new SorteadorComSemente(semente));
        _servicoAtual = servico;
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        await servico.PreencherComBotsAsync(ana.ContaId, bots);
        await servico.IniciarPartidaAsync(ana.ContaId);

        for (var passo = 0; passo < 20000; passo++)
        {
            var visao = servico.ObterVisao(ana.ContaId)!;
            if (visao.Fase == "encerrada")
                return (codigo, ana.ContaId);

            var minhaVez = visao.Vez is not null && visao.Vez == visao.MeuAssento;
            var ficaAusente = estrategia == Estrategia.Ausente
                || (estrategia == Estrategia.FicaENaoJoga && visao.Etapa == "rodadas");
            if (minhaVez && !ficaAusente)
            {
                await AgirAsync(servico, ana.ContaId, visao, estrategia);
                continue;
            }
            _relogio.Avancar(TimeSpan.FromSeconds(visao.PrazoFimUtc is null ? 3 : 46));
            await EsperarAgendadaAsync(servico, codigo);
        }
        Assert.Fail("a partida não terminou");
        return default;
    }

    private MesaServico? _servicoAtual;

    private static async Task AgirAsync(MesaServico servico, Guid conta, VisaoDaMesa visao, Estrategia estrategia)
    {
        if (visao.Etapa == "rodadas")
        {
            var carta = visao.MinhaMao[visao.Permitidas[0]];
            Assert.That((await servico.JogarCartaAsync(conta, carta.Naipe, carta.Valor)).Sucesso, Is.True);
            return;
        }

        var bau = visao.Baus.FirstOrDefault(b => b.Disponivel);
        if (estrategia == Estrategia.PegaBaus && bau is not null)
        {
            Assert.That((await servico.PegarBauAsync(conta, bau.Numero)).Sucesso, Is.True);
            return;
        }
        var ficar = visao.Decisao!.PodeFicar;
        Assert.That((await servico.DecidirAsync(conta, ficar)).Sucesso, Is.True);
    }

    [Test]
    public async Task Deve_JogarUmaPartidaCompleta_GravandoHistoricoEClassificacao([Range(1, 12)] int semente)
    {
        var (codigo, conta) = await JogarAteOFimAsync(semente, Estrategia.SempreFica);

        var partida = _repositorio.Todos<Partida>().Single();
        Assert.That(partida.Situacao, Is.EqualTo(SituacaoDaPartida.Encerrada));
        Assert.That(partida.EncerradaEm, Is.Not.Null);
        var maos = _repositorio.Todos<Mao>().ToList();
        Assert.That(maos, Is.Not.Empty);
        Assert.That(maos.Select(m => m.Numero), Is.EqualTo(Enumerable.Range(1, maos.Count)));
        var linhas = _repositorio.Todos<PartidaJogador>().ToList();
        Assert.That(linhas.Select(l => l.Colocacao), Is.EquivalentTo(new int?[] { 1, 2, 3 }));
        Assert.That(linhas.Sum(l => l.FichasFinais), Is.LessThanOrEqualTo(60));
        Assert.That(linhas.All(l => l.FichasFinais >= 0), Is.True);
        Assert.That(_repositorio.Todos<Bau>().Count(), Is.EqualTo(maos.Count * 2));
        Assert.That(_repositorio.Todos<MovimentoDeFicha>().Any(), Is.True);

        var visao = _servicoAtual!.ObterVisao(conta)!;
        Assert.That(visao.Fase, Is.EqualTo("encerrada"));
        Assert.That(visao.Diario.Last().Chave, Is.EqualTo("Partida_DiarioFim"));
        Assert.That(await _servicoAtual.SairAsync(conta), Is.EqualTo(Resultado.Ok));
        Assert.That(_servicoAtual.ListarSalasPublicas(), Is.Empty);
        Assert.That(codigo, Has.Length.EqualTo(6));
    }

    [Test]
    public async Task Deve_FazerOsBotsReagirComEmotes_AoFimDasRodadas()
    {
        await JogarAteOFimAsync(1, Estrategia.SempreFica, bots: 3);
        await Task.Delay(100);

        var emotes = _notificador.Emotes.ToList();
        Assert.That(emotes, Is.Not.Empty);
        Assert.That(emotes.Select(e => e.Emote), Is.All.InRange(0, 4));
        Assert.That(emotes.Select(e => e.Assento), Is.All.InRange(0, 3));
        Assert.That(emotes.Select(e => e.Emote).Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public async Task Deve_JogarComBaus_ERegistrarTaxaEPosse([Range(1, 12)] int semente)
    {
        await JogarAteOFimAsync(semente, Estrategia.PegaBaus, bots: 3);

        var baus = _repositorio.Todos<Bau>().ToList();
        Assert.That(baus.Any(b => b.PegoPorAssento is not null), Is.True);
        Assert.That(_repositorio.Todos<Mao>().Count(), Is.GreaterThan(0));
    }

    [Test]
    public async Task Deve_EliminarPorInatividade_QuemNuncaAge([Range(1, 12)] int semente)
    {
        await JogarAteOFimAsync(semente, Estrategia.Ausente);

        var partida = _repositorio.Todos<Partida>().Single();
        Assert.That(partida.Situacao, Is.EqualTo(SituacaoDaPartida.Encerrada));
        Assert.That(_repositorio.Todos<MaoJogador>().Any(m => m.Decisao.ToString() == "Ausente"), Is.True);
    }

    [Test]
    public async Task Deve_EliminarPorInatividade_QuemFicaMasNuncaJoga([Range(1, 6)] int semente)
    {
        await JogarAteOFimAsync(semente, Estrategia.FicaENaoJoga);

        var linhas = _repositorio.Todos<PartidaJogador>().ToList();
        Assert.That(_repositorio.Todos<Jogada>().Any(j => j.FoiAutomatica && j.FoiVirada), Is.True);
        Assert.That(linhas.Select(l => l.MotivoDaSaida?.ToString()), Does.Contain("Inatividade"));
    }

    [Test]
    public async Task Deve_RecusarBauInexistente_NaVezDoJogador()
    {
        var (servico, ana, codigo) = await PartidaComBotsAsync();
        for (var i = 0; i < 60; i++)
        {
            var visao = servico.ObterVisao(ana.ContaId)!;
            if (visao.Vez == visao.MeuAssento)
                break;
            _relogio.Avancar(TimeSpan.FromSeconds(3));
            await EsperarAgendadaAsync(servico, codigo);
        }

        var resultado = await servico.PegarBauAsync(ana.ContaId, 7);

        Assert.That(resultado.ChaveDoErro, Is.EqualTo(ChavesDeErro.BauInexistente));
    }

    [Test]
    public async Task Deve_PermitirSairDepoisDeEliminado_ERegistrarSaidaDosOutros()
    {
        var servico = Novo(new SorteadorComSemente(4));
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        await servico.PreencherComBotsAsync(ana.ContaId, 4);
        await servico.IniciarPartidaAsync(ana.ContaId);

        for (var i = 0; i < 4000; i++)
        {
            var visao = servico.ObterVisao(ana.ContaId)!;
            if (visao.Fase == "encerrada" || !visao.Assentos[visao.MeuAssento].EstaNaPartida)
                break;
            var minhaVez = visao.Vez is not null && visao.Vez == visao.MeuAssento;
            if (minhaVez && visao.Etapa == "decisao")
            {
                await servico.DecidirAsync(ana.ContaId, visao.Decisao!.PodeFicar);
                continue;
            }
            _relogio.Avancar(TimeSpan.FromSeconds(visao.PrazoFimUtc is null ? 3 : 46));
            await EsperarAgendadaAsync(servico, codigo);
        }

        var depois = servico.ObterVisao(ana.ContaId)!;
        Assert.That(depois.Assentos[depois.MeuAssento].EstaNaPartida, Is.False);
        Assert.That(depois.Fase, Is.EqualTo("partida"));
        Assert.That((await servico.SairAsync(ana.ContaId)).Sucesso, Is.True);
        Assert.That(servico.ObterVisao(ana.ContaId), Is.Null);
    }

    [Test]
    public async Task Deve_RecusarDesistenciaDoProximoDistribuidor()
    {
        var chaves = new List<string?>();
        for (var semente = 1; semente <= 30; semente++)
        {
            Preparar();
            var (servico, ana, _) = await PartidaComBotsAsync(sorteador: new SorteadorComSemente(semente));
            var resultado = await servico.SairAsync(ana.ContaId);
            chaves.Add(resultado.ChaveDoErro);
        }

        Assert.That(chaves, Does.Contain(ChavesDeErro.ProximoDistribuidorNaoPodeSair));
        Assert.That(chaves, Does.Contain((string?)null));
    }

    [Test]
    public async Task Deve_DesistirDuranteAPartida_EInterromperQuandoNaoSobraHumano()
    {
        var (servico, ana, codigo) = await PartidaComBotsAsync();
        Resultado saida = default;
        for (var i = 0; i < 60; i++)
        {
            saida = await servico.SairAsync(ana.ContaId);
            if (saida.Sucesso)
                break;
            Assert.That(saida.ChaveDoErro, Is.EqualTo(ChavesDeErro.ProximoDistribuidorNaoPodeSair));
            _relogio.Avancar(TimeSpan.FromSeconds(21));
            await EsperarAgendadaAsync(servico, codigo);
        }

        Assert.That(saida.Sucesso, Is.True);
        Assert.That(servico.ObterVisao(ana.ContaId), Is.Null);
        Assert.That(_repositorio.Todos<Partida>().Single().Situacao, Is.EqualTo(SituacaoDaPartida.Interrompida));
        Assert.That(servico.ListarSalasPublicas(), Is.Empty);
    }

    [Test]
    public async Task Deve_SeguirComOutroHumano_QuandoOCriadorDesisteNoMeioDaPartida()
    {
        var servico = Novo();
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        var bia = Perfil("Bia");
        await servico.EntrarAsync(bia, codigo);
        await servico.PreencherComBotsAsync(ana.ContaId, 1);
        await servico.IniciarPartidaAsync(ana.ContaId);

        Resultado saida = default;
        for (var i = 0; i < 60; i++)
        {
            saida = await servico.SairAsync(ana.ContaId);
            if (saida.Sucesso)
                break;
            _relogio.Avancar(TimeSpan.FromSeconds(21));
            await EsperarAgendadaAsync(servico, codigo);
        }

        Assert.That(saida.Sucesso, Is.True);
        Assert.That(servico.ObterVisao(bia.ContaId)!.SouCriador, Is.True);
        Assert.That(servico.ListarSalasPublicas(), Has.Count.EqualTo(1));
        Assert.That(_repositorio.Todos<Partida>().Single().Situacao, Is.EqualTo(SituacaoDaPartida.EmAndamento));

        var saiuDoDiario = false;
        for (var i = 0; i < 400 && !saiuDoDiario; i++)
        {
            var visao = servico.ObterVisao(bia.ContaId)!;
            saiuDoDiario = visao.Diario.Any(e => e.Chave == "Partida_DiarioSaiu");
            var minhaVez = visao.Vez is not null && visao.Vez == visao.MeuAssento;
            if (minhaVez && visao.Etapa == "decisao")
            {
                await servico.DecidirAsync(bia.ContaId, false);
                continue;
            }
            _relogio.Avancar(TimeSpan.FromSeconds(visao.PrazoFimUtc is null ? 3 : 46));
            await EsperarAgendadaAsync(servico, codigo);
        }
        Assert.That(saiuDoDiario, Is.True);
        Assert.That(_repositorio.Todos<Partida>().Single().Situacao, Is.EqualTo(SituacaoDaPartida.Encerrada));
    }

    [Test]
    public async Task Deve_GuardarOErro_QuandoOBancoFalhaDentroDeUmaAcaoAgendada()
    {
        var servico = Novo(new SorteadorComSemente(3));
        var ana = Perfil("Ana");
        var codigo = await SalaComAsync(servico, ana);
        await servico.PreencherComBotsAsync(ana.ContaId, 2);
        await servico.IniciarPartidaAsync(ana.ContaId);
        _trabalho.ChamadasAteFalhar = _trabalho.Chamadas;

        for (var i = 0; i < 400 && servico.UltimoErroDe(codigo) is null; i++)
        {
            _relogio.Avancar(TimeSpan.FromSeconds(21));
            await EsperarAgendadaAsync(servico, codigo);
        }

        Assert.That(servico.UltimoErroDe(codigo), Is.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Deve_IgnorarTarefaAgendadaDeSalaDesconhecida()
    {
        var servico = Novo();

        await servico.TarefaAgendadaDe("NAOEXISTE");

        Assert.That(servico.UltimoErroDe("NAOEXISTE"), Is.Null);
    }
}
