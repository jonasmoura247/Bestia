using Bestia.Aplicacao.Contratos;
using Microsoft.AspNetCore.Identity;

namespace Bestia.Infraestrutura.Seguranca;

/// <summary>PBKDF2 com sal único, do PasswordHasher do ASP.NET Core (sem usar o Identity completo).</summary>
public sealed class HashDeSenhaIdentity : IHashDeSenha
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Gerar(string senha) => _hasher.HashPassword(null!, senha);

    public bool Conferir(string hash, string senha) =>
        _hasher.VerifyHashedPassword(null!, hash, senha) != PasswordVerificationResult.Failed;
}
