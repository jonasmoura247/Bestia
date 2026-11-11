using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;

// Modelos de persistência: uma classe por tabela, sem regra de negócio. Campos `...Cifrado` e `...Hash`
// são os bytes gerados por ICriptografia; nada pessoal fica em texto aberto.
namespace Bestia.Aplicacao.Modelos;

public enum TipoDeToken : byte { ConfirmacaoDeEmail, RedefinicaoDeSenha }

public enum TipoDeSolicitacao : byte { Acesso, Portabilidade, Correcao, Eliminacao, Revogacao }

public enum SituacaoDaSolicitacao : byte { Aberta, Respondida }

public enum TipoDeAcesso : byte { Login, Cadastro, RedefinicaoDeSenha }

public enum SituacaoDaSala : byte { Aberta, EmPartida, Encerrada }

public enum SituacaoDaPartida : byte { EmAndamento, Encerrada, Interrompida }

public class Jogador
{
    public Guid Id { get; set; }
    public byte[]? ApelidoCifrado { get; set; }
    public byte[]? ApelidoHash { get; set; }
    public byte[]? EmailCifrado { get; set; }
    public byte[]? EmailHash { get; set; }
    public string? SenhaHash { get; set; }
    public DateTime? EmailConfirmadoEm { get; set; }
    public string Idioma { get; set; } = "pt-BR";
    public string CorDoAvatar { get; set; } = "";
    public int FalhasDeLogin { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
    public DateTime? RemovidoEm { get; set; }
    public Guid CarimboDeSeguranca { get; set; } = Guid.NewGuid();
    public byte[] Versao { get; set; } = [];
}

public class LoginExterno
{
    public Guid Id { get; set; }
    public Guid JogadorId { get; set; }
    public string Provedor { get; set; } = "";
    public byte[] IdExternoHash { get; set; } = [];
    public DateTime CriadoEm { get; set; }
}

public class TokenDeConta
{
    public Guid Id { get; set; }
    public Guid JogadorId { get; set; }
    public TipoDeToken Tipo { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadoEm { get; set; }
}

public class Consentimento
{
    public Guid Id { get; set; }
    public Guid JogadorId { get; set; }
    public string Documento { get; set; } = "";
    public string Versao { get; set; } = "";
    public DateTime AceitoEm { get; set; }
    public byte[]? IpHash { get; set; }
    public DateTime? RevogadoEm { get; set; }
}

public class SolicitacaoPrivacidade
{
    public Guid Id { get; set; }
    public Guid JogadorId { get; set; }
    public TipoDeSolicitacao Tipo { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime PrazoEm { get; set; }
    public DateTime? RespondidaEm { get; set; }
    public SituacaoDaSolicitacao Situacao { get; set; }
}

public class RegistroDeAcesso
{
    public Guid Id { get; set; }
    public Guid JogadorId { get; set; }
    public byte[] IpCifrado { get; set; } = [];
    public TipoDeAcesso Tipo { get; set; }
    public DateTime OcorreuEm { get; set; }
}

public class RegistroDeAuditoria
{
    public Guid Id { get; set; }
    public Guid? JogadorId { get; set; }
    public string Acao { get; set; } = "";
    public DateTime OcorreuEm { get; set; }
    public string? Detalhe { get; set; }
}

public class Sala
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public Guid CriadaPorJogadorId { get; set; }
    public int MaximoDeJogadores { get; set; }
    public bool EhPrivada { get; set; }
    public SituacaoDaSala Situacao { get; set; }
    public DateTime CriadaEm { get; set; }
    public byte[] Versao { get; set; } = [];
}

public class SalaJogador
{
    public Guid SalaId { get; set; }
    public Guid JogadorId { get; set; }
    public DateTime EntrouEm { get; set; }
    public DateTime? SaiuEm { get; set; }
}

public class Partida
{
    public Guid Id { get; set; }
    public Guid SalaId { get; set; }
    public DateTime IniciadaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public int QuantidadeDeJogadores { get; set; }
    public Guid? VencedorJogadorId { get; set; }
    public SituacaoDaPartida Situacao { get; set; }
}

public class PartidaJogador
{
    public Guid PartidaId { get; set; }
    public int Assento { get; set; }
    public Guid? JogadorId { get; set; }
    public string? NomeDoBot { get; set; }
    public int FichasIniciais { get; set; }
    public int FichasFinais { get; set; }
    public int? Colocacao { get; set; }
    public MotivoDaSaida? MotivoDaSaida { get; set; }
}

public class Mao
{
    public Guid Id { get; set; }
    public Guid PartidaId { get; set; }
    public int Numero { get; set; }
    public int AssentoDoDistribuidor { get; set; }
    public Naipe Mania { get; set; }
    public int Montante { get; set; }
    public int TaxaDeBau { get; set; }
    public DesfechoDaMao Desfecho { get; set; }
}

public class MaoJogador
{
    public Guid MaoId { get; set; }
    public int Assento { get; set; }
    public DecisaoNaMao Decisao { get; set; }
    public int BausPegos { get; set; }
    public int RodadasVencidas { get; set; }
    public int ReposicaoPaga { get; set; }
}

public class Bau
{
    public Guid Id { get; set; }
    public Guid MaoId { get; set; }
    public int Numero { get; set; }
    public int? PegoPorAssento { get; set; }
    public int TaxaPaga { get; set; }
}

public class Rodada
{
    public Guid Id { get; set; }
    public Guid MaoId { get; set; }
    public int Numero { get; set; }
    public int AbridorAssento { get; set; }
    public int VencedorAssento { get; set; }
    public int Valor { get; set; }
}

public class Jogada
{
    public Guid Id { get; set; }
    public Guid RodadaId { get; set; }
    public int Assento { get; set; }
    public int Ordem { get; set; }
    public Naipe Naipe { get; set; }
    public Valor Valor { get; set; }
    public bool FoiAutomatica { get; set; }
    public bool FoiVirada { get; set; }
}

public class MovimentoDeFicha
{
    public Guid Id { get; set; }
    public Guid PartidaId { get; set; }
    public Guid? MaoId { get; set; }
    public int Assento { get; set; }
    public TipoDeMovimento Tipo { get; set; }
    public int Valor { get; set; }
    public DateTime OcorreuEm { get; set; }
}
