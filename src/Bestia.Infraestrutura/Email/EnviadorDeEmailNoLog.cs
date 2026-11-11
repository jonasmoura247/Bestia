using System.Collections.Concurrent;
using Bestia.Aplicacao.Contratos;
using Microsoft.Extensions.Logging;

namespace Bestia.Infraestrutura.Email;

public sealed record MensagemDeEmail(DateTime Em, string Para, string Assunto, string Corpo);

/// <summary>Só para desenvolvimento: guarda os últimos e-mails "enviados" para a página /dev/emails.</summary>
public sealed class CaixaDeEmails
{
    private const int Capacidade = 50;
    private readonly ConcurrentQueue<MensagemDeEmail> _mensagens = new();

    public void Adicionar(MensagemDeEmail mensagem)
    {
        _mensagens.Enqueue(mensagem);
        while (_mensagens.Count > Capacidade)
            _mensagens.TryDequeue(out _);
    }

    /// <summary>Do mais novo para o mais antigo.</summary>
    public IReadOnlyList<MensagemDeEmail> Listar() => _mensagens.Reverse().ToList();
}

/// <summary>Só para desenvolvimento: em vez de enviar, escreve o e-mail no log e na caixa de desenvolvimento.</summary>
public sealed class EnviadorDeEmailNoLog(ILogger<EnviadorDeEmailNoLog> registro, CaixaDeEmails caixa) : IEnviadorDeEmail
{
    public Task EnviarAsync(string para, string assunto, string corpo, CancellationToken ct = default)
    {
        registro.LogInformation("E-mail (desenvolvimento) para {Para} | {Assunto} | {Corpo}", para, assunto, corpo);
        caixa.Adicionar(new MensagemDeEmail(DateTime.UtcNow, para, assunto, corpo));
        return Task.CompletedTask;
    }
}
