# Bestia

Jogo de cartas multijogador online em **ASP.NET Core MVC (.NET 8)**, para 3 a 10 pessoas e bots, com mesa em tempo real (SignalR), três idiomas (pt-BR, en, es) e conformidade com a LGPD.

Projeto de portfólio: a ideia é mostrar uma aplicação completa, com regras de negócio testadas, segurança e privacidade tratadas como requisito, e não só uma tela bonita. Não há dinheiro real: as fichas são pontos do jogo, sem valor monetário.

> **Estado:** o motor do jogo, as contas, as salas, a mesa em tempo real e a LGPD estão feitos e testados. Ainda **não está publicado**: faltam o login real com Google, o envio real de e-mail e a revisão jurídica dos textos. O quadro completo está em [`docs/STATUS.md`](docs/STATUS.md).

## O jogo

- 3 a 10 jogadores, 20 fichas cada, mãos de 3 cartas e 3 rodadas, com um baralho de 48 cartas.
- A carta virada na mesa define a **mania** da mão: o naipe dela vence os outros, mesmo com valor menor.
- Só o distribuidor aposta (3 fichas); cada rodada vale o montante dividido por 3. Os outros **ficam**, **correm** ou **pegam um baú** (troca a mão por 3 cartas viradas para baixo).
- Há 20 segundos para decidir e 45 para jogar; passou do tempo, o jogo joga pela pessoa.
- Não há vencedor final: a partida segue até sobrarem menos de 3 jogadores e o ranking é por fichas.

As regras completas ficam na tela "Regras do jogo", que também abre durante a partida.

## O que tem

- **Mesa em tempo real** com SignalR: prazos controlados no servidor, cada jogador recebe só a visão da própria mão, reconexão de até 2 minutos e segunda aba derrubando a primeira.
- **Bots** com estratégia própria, usados para completar mesas e na simulação de 1.000 partidas dos testes.
- **Salas** públicas e privadas (por código), com sala de espera.
- **Contas:** cadastro com confirmação por e-mail, login com bloqueio por tentativas, recuperação de senha e login com Google.
- **Perfil** com estatísticas e histórico de partidas.
- **Três idiomas** (pt-BR, en, es), com todos os textos em um catálogo único e um teste que falha se faltar tradução ou sobrar texto solto na tela.
- **LGPD:** consentimento registrado, "meus dados" com download em JSON, alteração e revogação, exclusão de conta com anonimização, pedidos de titulares e rotina diária de retenção.

## Segurança

- Dados pessoais criptografados em repouso com **AES-256-GCM**; busca por e-mail por **índice cego**, sem guardar o e-mail em claro; rotação de chave.
- Senhas com `PasswordHasher` do ASP.NET Core Identity, bloqueio de conta e invalidação de sessões ao trocar a senha.
- Antiforgery, cookies seguros, cabeçalhos de segurança, HSTS e limite de requisições por IP.
- Chaves e segredos só por variável de ambiente ou cofre; nada disso vai para o repositório.
- Imagem Docker e health check em `/saude`.

## Arquitetura

Camadas, com o domínio sem nenhuma dependência de infraestrutura:

```
src/
  Bestia.Dominio          regras do jogo: Baralho, Carta, Mao, Rodada, Montante, Baú, Mesa. Sem I/O.
  Bestia.Aplicacao        casos de uso (contas, privacidade, salas, mesa ao vivo) e portas
  Bestia.Infraestrutura   EF Core + SQL Server, criptografia, e-mail, rotinas
  Bestia.Recursos         catálogo de textos (textos-mestre.json) e paridade de idiomas
  Bestia.Web              MVC, SignalR (MesaHub), páginas e JavaScript do navegador
tests/
  Bestia.Testes.Dominio   regras do jogo e simulação de 1.000 partidas
  Bestia.Testes.Aplicacao contas, privacidade, salas, mesa (relógio simulado) e carga de 50 mesas
  Bestia.Testes.Integracao  criptografia, EF Core, migrações, partida completa e auditoria de licenças
  Bestia.Testes.Web       páginas, cookies, antiforgery, SignalR, LGPD e paridade de idiomas
```

Decisões que valem destaque:

- **Mesa em memória, histórico no banco:** a mão em curso vive em memória (rápido e simples); o banco guarda contas, salas, partidas concluídas e movimentos de fichas.
- **Relógio injetável:** os prazos de 20 s e 45 s são testados com relógio simulado, sem `Thread.Sleep`.
- **Sem número ou texto mágico:** os valores do jogo são constantes nomeadas.
- **Só licença MIT:** a regra do projeto é só aceitar pacote MIT, e uma auditoria roda como teste sobre todas as dependências (a exceção aceita está em [`LICENCAS.md`](LICENCAS.md)).

O plano técnico completo está em [`Bestia — Plano e Especificação Técnica (C# MVC).md`](Bestia%20%E2%80%94%20Plano%20e%20Especifica%C3%A7%C3%A3o%20T%C3%A9cnica%20(C%23%20MVC).md).

## Como rodar

Precisa do .NET 8 SDK e do Docker (para o SQL Server de desenvolvimento).

```powershell
# 1. SQL Server de desenvolvimento (escolha uma senha forte; ela não fica no repositório)
$senha = "<escolha-uma-senha-forte>"
docker run -d --name bestia-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$senha" -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest

# 2. Aplicação (aplica as migrações ao subir; os e-mails vão para o log, com o link de confirmação)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Bestia = "Server=localhost,14333;Database=Bestia;User Id=sa;Password=$senha;TrustServerCertificate=True;Encrypt=True"
dotnet run --project src/Bestia.Web --urls http://localhost:5099
```

A senha é só a do contêiner local de desenvolvimento e vem da variável de ambiente `ConnectionStrings__Bestia`, a mesma que os testes de Web usam. Em desenvolvimento, sem chaves configuradas, a aplicação usa chaves fixas de **desenvolvimento**, nunca usadas em produção (fora do ambiente Development ela recusa subir sem as chaves reais). A configuração de produção, o deploy e as rotinas diárias estão em [`docs/OPERACAO.md`](docs/OPERACAO.md).

## Testes

```powershell
dotnet test
```

O build falha abaixo de **100% de linhas e ramos** nos módulos de código (Domínio, Aplicação, Recursos, Infraestrutura e Web), com exclusões justificadas em [`docs/STATUS.md`](docs/STATUS.md). Os testes de Integração e de Web precisam do SQL Server do passo 1 rodando. O JavaScript do navegador (`mesa.js`, `lobby.js`, `site.js`) não tem teste automatizado: foi checado com `node --check` e exercitado no navegador por roteiro.

O CI (`.github/workflows/ci.yml`) restaura, confere a formatação, compila com avisos como erro, roda os testes com cobertura e procura pacotes vulneráveis.

## Limitações conhecidas

- As mesas vivem em memória: se o servidor cair, a mão em curso se perde e a partida fica como `Interrompida`.
- Roda em **uma instância só**. Para escalar seria preciso um backplane Redis do SignalR e mesa fixa por servidor.
- Os Termos e a Política de privacidade são textos de referência e precisam de revisão jurídica antes de abrir ao público.
- A carga testada (50 mesas de 6 jogadores) é em memória, com relógio simulado; não substitui um teste com rede e navegadores reais.

## Documentação

| Arquivo | Conteúdo |
| --- | --- |
| [`docs/STATUS.md`](docs/STATUS.md) | fases do plano, qualidade, desvios e pendências |
| [`docs/OPERACAO.md`](docs/OPERACAO.md) | rodar, configurar, publicar, rotinas e backups |
| [`LICENCAS.md`](LICENCAS.md) | licenças das dependências |
| [`bestia-prototipo.html`](bestia-prototipo.html) | protótipo visual da mesa |
