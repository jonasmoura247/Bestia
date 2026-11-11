# Bestia — operação

Guia rápido para rodar, configurar e publicar o site. O plano técnico está em `Bestia — Plano e Especificação Técnica (C# MVC).md` (raiz do projeto);
o que foi feito e o que falta está em `STATUS.md`.

## Rodar em desenvolvimento

```powershell
# 1. SQL Server de desenvolvimento
docker run -d --name bestia-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=Bestia#2026Dev!" -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest

# 2. Aplicação (aplica as migrações ao subir; e-mails vão para o log, com o link de confirmação)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Bestia = "Server=localhost,14333;Database=Bestia;User Id=sa;Password=Bestia#2026Dev!;TrustServerCertificate=True;Encrypt=True"
$env:Bestia__MigrarAoIniciar = "true"
dotnet run --project src/Bestia.Web --urls http://localhost:5099
```

Em desenvolvimento, sem chaves configuradas, a aplicação usa chaves fixas de **desenvolvimento** (nunca em produção).

## Testes

```powershell
dotnet test            # 5 projetos; falha abaixo de 100% de linhas e ramos
```

| Projeto | O que cobre | Precisa de Docker |
| --- | --- | --- |
| `Bestia.Testes.Dominio` | regras do jogo, simulação de 1.000 partidas com bots | não |
| `Bestia.Testes.Aplicacao` | contas, privacidade, salas e mesa ao vivo (relógio simulado), textos, carga de 50 mesas | não |
| `Bestia.Testes.Integracao` | criptografia, EF Core, migrações, partida completa, licenças | **sim** (SQL Server em contêiner) |
| `Bestia.Testes.Web` | páginas, cookies, anti-falsificação, SignalR, LGPD, paridade de idiomas | **sim** (o SQL Server do passo 1 de "Rodar em desenvolvimento") |

## Configuração (produção)

Tudo por variável de ambiente ou cofre de segredos; nada disso vai para o repositório.

| Chave | Obrigatória | Observação |
| --- | --- | --- |
| `ConnectionStrings__Bestia` | sim | `Encrypt=True`; usuário de mínimo privilégio |
| `Seguranca__ChaveMestra` | sim | 32 bytes em base64; guardar uma cópia em cofre separado |
| `Seguranca__ChaveDeIndice` | sim | 32 bytes em base64; **diferente** da chave mestra; trocá-la exige recalcular os índices |
| `Google__ClientId`, `Google__ClientSecret` | não | sem eles o botão do Google some |
| `Email__Servidor`, `Porta`, `Usuario`, `Senha`, `Remetente` | sim | SMTP de qualquer provedor |
| `DataProtection__Pasta` | recomendado | volume persistente; sem ele cada deploy desloga todo mundo |
| `Privacidade__EncarregadoEmail` | sim | aparece na Política de privacidade |
| `Bestia__ExigirHttps` | padrão `true` fora de Development | HSTS e redirecionamento |

Gerar uma chave: `[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])`
(ou `openssl rand -base64 32`).

## Publicar

```bash
docker build -t bestia-web .
docker run -d -p 8080:8080 -v bestia-chaves:/chaves --env-file producao.env bestia-web
```

- Colocar um proxy reverso na frente (HTTPS e WebSockets).
- Migrações: gerar o script idempotente e aplicá-lo em etapa própria
  (`dotnet ef migrations script --idempotent --project src/Bestia.Infraestrutura`); a aplicação **não** migra
  sozinha em produção.
- Saúde: `GET /saude` (200 quando o banco responde).
- As mesas vivem em memória: avisar os jogadores antes de um deploy ou esperar as partidas terminarem.
  Se o servidor cair, a mão em curso se perde (a partida fica `Interrompida` no banco).
- Uma instância só na v1. Para escalar: backplane Redis do SignalR e mesa fixa por servidor.

## Rotinas automáticas (uma vez por dia)

- apaga registros de acesso com mais de 6 meses;
- apaga tokens vencidos;
- contas sem acesso há 24 meses: aviso por e-mail e, 30 dias depois, anonimização.

## Backups e LGPD

- Backup diário criptografado, retenção de 30 dias, teste de restauração mensal (responsabilidade da hospedagem).
- Ao excluir uma conta, os dados somem dos backups quando o ciclo de 30 dias vence; a Política de privacidade
  precisa dizer isso.
- Pedidos de titulares ficam em `SolicitacaoPrivacidade` com prazo de 15 dias.
- Incidente de segurança: plano escrito com responsáveis, contenção e comunicação à ANPD e aos titulares no
  prazo regulatório vigente (confirmar com o jurídico).

## Guia de testes manuais (a cada release)

Mesa em celular, tablet e desktop; animações (fichas voando, baús, carta virada, emotes); leitura por
teclado e leitor de tela; troca de idioma durante a partida; queda e reconexão (até 2 minutos); segunda aba
derrubando a primeira.
