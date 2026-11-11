# Licenças das dependências

Regra do projeto: só entra pacote de código com licença **MIT**. A auditoria roda como teste
(`LicencasTestes`, projeto `Bestia.Testes.Integracao`) sobre todos os pacotes de produção, diretos e
transitivos, e falha o build se aparecer uma licença fora da lista abaixo.

## Pacotes diretos

| Pacote | Versão | Licença | Onde |
| --- | --- | --- | --- |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | MIT | Infraestrutura |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | MIT | Infraestrutura (só ferramentas) |
| Microsoft.Extensions.Identity.Core | 8.0.11 | MIT | Infraestrutura (PasswordHasher) |
| Microsoft.AspNetCore.Authentication.Google | 8.0.11 | MIT | Web |
| @microsoft/signalr (signalr.min.js) | 8.0.7 | MIT | Web (`wwwroot/lib/signalr`, com a licença ao lado) |
| NUnit / NUnit3TestAdapter / Microsoft.NET.Test.Sdk | 4.2.2 / 4.6.0 / 17.11.1 | MIT | Testes |
| coverlet.msbuild | 6.0.2 | MIT | Testes |
| Microsoft.AspNetCore.Mvc.Testing / SignalR.Client | 8.0.11 | MIT | Testes |
| Testcontainers.MsSql | 3.10.0 | MIT | Testes |

Conferência na época da escrita: `license type="expression"` = MIT no `.nuspec` de cada um. Reconferir a
cada atualização de versão.

## Transitivos sem expressão SPDX

Conferidos na origem (licenseUrl aponta para o `LICENSE.TXT` MIT do dotnet/corefx):
Microsoft.CSharp, System.CodeDom, System.Memory, System.Numerics.Vectors, System.Threading.Tasks.Extensions.

## Exceções documentadas (licença da Microsoft, não MIT)

Pacotes de runtime que chegam como dependência transitiva do driver do SQL Server e que declaram a
"Microsoft .NET Library License" ou os "Microsoft Software License Terms" (permitem uso e redistribuição,
mas **não são MIT**). **Decisão (09/10/2026): exceção aceita**, junto com o próprio SQL Server (edição Express,
gratuita, até 10 GB por banco). Alternativa avaliada e descartada por ora: MariaDB (MySqlConnector + Pomelo, MIT).

| Pacote | Licença declarada |
| --- | --- |
| Microsoft.Data.SqlClient.SNI.runtime | Microsoft Software License Terms (biblioteca nativa de rede do SQL Server) |
| Microsoft.NETCore.Platforms | Microsoft .NET Library License |
| Microsoft.NETCore.Targets | Microsoft .NET Library License |
| System.Runtime (4.3.0) | Microsoft .NET Library License |
| System.Text.Encoding (4.3.0) | Microsoft .NET Library License |

## Fora do código da solução

SQL Server (edições Express/Developer), Docker, serviço de login do Google, provedor de hospedagem e SMTP são
infraestrutura contratada ou instalada, não dependências de código.
