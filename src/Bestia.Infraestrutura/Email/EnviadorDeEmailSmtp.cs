using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mail;
using Bestia.Aplicacao.Contratos;

namespace Bestia.Infraestrutura.Email;

public sealed class OpcoesDeEmail
{
    public string Servidor { get; set; } = "";
    public int Porta { get; set; } = 587;
    public string Usuario { get; set; } = "";
    public string Senha { get; set; } = "";
    public string Remetente { get; set; } = "";
}

/// <summary>Adaptador fino sobre o SmtpClient da BCL; sem lógica própria para testar.</summary>
[ExcludeFromCodeCoverage]
public sealed class EnviadorDeEmailSmtp(OpcoesDeEmail opcoes) : IEnviadorDeEmail
{
    public async Task EnviarAsync(string para, string assunto, string corpo, CancellationToken ct = default)
    {
        using var cliente = new SmtpClient(opcoes.Servidor, opcoes.Porta)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(opcoes.Usuario, opcoes.Senha),
        };
        using var mensagem = new MailMessage(opcoes.Remetente, para, assunto, corpo);
        await cliente.SendMailAsync(mensagem, ct);
    }
}
