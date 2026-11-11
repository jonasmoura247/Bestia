using Bestia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.MsSql;

namespace Bestia.Testes.Integracao;

/// <summary>Sobe um SQL Server real em contêiner uma vez por execução e aplica as migrações.</summary>
[SetUpFixture]
public class BancoDeTestes
{
    private static MsSqlContainer _conteiner = null!;

    public static BestiaDbContext Novo() =>
        new(new DbContextOptionsBuilder<BestiaDbContext>().UseSqlServer(_conteiner.GetConnectionString()).Options);

    [OneTimeSetUp]
    public async Task Iniciar()
    {
        _conteiner = new MsSqlBuilder().Build();
        await _conteiner.StartAsync();
        await using var banco = Novo();
        await banco.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Parar() => await _conteiner.DisposeAsync();
}
