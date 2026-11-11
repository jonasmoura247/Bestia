# Bestia — Plano e Especificação Técnica (C# MVC)

Oct 9, 2026 · @Jonas

## 1. Visão geral e escopo

A v1 entrega o Bestia como site online em ASP.NET Core MVC: o jogador cria conta (ou entra com Google), escolhe uma sala e joga contra outras pessoas e bots, em português, inglês ou espanhol, com os dados pessoais protegidos conforme a LGPD.

**Objetivos da v1**

- Jogar uma partida completa de 3 a 10 jogadores, com todas as regras do `Bestia.md` (baús, timers de 20 s e 45 s, ausência, único que fica).
- Cadastro com e-mail e senha, ou login com conta Google.
- Menu lateral esquerdo com Iniciar partida, Salas, Meu perfil, Regras do jogo, Configurações e Sair.
- Tela de regras consultável a qualquer momento, inclusive durante a partida.
- Histórico das partidas gravado em SQL Server.
- Três idiomas (pt-BR, en, es) com todos os textos em resources.
- Conformidade com a LGPD: dados pessoais criptografados, consentimento registrado, exportação e exclusão da conta.

**Fora da v1 (ficam para depois)**

- Aplicativo mobile nativo, ranking global com temporadas, chat de texto livre, loja ou qualquer pagamento.
- Login com outros provedores além do Google.
- Mais de uma instância do servidor (a v1 roda em uma só; a escala horizontal está prevista na seção 10).

**Premissas**

- Não há dinheiro real: as fichas são pontos do jogo, sem valor monetário nem conversão.
- O protótipo `bestia-prototipo.html` é a referência visual e de comportamento; a lógica dele é reescrita em C#, não reaproveitada.
- Todo o código, nomes de classes, métodos, tabelas e colunas ficam em português do Brasil.

## 2. Stack e licenças (somente MIT)

A regra é: nenhum pacote de código entra na solução sem licença MIT confirmada no `.nuspec` ou no repositório. As licenças abaixo vêm do meu conhecimento até meados de 2026 e **devem ser reconferidas no dia em que cada pacote for adicionado**, porque projetos mudam de licença (já aconteceu com vários do ecossistema .NET).

**Pacotes permitidos**

| Necessidade | Escolha | Licença |
| --- | --- | --- |
| Plataforma e web | .NET 8 LTS (ou superior) com ASP.NET Core MVC | MIT |
| Acesso a dados | Entity Framework Core + `Microsoft.EntityFrameworkCore.SqlServer` | MIT |
| Driver SQL Server | `Microsoft.Data.SqlClient` | MIT |
| Tempo real | ASP.NET Core SignalR (servidor) e `@microsoft/signalr` (navegador) | MIT |
| Login com Google | `Microsoft.AspNetCore.Authentication.Google` | MIT |
| Sessão e cookies | `Microsoft.AspNetCore.Authentication.Cookies` | MIT |
| Hash de senha | `PasswordHasher` de `Microsoft.Extensions.Identity.Core` (PBKDF2) | MIT |
| Criptografia de dados | `System.Security.Cryptography` (AES-GCM, HMAC-SHA256, HKDF) da própria BCL | MIT |
| Proteção de chaves e cookies | `Microsoft.AspNetCore.DataProtection` | MIT |
| Textos e idiomas | `Microsoft.Extensions.Localization` e arquivos `.resx` | MIT |
| Log e configuração | `Microsoft.Extensions.Logging` e `IConfiguration` | MIT |
| Validação | `DataAnnotations` e `IValidatableObject` da BCL | MIT |
| E-mail (recuperação de senha) | `System.Net.Mail` via SMTP configurável | MIT |
| Testes | NUnit, NUnit3TestAdapter, `Microsoft.NET.Test.Sdk`, coverlet | MIT |
| Testes com banco real | Testcontainers para .NET | MIT |
| Interface | Bootstrap 5, Bootstrap Icons, jQuery (se necessário) | MIT |
| Cache distribuído (fase de escala) | `StackExchange.Redis` e `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | MIT |

**O que foi evitado de propósito**

| Pacote ou recurso | Motivo | Substituto |
| --- | --- | --- |
| xUnit | Apache 2.0 | NUnit |
| Moq e NSubstitute | BSD | Fakes escritos à mão (interfaces pequenas facilitam) |
| FluentAssertions | Apache 2.0 e licença comercial nas versões recentes | Asserts do NUnit (`Assert.That`) |
| AutoMapper e MediatR | Licença comercial nas versões recentes | Mapeamento manual e chamadas diretas de serviço |
| Serilog | Apache 2.0 | `Microsoft.Extensions.Logging` |
| Dapper | Apache 2.0 | EF Core (e SQL via `FromSql` quando preciso) |
| FluentValidation | Apache 2.0 | `DataAnnotations` |
| Playwright para .NET | Apache 2.0 | Testes de interface ficam manuais na v1 |
| Fontes Pirata One e Alegreya do protótipo | Licença SIL OFL, não MIT | Pilha de fontes do sistema (Georgia, serif); ver ponto em aberto na seção 16 |

**Itens que não são código da solução**

O SQL Server (motor proprietário, edições Express e Developer gratuitas), o Docker, o serviço de login do Google e o provedor de hospedagem são infraestrutura contratada ou instalada, não dependências de código. O pedido de usar SQL Server já os torna uma exceção conhecida.

**Como garantir na prática**

- O pull request tem um item de checklist: "adicionei pacote novo? licença MIT conferida".
- Um script de auditoria no pipeline lista todas as dependências (diretas e transitivas) com a licença declarada e falha se aparecer uma fora da lista aprovada. A ferramenta usada para isso também precisa ser MIT; confirmar antes de adotar.
- O arquivo `LICENCAS.md` na raiz registra cada pacote aprovado, versão e data da conferência.

## 3. Arquitetura

A solução tem quatro camadas com dependência em uma só direção: Web depende de Aplicação, Infraestrutura depende de Aplicação, e as duas dependem do Domínio. O Domínio não depende de nada, o que permite testar todas as regras do jogo sem banco, sem web e sem relógio real.

| Camada (projeto) | Responsabilidade | Pode conhecer |
| --- | --- | --- |
| `Bestia.Dominio` | Regras do jogo e do cadastro: Baralho, Carta, Mao, Rodada, Montante, Baú, Jogador, Mesa. Sem I/O. | Nada |
| `Bestia.Aplicacao` | Casos de uso (CriarConta, EntrarNaSala, JogarCarta...), contratos (interfaces) de repositório, criptografia, e-mail e relógio | Dominio |
| `Bestia.Infraestrutura` | EF Core e migrações, repositórios, criptografia AES-GCM, envio de e-mail, relógio do sistema | Aplicacao, Dominio |
| `Bestia.Web` | Controllers MVC, Hubs SignalR, Views e Partial Views, `wwwroot`, configuração e injeção de dependência | Aplicacao, Infraestrutura (só na composição) |
| `Bestia.Recursos` | Arquivos `.resx` gerados e a classe tipada de chaves | Nada |
| `Ferramentas/Bestia.GeradorDeRecursos` | Console que lê o arquivo mestre de textos e gera os `.resx` | Nada |

**Estrutura de pastas**

```text
Bestia/
  Bestia.sln
  LICENCAS.md
  docs/                         plano, regras, decisões (este documento)
  src/
    Bestia.Dominio/
      Cartas/                   Carta, Naipe, Valor, Baralho, ForcaDaCarta
      Partidas/                 Mesa, Mao, Rodada, Jogada, Montante, Bau, Decisao
      Jogadores/                Jogador, Ficha, Assento
      Servicos/                 ComparadorDeCartas, SorteadorDeAssento
    Bestia.Aplicacao/
      Contas/                   CriarConta, EntrarComSenha, EntrarComGoogle, ExcluirConta
      Salas/                    CriarSala, EntrarNaSala, SairDaSala
      Partidas/                 IniciarPartida, DecidirFicarOuCorrer, PegarBau, JogarCarta
      Privacidade/              ExportarDados, RegistrarConsentimento
      Contratos/                IRepositorioDeJogadores, ICriptografia, IRelogio, IEnviadorDeEmail
    Bestia.Infraestrutura/
      Persistencia/             BestiaDbContext, Mapeamentos, Migracoes
      Repositorios/
      Seguranca/                CriptografiaAesGcm, IndiceCego, ProvedorDeChaves
      Email/
    Bestia.Web/
      Controllers/              ContaController, SalasController, PartidaController, RegrasController, ConfiguracoesController
      Hubs/                     MesaHub
      Views/                    Compartilhado/_Layout, _MenuLateral, parciais por tela
      wwwroot/                  css, js, imagens
    Bestia.Recursos/
      Textos.pt-BR.resx  Textos.en.resx  Textos.es.resx
      textos-mestre.json
  tests/
    Bestia.Testes.Dominio/
    Bestia.Testes.Aplicacao/
    Bestia.Testes.Integracao/
  ferramentas/
    Bestia.GeradorDeRecursos/
```

**Fluxo de uma jogada**

1. O navegador chama o método `JogarCarta` do `MesaHub` pelo SignalR, enviando só o identificador da carta.
2. O Hub identifica o jogador pelo cookie de login (nunca por dado enviado pelo navegador) e chama o caso de uso `JogarCarta`.
3. O caso de uso localiza a mesa em memória, confere se é a vez do jogador e se a carta é permitida, e pede à `Rodada` do Domínio para registrar a jogada.
4. O Domínio devolve o resultado (carta aceita, rodada encerrada, vencedor, fichas movidas).
5. O Hub envia a cada jogador uma visão filtrada da mesa: cada um recebe apenas as próprias cartas e os baús fechados continuam sem conteúdo.
6. Ao fim da mão, o resultado é gravado no banco em uma transação única.

**Decisões de arquitetura**

- **Servidor autoritativo:** o navegador só pede ações; quem valida e decide é o servidor. Isso impede trapaça por adulteração do JavaScript.
- **Mesa em memória, histórico no banco:** a mesa em andamento vive em memória (rápido, simples). O banco guarda contas, salas, partidas concluídas e movimentos de fichas.
- **Relógio injetado:** os timers de 20 s e 45 s usam `IRelogio`, para os testes simularem o tempo passar sem esperar.
- **MVC com Partial Views:** o esqueleto da tela (layout e menu lateral) é renderizado no servidor; a mesa de jogo é atualizada no navegador por JavaScript simples recebendo eventos do SignalR.

## 4. Padrões de código (Clean Code em pt-BR)

Todo identificador de negócio é escrito em português, sem acento e sem abreviação, de forma que o código seja lido como o regulamento do jogo. Termos técnicos do framework (`Controller`, `Hub`, `Task`, `DbContext`) ficam como são.

**Nomenclatura**

| Elemento | Regra | Exemplo |
| --- | --- | --- |
| Classe e registro | Substantivo no singular, PascalCase | `Montante`, `Rodada`, `Bau` |
| Método | Verbo no infinitivo + objeto | `PegarBau()`, `DistribuirCartas()` |
| Booleano | Pergunta | `EstaVivo`, `PodeCobrirMontante` |
| Caso de uso | Verbo + objeto + sufixo `CasoDeUso` | `JogarCartaCasoDeUso` |
| Modelo de tela | Nome + `Modelo` | `EntrarModelo`, `MesaModelo` |
| Interface | Prefixo `I` + nome | `IRepositorioDeJogadores` |
| Constante | Nome explícito, nunca número solto | `FichasIniciais = 20`, `SegundosParaDecidir = 20` |
| Exceção de regra | Sufixo `Excecao` | `JogadaInvalidaExcecao` |
| Texto visível | Nunca no código: sempre chave de resource | `Textos.Menu_IniciarPartida` |

**Regras de escrita**

- Métodos curtos: até 20 linhas, uma responsabilidade, no máximo 3 parâmetros (acima disso, criar um objeto).
- Retorno antecipado (guard clauses) em vez de `if` aninhado.
- Sem comentários que repetem o código; comentário só para explicar um porquê não óbvio (por exemplo, uma regra do jogo ainda em aberto).
- Sem números ou textos mágicos: valores do jogo (20 fichas, 3 cartas, 3 rodadas, 45 s) viram constantes nomeadas em `ConfiguracaoDoJogo`.
- Falhas esperadas (senha errada, sala cheia) retornam um `Resultado` com a chave de mensagem; exceções ficam para o que não devia acontecer.
- Entidades do Domínio protegem as próprias regras: construtores e métodos validam, setters são privados.
- Controllers finos: recebem, chamam um caso de uso, devolvem a View. Nada de regra de negócio neles nem nas Views.
- Operações de I/O são `async` com sufixo `Async` e recebem `CancellationToken`.
- `Nullable` habilitado em todos os projetos e avisos tratados como erro.

**Ferramentas de qualidade (todas MIT)**

- `.editorconfig` na raiz com as regras de nomenclatura e formatação, aplicadas pelo SDK do .NET.
- Analisadores do próprio SDK (`AnalysisLevel` máximo) e `dotnet format` no pipeline.
- Mensagens de commit em português, no imperativo ("Adiciona regra do segundo baú").

**Checklist de revisão de pull request**

- [ ] Há teste cobrindo a regra alterada?
- [ ] Nenhum texto fixo em Views ou código (tudo em resources, nas três línguas)?
- [ ] Nenhum dado pessoal em log, URL ou mensagem de erro?
- [ ] Pacote novo tem licença MIT conferida?
- [ ] Nomes em português, sem abreviação?
- [ ] Alguma consulta ao banco dentro de laço (N+1)?

## 5. Internacionalização e resources

Nenhum texto visível existe fora de resources. Existe **um arquivo mestre** com todas as chaves e as três traduções lado a lado; dele são gerados os `.resx` de cada idioma e uma classe com as chaves tipadas. Assim ninguém esquece uma tradução e um erro de digitação na chave vira erro de compilação.

**Arquivo mestre** (`src/Bestia.Recursos/textos-mestre.json`)

```json
{
  "Menu_IniciarPartida": {
    "pt-BR": "Iniciar partida",
    "en": "Start game",
    "es": "Iniciar partida"
  },
  "Partida_BauSegundoCusto": {
    "pt-BR": "Pegar o segundo baú custa {0} fichas.",
    "en": "Taking the second chest costs {0} chips.",
    "es": "Tomar el segundo cofre cuesta {0} fichas."
  }
}
```

**Geração**

1. A ferramenta `Bestia.GeradorDeRecursos` lê o JSON e gera `Textos.pt-BR.resx`, `Textos.en.resx`, `Textos.es.resx` e a classe estática `Textos` com uma constante por chave.
2. Ela roda no build do projeto `Bestia.Recursos`; os `.resx` gerados não são editados à mão.
3. A ferramenta falha o build se alguma chave não tiver as três línguas preenchidas, ou se houver chave duplicada ou fora do padrão.

**Convenção de chaves:** `Area_Elemento`, em português e sem acento.

| Prefixo | Uso | Exemplos |
| --- | --- | --- |
| `Menu_` | Itens do menu lateral | `Menu_IniciarPartida`, `Menu_Regras`, `Menu_Configuracoes`, `Menu_Sair` |
| `Conta_` | Cadastro e login | `Conta_Entrar`, `Conta_CriarConta`, `Conta_EntrarComGoogle`, `Conta_EsqueciSenha` |
| `Sala_` | Salas e espera | `Sala_Criar`, `Sala_Cheia`, `Sala_AguardandoJogadores` |
| `Partida_` | Mesa e jogo | `Partida_Ficar`, `Partida_Correr`, `Partida_PegarBau`, `Partida_SuaVez` |
| `Regras_` | Tela de regras | `Regras_Titulo`, `Regras_Bau_Paragrafo1` |
| `Config_` | Configurações | `Config_Idioma`, `Config_ExcluirConta` |
| `Privacidade_` | LGPD | `Privacidade_Consentimento`, `Privacidade_ExportarDados` |
| `Erro_` | Mensagens de falha devolvidas pelo servidor | `Erro_SenhaInvalida`, `Erro_NaoEhSuaVez` |
| `Emote_` | Rótulos dos emotes | `Emote_Risada` |

**Como o idioma é escolhido**

- Idiomas aceitos: `pt-BR` (padrão), `en`, `es`.
- Ordem de decisão: preferência salva no perfil do jogador, depois cookie de cultura, depois cabeçalho `Accept-Language` do navegador, depois `pt-BR`.
- Trocar o idioma em Configurações grava a preferência no perfil e no cookie, e vale para a próxima página sem reiniciar a partida.

**Regras de uso**

- Em Views: `@Localizer[Textos.Menu_IniciarPartida]` (`IStringLocalizer`).
- Erros do servidor não carregam texto: carregam a **chave** (`Erro_NaoEhSuaVez`), e a camada Web traduz.
- Textos do JavaScript vêm de um endpoint `/recursos/{cultura}.json` gerado do mesmo arquivo mestre, só com as chaves marcadas como "navegador".
- Números, datas e fichas usam a cultura ativa (`CultureInfo`); nunca montar texto concatenando pedaços (a ordem das palavras muda entre idiomas), sempre usar placeholders `{0}`.
- Os textos das regras são longos: cada parágrafo é uma chave (`Regras_Bau_Paragrafo1`), nunca uma View duplicada por idioma.

**Teste de paridade (roda em todo build)**

- Toda chave usada em `.cshtml` ou `.cs` existe no arquivo mestre.
- Toda chave do mestre tem as três línguas e os mesmos placeholders (`{0}`, `{1}`) em todas.
- Nenhuma View contém texto visível solto (busca por texto entre tags fora de `@Localizer`).

## 6. Autenticação e contas

O login usa **cookie de autenticação do ASP.NET Core** e a tabela própria `Jogador`, sem o ASP.NET Identity completo. Motivo: o Identity guarda e-mail em texto aberto e procura por ele, o que conflita com a criptografia exigida pela LGPD (seção 7). Do Identity usamos apenas o `PasswordHasher`, que é MIT e está bem testado.

**Cadastro com e-mail e senha**

1. A pessoa informa apelido, e-mail e senha, e marca o aceite dos Termos e da Política de Privacidade (checkbox não pré-marcado) e a declaração de ter 18 anos ou mais.
2. O servidor valida: apelido único (3 a 16 caracteres), e-mail em formato válido, senha com no mínimo 10 caracteres e fora de uma lista curta de senhas comuns.
3. A senha vira hash com `PasswordHasher` (PBKDF2 com sal único); o e-mail é criptografado e indexado por hash (seção 7); o aceite é gravado em `Consentimento`.
4. Um e-mail com link de confirmação válido por 24 horas é enviado. Sem confirmação, a conta entra no lobby mas não joga.

**Login com Google**

1. O botão "Entrar com Google" dispara o fluxo OAuth 2.0 / OpenID Connect com os escopos `openid`, `email` e `profile`.
2. No retorno, o servidor lê o identificador estável do Google (`sub`) e procura em `LoginExterno`.
3. Se não existe e o Google informa e-mail verificado, a conta é criada, ou ligada a uma conta local que já use o mesmo e-mail. Antes disso, uma tela pede o aceite dos Termos e da Privacidade e o apelido (sugerido pelo nome do Google, editável).
4. Não gravamos foto, sobrenome nem qualquer dado do Google além do necessário: identificador (guardado só como hash), e-mail e um nome de exibição opcional.
5. As chaves do Google (`ClientId` e `ClientSecret`) ficam em segredo de configuração, nunca no repositório.

**Sessão e proteção**

| Item | Regra |
| --- | --- |
| Cookie | `HttpOnly`, `Secure`, `SameSite=Lax`, expiração de 8 h com renovação por uso |
| Formulários | Token anti-falsificação em todo POST (`[ValidateAntiForgeryToken]` global) |
| Acesso | Política global exige login; páginas públicas marcam `[AllowAnonymous]` de forma explícita |
| Tentativas | 5 senhas erradas bloqueiam o login por 15 minutos; o limitador de requisições do ASP.NET Core (MIT) protege os endpoints de login, cadastro e recuperação |
| Mensagens | "E-mail ou senha inválidos" sempre igual, para não revelar quais e-mails existem |
| SignalR | A conexão da mesa usa o mesmo cookie; o servidor nunca confia no identificador de jogador enviado pelo navegador |
| Uma sessão por mesa | Entrar na mesma sala por outra aba desconecta a anterior |

**Recuperação de senha**

- O pedido gera um token aleatório de uso único, válido por 1 hora; no banco guardamos só o hash do token.
- A resposta é sempre "se o e-mail existir, enviamos instruções".
- Ao redefinir, todas as sessões abertas do jogador são invalidadas.

**Telas desta fase:** Entrar, Criar conta, Confirmar e-mail, Esqueci minha senha, Redefinir senha, Aceite de termos (primeiro acesso com Google).

## 7. Segurança e LGPD

Todo dado pessoal é criptografado no nível do campo antes de ir ao banco, com chaves guardadas fora do banco, e o produto só coleta o mínimo necessário. Este capítulo é um plano técnico; as bases legais, os textos de Termos e Privacidade e os prazos precisam ser validados por um advogado e pelo encarregado de dados antes da publicação.

**Inventário de dados (minimização)**

| Dado | Para quê | Base legal (a validar) | Como é guardado | Retenção |
| --- | --- | --- | --- | --- |
| E-mail | Login, confirmação, recuperação | Execução de contrato | AES-GCM + índice cego (HMAC) | Até a exclusão da conta |
| Apelido | Aparecer na mesa | Execução de contrato | AES-GCM + índice cego para unicidade | Até a exclusão da conta |
| Senha | Login | Execução de contrato | Somente hash PBKDF2 com sal, sem como reverter | Até a exclusão da conta |
| Identificador do Google | Reconhecer o login | Execução de contrato | Somente hash (HMAC) | Até a exclusão da conta |
| Nome de exibição do Google (opcional) | Sugerir apelido | Consentimento | AES-GCM | Até o aceite do apelido; depois apagado |
| Idioma e preferências | Usar o site | Execução de contrato | Texto simples (não identifica ninguém) | Até a exclusão da conta |
| Aceites (termos, privacidade) | Prova de consentimento | Obrigação legal e exercício de direitos | Versão, data e hash do IP | Enquanto houver relacionamento mais prazo prescricional (validar) |
| IP e data de acesso | Segurança e registro de acesso | Obrigação legal (Marco Civil) e legítimo interesse | AES-GCM em tabela separada | 6 meses (validar com jurídico) |
| Histórico de partidas e fichas | Perfil e estatísticas | Execução de contrato | Referência ao Id do jogador, sem texto pessoal | Mantido; anonimizado na exclusão |

Não coletamos telefone, CPF, endereço, localização, foto, contatos nem dados de pagamento. Não há cookies de análise ou publicidade; os cookies usados são só os essenciais (login, idioma e anti-falsificação).

**Criptografia dos campos**

- **Algoritmo:** AES-256-GCM da biblioteca do .NET, com `nonce` aleatório de 12 bytes novo a cada gravação. O valor guardado em `VARBINARY` tem o formato: versão • identificador da chave • nonce • tag • texto cifrado.
- **Busca sem decifrar (índice cego):** para achar um jogador pelo e-mail, o sistema normaliza o e-mail (minúsculas, sem espaços) e calcula `HMAC-SHA256` com uma chave própria de índice; a coluna `EmailHash` tem índice único. A mesma técnica vale para apelido e identificador do Google.
- **Chaves separadas por finalidade:** uma chave mestra, e dela, por HKDF, a chave de criptografia, a chave de índice e a chave de tokens. O vazamento de uma não expõe as outras.
- **Onde ficam as chaves:** fora do banco e fora do repositório: variável de ambiente ou cofre de segredos do provedor (o SDK do Azure Key Vault é MIT). Em desenvolvimento, `dotnet user-secrets`.
- **Rotação:** cada valor cifrado carrega o identificador da chave; para trocar a chave, uma tarefa recifra os registros aos poucos, e a chave antiga só é apagada ao fim.
- **Em trânsito:** somente HTTPS, HSTS ativo, TLS 1.2 ou superior, e `Encrypt=True` na conexão com o SQL Server.
- **Em repouso (camada extra):** TDE no SQL Server quando a edição permitir e backups criptografados.

**Por que criptografia na aplicação e não só no banco:** quem tem acesso ao banco (administrador, backup vazado, dump) vê apenas bytes ilegíveis; as chaves nunca estão com ele.

**Direitos do titular (art. 18 da LGPD)**

| Direito | Como o sistema atende |
| --- | --- |
| Confirmação e acesso | Tela Configurações > Privacidade > "Meus dados" mostra tudo que guardamos |
| Portabilidade | Botão "Baixar meus dados" gera um arquivo JSON com perfil, aceites e histórico |
| Correção | Apelido e e-mail editáveis (e-mail novo exige nova confirmação) |
| Eliminação e anonimização | "Excluir minha conta" (pede a senha ou novo login com Google) apaga e-mail, apelido e nome e troca o jogador por "Jogador removido" nas partidas, preservando o histórico dos outros |
| Revogação do consentimento | Cada consentimento pode ser retirado; se for essencial, a conta é encerrada |
| Informação sobre compartilhamento | A política lista os operadores (hospedagem, e-mail, Google) |
| Pedido por e-mail ao encarregado | Gravado em `SolicitacaoPrivacidade` com prazo de 15 dias para resposta |

**Governança**

- **Encarregado de dados (DPO):** nome e e-mail publicados na Política de Privacidade.
- **Registro das operações de tratamento:** este inventário, mantido atualizado a cada nova coluna pessoal.
- **Incidente de segurança:** plano escrito com responsáveis, contenção, e comunicação à ANPD e aos titulares dentro do prazo regulatório vigente (hoje 3 dias úteis, conforme a Resolução CD/ANPD 15/2024; confirmar).
- **Logs de aplicação:** nunca contêm e-mail, apelido, IP ou senha; usam somente o `Id` do jogador. Existe teste que falha se um e-mail aparecer no log.
- **Auditoria:** `RegistroAuditoria` anota quem fez o quê e quando (login, troca de e-mail, exportação, exclusão), sem o dado pessoal em si.
- **Contas inativas:** aviso por e-mail após 24 meses sem acesso e exclusão 30 dias depois, se não houver retorno.
- **Menores:** na v1 o cadastro exige declaração de 18 anos ou mais, o que evita o tratamento específico do art. 14 da LGPD (ponto em aberto na seção 16).

**Outras proteções de segurança**

- Cabeçalhos HTTP: `Content-Security-Policy` restritiva, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`.
- Entrada sempre validada no servidor; saída HTML codificada pelo Razor por padrão; consultas apenas via EF Core parametrizado.
- O servidor valida cada ação de jogo (vez, carta válida, fichas) e limita a frequência de mensagens por conexão, incluindo emotes.
- Dependências com alerta de vulnerabilidade checadas no pipeline (`dotnet list package --vulnerable`).

## 8. Banco de dados (SQL Server)

O banco guarda contas, salas e o histórico das partidas concluídas; a mesa em andamento fica em memória. São 17 tabelas, todas com nomes no singular e em português, para qualquer pessoa ler o modelo sem dicionário.

**Convenções**

- Tabela no singular (`Jogador`, `Partida`); chave primária sempre `Id` (`uniqueidentifier`, gerado pela aplicação em ordem sequencial para não fragmentar o índice).
- Chave estrangeira = nome da tabela + `Id` (`JogadorId`, `PartidaId`).
- Datas em UTC, `datetime2`, com nome terminado em `Em` (`CriadoEm`, `EncerradaEm`). Booleanos começam com `Eh` ou `Esta`.
- Campos criptografados terminam em `Cifrado` (`varbinary`); índices cegos terminam em `Hash`.
- Estados e tipos são `tinyint` mapeados para `enum` do C#, com uma tabela de apoio apenas quando houver texto para exibir.
- Fichas nunca ficam negativas: restrição `CHECK (Fichas >= 0)`.

**Contas e privacidade**

| Tabela | Para que serve | Colunas principais |
| --- | --- | --- |
| `Jogador` | Conta da pessoa | `Id`, `ApelidoCifrado`, `ApelidoHash` (único), `EmailCifrado`, `EmailHash` (único), `SenhaHash` (nula se só Google), `EmailConfirmadoEm`, `Idioma`, `CorDoAvatar`, `FalhasDeLogin`, `BloqueadoAte`, `CriadoEm`, `UltimoAcessoEm`, `RemovidoEm` |
| `LoginExterno` | Vínculo com o Google | `Id`, `JogadorId`, `Provedor`, `IdExternoHash` (único com `Provedor`), `CriadoEm` |
| `TokenDeConta` | Confirmação de e-mail e redefinição de senha | `Id`, `JogadorId`, `Tipo`, `TokenHash`, `ExpiraEm`, `UsadoEm` |
| `Consentimento` | Prova dos aceites | `Id`, `JogadorId`, `Documento`, `Versao`, `AceitoEm`, `IpHash`, `RevogadoEm` |
| `SolicitacaoPrivacidade` | Pedidos do titular (art. 18) | `Id`, `JogadorId`, `Tipo`, `CriadaEm`, `PrazoEm`, `RespondidaEm`, `Situacao` |
| `RegistroDeAcesso` | Log de acesso exigido por lei | `Id`, `JogadorId`, `IpCifrado`, `Tipo`, `OcorreuEm` |
| `RegistroDeAuditoria` | Ações sensíveis | `Id`, `JogadorId`, `Acao`, `OcorreuEm`, `Detalhe` (sem dado pessoal) |

**Salas e partidas**

| Tabela | Para que serve | Colunas principais |
| --- | --- | --- |
| `Sala` | Lugar onde o grupo se reúne | `Id`, `Nome`, `Codigo`, `CriadaPorJogadorId`, `MaximoDeJogadores`, `Situacao`, `CriadaEm` |
| `SalaJogador` | Quem está na sala | `SalaId`, `JogadorId`, `EntrouEm`, `SaiuEm` |
| `Partida` | Uma partida completa | `Id`, `SalaId`, `IniciadaEm`, `EncerradaEm`, `QuantidadeDeJogadores`, `VencedorJogadorId`, `Situacao` |
| `PartidaJogador` | Participante da partida | `PartidaId`, `JogadorId` (nulo se bot), `NomeDoBot`, `Assento`, `FichasIniciais`, `FichasFinais`, `Colocacao`, `MotivoDaSaida` |
| `Mao` | Cada distribuição de cartas | `Id`, `PartidaId`, `Numero`, `DistribuidorJogadorId`, `Mania` (naipe), `Montante`, `TaxaDeBau`, `Desfecho` |
| `MaoJogador` | O que cada um fez na mão | `MaoId`, `JogadorId`, `Decisao` (ficou, correu, ausente), `BausPegos`, `RodadasVencidas`, `ReposicaoPaga` |
| `Bau` | Os 2 baús da mão | `Id`, `MaoId`, `Numero`, `PegoPorJogadorId`, `TaxaPaga` |
| `Rodada` | As 3 rodadas da mão | `Id`, `MaoId`, `Numero`, `AbridorJogadorId`, `VencedorJogadorId`, `Valor` |
| `Jogada` | Cada carta jogada | `Id`, `RodadaId`, `JogadorId`, `Ordem`, `Naipe`, `Valor`, `FoiAutomatica`, `FoiVirada` |
| `MovimentoDeFicha` | Livro-caixa de todas as fichas | `Id`, `PartidaId`, `MaoId`, `JogadorId`, `Tipo`, `Valor` (positivo ou negativo), `OcorreuEm` |

**Tipos de `MovimentoDeFicha`:** `ApostaDoDistribuidor`, `TaxaDoBau`, `PremioDaRodada`, `ReposicaoDoMontante`, `MontanteDoDistribuidor` (quando todos correm) e `PremioDoUnicoQueFicou`. Regra de integridade: a soma de todos os movimentos de uma partida é igual a menos o montante ainda pendente na mesa (zero quando não sobra montante); isso vira teste automático e alarme.

**Relacionamentos principais**

- `Jogador` 1–N `LoginExterno`, `TokenDeConta`, `Consentimento`, `SolicitacaoPrivacidade`.
- `Sala` 1–N `SalaJogador`; `Sala` 1–N `Partida`.
- `Partida` 1–N `PartidaJogador`, `Mao`, `MovimentoDeFicha`.
- `Mao` 1–N `MaoJogador`, `Bau`, `Rodada`; `Rodada` 1–N `Jogada`.

**Índices e restrições**

- Únicos: `Jogador.EmailHash`, `Jogador.ApelidoHash`, `LoginExterno(Provedor, IdExternoHash)`, `Sala.Codigo`, `Mao(PartidaId, Numero)`, `Rodada(MaoId, Numero)`, `Bau(MaoId, Numero)`.
- De consulta: `PartidaJogador(JogadorId)` para o histórico do perfil; `RegistroDeAcesso(OcorreuEm)` para a rotina de apagar após 6 meses.
- `rowversion` em `Jogador` e `Sala` para evitar duas alterações simultâneas se sobrescreverem.

**Exclusão de conta (anonimização):** em `Jogador`, `ApelidoCifrado`, `EmailCifrado` e hashes são apagados, `RemovidoEm` recebe a data e a linha continua existindo para manter as chaves estrangeiras do histórico. As tabelas `LoginExterno`, `TokenDeConta` e `RegistroDeAcesso` do jogador são apagadas; `Consentimento` fica pelo prazo legal.

**Migrações**

- Migrações do EF Core versionadas no repositório, uma por mudança, com nome em português (`CriaTabelaJogador`).
- Em produção, o deploy gera um script SQL idempotente e o aplica em etapa separada; a aplicação não migra sozinha ao subir.
- Backup diário criptografado, com teste de restauração mensal.

## 9. Regras do jogo no domínio

O motor das regras vive em `Bestia.Dominio`, sem banco, rede ou relógio, e é a única fonte da verdade. A referência de texto é o `Bestia.md`; a de comportamento é o protótipo. Qualquer dúvida de regra vira um teste antes de virar código.

**Constantes** (classe `ConfiguracaoDoJogo`, nunca números soltos)

| Constante | Valor |
| --- | --- |
| `FichasIniciais` | 20 |
| `MinimoDeJogadores` / `MaximoDeJogadores` | 3 / 10 |
| `CartasPorMao` e `RodadasPorMao` | 3 e 3 |
| `ApostaDoDistribuidor` | 3 fichas |
| `TaxaDoSegundoBau` | 3 fichas |
| `MinimoDeFichasParaContinuar` | 3 |
| `SegundosParaDecidir` | 20 |
| `SegundosParaJogarCarta` | 45 |
| `AusenciasParaEliminar` | 3 (as três rodadas da mesma mão) |

**Baralho e força das cartas**

- 48 cartas: A, 2, 3, 4, 5, 6, 7, 8, 9, J, Q, K em 4 naipes.
- Ordem do mais forte ao mais fraco: **A, 3, 2, K, Q, J, 9, 8, 7, 6, 5, 4**.
- A cada mão uma carta é virada: o naipe dela é a **mania** (trunfo).

**Fluxo de uma mão (máquina de estados)**

1. **Aposta:** o distribuidor coloca 3 fichas. O montante é essas 3 mais o que ficou pendente da mão anterior (sempre múltiplo de 3).
2. **Distribuição:** 3 cartas para cada jogador vivo, vira a mania, separa 2 baús de 3 cartas viradas para baixo.
3. **Decisão**, em sentido horário a partir de quem está à esquerda do distribuidor, que decide por último. Cada um tem 20 s e escolhe: ficar, correr ou pegar um baú.
   - Só pode ficar quem tem fichas iguais ou maiores que o montante; quem não tem só pode correr.
   - Se o tempo acabar: quem não pegou baú corre; quem pegou segue com a mão atual.
4. **Atalhos:** se todos correm, o distribuidor leva o montante; se só um fica, ele leva o montante inteiro sem jogar.
5. **Rodadas 1 a 3:** na primeira, abre quem ficou primeiro à esquerda do distribuidor; nas seguintes, abre quem venceu a rodada anterior.
   - É obrigatório seguir o naipe da carta aberta; sem esse naipe, joga qualquer carta.
   - Vence a maior carta de mania da mesa; se não houver mania, a maior do naipe aberto.
   - A rodada vale montante ÷ 3 e vai para o vencedor.
   - O jogador tem 45 s para jogar; se estourar, o jogo joga por ele a carta mais fraca permitida, virada para baixo até o fim da rodada.
6. **Reposição:** quem ficou e perdeu as 3 rodadas repõe o montante inteiro com as próprias fichas; o valor fica na mesa para a próxima mão.
7. **Fim da mão:** quem tem menos de 3 fichas sai; quem esteve ausente nas 3 rodadas é eliminado por inatividade; o distribuidor passa ao próximo à esquerda.

Não existe empate: são 3 rodadas e uma carta sempre vence a rodada.

**Baús**

- São 2 baús de 3 cartas, viradas para baixo, cada um com no máximo um dono por mão.
- Pegar um baú troca a mão: a mão antiga é descartada, a do baú passa a ser a do jogador (ele só vê depois de pegar) e ele fica **obrigado a ficar**, sem poder correr.
- Se o mesmo jogador pega o segundo baú, paga 3 fichas ao montante (que passa a valer 3 a mais, ou seja, cada rodada vale 1 ficha a mais) e joga com a mão do último baú que abriu, sem poder escolher.
- Se outro jogador pega o segundo baú, ele só fica obrigado a ficar e não paga nada.
- Quando os dois baús já foram pegos, a decisão de quem pegou acaba na hora.
- Baús não pegos e mãos descartadas voltam ao baralho ao fim da mão.
- Restrições adotadas (a confirmar): só pega baú quem cobre o montante, e o segundo baú só é permitido se o pagante e todos que já ficaram ainda cobrem o novo montante.

**Início e fim da partida**

- Entre 3 e 10 jogadores, 20 fichas cada, sentados em círculo; um assento é sorteado para o relógio das 12 h e o jogo corre no sentido horário. O primeiro distribuidor é sorteado.
- Quem fica com menos de 3 fichas é eliminado. Cada um pode desistir quando quiser.
- A partida acaba quando restam menos de 3 jogadores; o ranking é por fichas.

**Cenários que viram testes obrigatórios**

| Cenário | Resultado esperado |
| --- | --- |
| Ordem de força com A, 3 e 2 na mesma rodada | A vence, depois 3, depois 2 |
| Carta de mania contra carta mais alta do naipe aberto | A mania vence |
| Jogador tem o naipe aberto e tenta jogar outro | Jogada recusada com `Erro_DeveSeguirONaipe` |
| Todos correm com montante de 6 | Distribuidor recebe 6 |
| Um jogador fica, os outros correm | Ele recebe o montante inteiro, sem rodadas |
| Jogador ficou e perdeu as 3 rodadas, montante 9 | Perde 9, e a próxima mão começa com 9 + 3 |
| Jogador com 8 fichas, montante 9 | Só pode correr |
| Mesmo jogador pega o baú 1 e depois o baú 2 | Paga 3, montante sobe 3, joga com as cartas do baú 2 |
| Outro jogador pega o baú 2 depois do baú 1 de A | Paga nada, é obrigado a ficar |
| Jogador tenta correr depois de pegar baú | Recusado |
| 45 s sem jogar | Carta mais fraca permitida entra virada para baixo |
| Três rodadas de ausência na mesma mão | Eliminado ao fim da mão e some da mesa |
| 100 mãos aleatórias com bots | Soma das fichas dos jogadores mais o montante pendente é sempre igual ao total inicial |

## 10. Partida online em tempo real

A partida roda no servidor e conversa com os navegadores por SignalR (WebSockets, com fallback automático). O navegador envia intenções; o servidor valida, aplica a regra e devolve a nova visão da mesa.

**Mensagens do navegador para o servidor (`MesaHub`)**

| Método | O que faz | Quem pode |
| --- | --- | --- |
| `EntrarNaSala(codigo)` | Senta na sala se houver vaga | Logado e com e-mail confirmado |
| `SairDaSala()` | Levanta e libera o assento | Quem está na sala |
| `PreencherComBots(quantidade)` | Completa a sala até 3 jogadores ou mais | Criador da sala |
| `IniciarPartida()` | Sorteia assentos e distribuidor e começa | Criador da sala, com 3+ jogadores |
| `Decidir(ficar ou correr)` | Responde à decisão da vez | Jogador da vez |
| `PegarBau(numero)` | Pega o baú 1 ou 2 | Jogador da vez |
| `JogarCarta(cartaId)` | Joga uma carta | Jogador da vez |
| `EnviarEmote(tipo)` | Mostra um dos 5 emotes | Quem está na sala |
| `Desistir()` | Sai da partida com as fichas que tem | Quem está na partida |

**Eventos do servidor para o navegador**

- `MesaAtualizada`: foto completa da mesa na visão daquele jogador; é enviada ao entrar, ao reconectar e ao fim de cada etapa.
- `SuaVez` e `PrazoIniciado`: avisa de quem é a vez e até que horário do servidor ela vale (a barra de tempo é só visual).
- `CartaJogada`, `BauPego`, `RodadaEncerrada`, `FichasMovidas`, `EmoteRecebido`, `JogadorEliminado`, `PartidaEncerrada` e `Erro(chave)`; cada um alimenta uma animação ou uma mensagem.

**Regras de funcionamento**

- **Um comando por vez por mesa:** cada mesa tem uma fila interna (`Channel`) e um único processador, então duas ações simultâneas nunca se atropelam, sem travas espalhadas.
- **Visão filtrada:** o servidor monta para cada jogador uma visão em que as cartas dos outros, o conteúdo dos baús fechados e as cartas jogadas por ausência (até a rodada fechar) são omitidos. Nada secreto trafega para o navegador.
- **Tempo no servidor:** os prazos de 20 s e 45 s são controlados por `IRelogio` no servidor; ao estourar, o próprio servidor executa a ação automática e conta a ausência. O navegador só desenha a contagem.
- **Sorteios justos:** embaralhar, sortear o assento das 12 h e o primeiro distribuidor usam `RandomNumberGenerator` (criptográfico), não `Random`.
- **Queda de conexão:** o jogador desconectado continua sentado; o prazo corre normalmente (não há pausa) e, se o tempo acabar, vale a ação automática. Ao reconectar em até 2 minutos ele recebe a `MesaAtualizada` e segue. Uma segunda aba derruba a primeira.
- **Limites:** máximo de 1 emote a cada 2,5 s por jogador e de 20 mensagens por segundo por conexão; acima disso a conexão é encerrada.
- **Bots:** rodam no servidor atrás da interface `IEstrategiaDeBot`, com a mesma lógica do protótipo (avaliar a mão, decidir pegar baú, escolher carta), e esperam de 0,7 a 1,5 s antes de agir para o jogo ter ritmo humano. Bots nunca aparecem em ranking global.
- **Salas:** a lista de salas do lobby é atualizada a cada 5 s (consulta simples); sala privada tem código de 6 caracteres.

**Gravação no banco**

- Ao fim de cada mão: uma transação grava `Mao`, `MaoJogador`, `Bau`, `Rodada`, `Jogada`, `MovimentoDeFicha` e as fichas atuais de `PartidaJogador`.
- Ao fim da partida: fecha `Partida` com vencedor e colocações.
- Se o servidor cair no meio de uma mão, a mão em curso é perdida, a partida fica `Interrompida` e as fichas voltam ao valor do fim da mão anterior.

**Escala (depois da v1):** para mais de um servidor, adicionar o backplane Redis do SignalR (MIT) e fixar cada mesa em um servidor por sessão aderente; a fila interna por mesa já prepara essa troca.

## 11. Interface

O site tem um esqueleto fixo (`_Layout.cshtml`) com **menu lateral esquerdo** e área de conteúdo à direita. O visual segue o protótipo de taverna (madeira escura, feltro verde, pergaminho, luz de vela), usando Bootstrap 5 só para grade e componentes básicos e um `taverna.css` próprio com variáveis de cor.

**Menu lateral esquerdo** (partial `_MenuLateral`, em um `ViewComponent` que marca o item ativo)

| Item | Destino | Ícone (Bootstrap Icons) |
| --- | --- | --- |
| Iniciar partida | `/salas` (criar ou entrar em sala) | `play-circle` |
| Salas | `/salas` com lista aberta | `door-open` |
| Meu perfil | `/perfil` (estatísticas e histórico) | `person` |
| Regras do jogo | `/regras` | `book` |
| Configurações | `/configuracoes` | `gear` |
| Sair | encerra a sessão | `box-arrow-left` |

No topo do menu ficam o nome do jogo e o apelido do jogador; no rodapé, o seletor de idioma e os links de Termos e Privacidade. No celular o menu vira uma gaveta aberta pelo botão "hambúrguer"; na tela da mesa ele fica recolhido por padrão para dar espaço às cartas. Sair da mesa durante uma partida pede confirmação, porque a pessoa desiste das fichas.

**Telas**

| Rota | Tela | Observação |
| --- | --- | --- |
| `/conta/entrar` | Entrar | E-mail e senha, botão Google, links de cadastro e recuperação; sem menu lateral |
| `/conta/criar` | Criar conta | Apelido, e-mail, senha, aceites |
| `/conta/termos` | Primeiro acesso com Google | Aceite e apelido |
| `/conta/confirmar`, `/conta/esqueci-senha`, `/conta/redefinir-senha` | Fluxos de e-mail | Mensagens genéricas |
| `/salas` | Lobby | Lista de salas, criar sala, entrar por código |
| `/salas/{codigo}` | Sala de espera | Quem já sentou, preencher com bots, iniciar |
| `/partida/{id}` | Mesa | Tela do jogo (abaixo) |
| `/perfil` | Meu perfil | Partidas jogadas, vitórias, fichas, histórico recente |
| `/regras` | Regras do jogo | Seção 12 |
| `/configuracoes` | Configurações | Idioma, apelido, som, animações reduzidas |
| `/configuracoes/privacidade` | Privacidade | Meus dados, baixar, excluir conta, consentimentos |
| `/erro` | Erro e página não encontrada | Texto amigável, sem detalhes técnicos |

**Partial Views (reaproveitáveis)**

`_MenuLateral`, `_BarraSuperior`, `_SeletorDeIdioma`, `_CartaoDeSala`, `_AssentoDoJogador`, `_CartaDoBaralho`, `_BausDoCentro`, `_PainelDeDecisao`, `_BarraDeTempo`, `_Emotes`, `_DiarioDaMesa`, `_Faixa` (a faixa de avisos de vez, vitória e derrota), `_RodapeLegal`.

**Tela da mesa**

- A página do servidor entrega o esqueleto, os modelos HTML (`<template>`) de carta e assento e os textos do idioma. Depois disso, um módulo JavaScript (`mesa.js`) desenha tudo a partir dos eventos do SignalR; não há recarga de página durante o jogo.
- Layout herdado do protótipo: mesa redonda com assentos em círculo (12 h marcado), mania e montante no centro, os dois baús no centro, bandeja de madeira fixa embaixo com as cartas da mão e os botões de decisão, painel de emotes e diário recolhível.
- Animações herdadas: fichas voando do centro ao vencedor e dos jogadores ao centro, baú voando até quem pegou, cartas perdedoras tremendo e piscando em vermelho, vencedora piscando em verde, emotes sobre o assento, barra de tempo (20 s e 45 s).
- Sinalização de turno: faixa com o texto da vez, selo "▶ vez" e "próximo", "abre", "ficou" e "correu" em cada assento.
- Responsividade: celular até 520 px (mesa compacta, bandeja no pé), tablet e desktop com mesa grande; a partir de 1100 px os botões de decisão ficam ao lado das cartas.
- Jogador eliminado por falta de fichas fica esmaecido; eliminado por inatividade some da mesa.

**Acessibilidade e tema**

- Vitória e derrota têm ícone e texto além da cor (✔ venceu, ✖ perdeu), por causa de daltonismo.
- A faixa de avisos usa `aria-live="polite"`; botões e cartas funcionam por teclado.
- A opção "reduzir animações" respeita `prefers-reduced-motion` e pode ser ligada em Configurações.
- Fontes: pilha do sistema (Georgia e serif) por causa da regra de licença MIT (seção 2).
- Bibliotecas de front-end (Bootstrap, ícones, `signalr.js`) ficam copiadas em `wwwroot/lib` com a versão fixa e a licença ao lado; nada é carregado de CDN, o que também protege a privacidade dos jogadores.

## 12. Tela de regras do jogo

O jogador consulta as regras a qualquer momento: pelo item "Regras do jogo" do menu lateral e, durante a partida, por um botão "?" na mesa que abre as mesmas regras em uma janela sobre o jogo, sem sair dela e sem perder o prazo da vez.

**Como é construída**

- Um único conteúdo, a partial `_ConteudoDasRegras`, usado pela página `/regras` e pela janela da mesa. Não existe cópia do texto.
- Todo texto vem de resources (`Regras_...`), nos três idiomas, um parágrafo por chave.
- As ilustrações são HTML e CSS (cartas, fichas, baús, mesa), reaproveitadas da página explicativa `bestia-como-jogar.html`; não dependem de arquivos de imagem, então carregam rápido e acompanham o tema.
- Índice lateral com âncoras (no celular, vira lista no topo) e botão "voltar ao topo".
- Acima do conteúdo fica o resumo rápido de uma tela, para quem só quer relembrar.
- Rodapé mostra a versão das regras (`Regras_Versao`) e a data de atualização; qualquer mudança de regra altera o texto, o teste do domínio e a versão no mesmo pull request.

**Seções**

| Nº | Seção | Conteúdo |
| --- | --- | --- |
| 1 | Objetivo | Como se ganha fichas e como o jogo acaba |
| 2 | Cartas e força | 48 cartas, ordem A, 3, 2, K, Q, J, 9, 8, 7, 6, 5, 4 |
| 3 | Mania | A carta virada e o naipe que vence todos |
| 4 | Fichas e montante | 20 fichas, aposta de 3, rodada vale montante ÷ 3 |
| 5 | Distribuição | Distribuidor, 3 cartas, ordem no sentido horário |
| 6 | Ficar ou correr | Quem pode ficar, quem só corre, o que acontece se todos correm ou só um fica |
| 7 | Os baús | Pegar, trocar, segundo baú e taxa de 3 fichas |
| 8 | As três rodadas | Quem abre, seguir o naipe, quem vence |
| 9 | Reposição | Perder as 3 rodadas |
| 10 | Tempo | 20 s para decidir, 45 s para jogar, carta virada e eliminação por ausência |
| 11 | Fim da partida | Menos de 3 fichas, desistência, ranking |
| 12 | Exemplo passo a passo | Uma mão completa ilustrada, com e sem baú |

**Critérios de aceite**

- [ ] A página `/regras` abre em até 1 segundo em conexão comum e funciona sem JavaScript, exceto a janela da mesa.
- [ ] Trocar o idioma troca todos os textos, sem nenhuma chave aparecendo "crua".
- [ ] A janela da mesa abre e fecha sem interromper o temporizador nem as animações.
- [ ] Cada regra do capítulo 9 deste plano aparece no texto das regras (revisão manual a cada release).
- [ ] Leitura confortável em tela de 360 px de largura.

## 13. Estratégia de testes

As regras do jogo são escritas por testes primeiro (TDD): o cenário da seção 9 vira um teste que falha, depois o código que o faz passar. Os nomes dos testes contam a regra em português, como `Deve_CobrarTaxa_QuandoMesmoJogadorPegaSegundoBau`.

| Nível | O que cobre | Ferramentas (MIT) | Meta |
| --- | --- | --- | --- |
| Unidade do Domínio | Cartas, força, mania, seguir naipe, montante, baús, ausência, fim de mão e de partida | NUnit | Cobertura de linhas de 90% ou mais |
| Unidade da Aplicação | Casos de uso com repositórios falsos e relógio controlado (simula 20 s e 45 s sem esperar) | NUnit, fakes manuais | 80% ou mais |
| Simulação | 1.000 partidas com bots e embaralhamento com semente fixa; valida que as fichas fecham, ninguém fica negativo e nenhuma partida trava | NUnit | Roda no pipeline noturno |
| Integração com banco | Migrações, restrições, índices únicos, gravação de uma mão completa, anonimização na exclusão | NUnit, Testcontainers com SQL Server | Todos os repositórios |
| Integração web | Login, cookies, anti-falsificação, rotas protegidas, Hub com cliente SignalR de teste | `Microsoft.AspNetCore.Mvc.Testing` | Fluxos principais |
| Segurança | Criptografia ida e volta, adulteração detectada, nonce nunca repete, índice cego estável, nenhum dado pessoal em log, todo controller exige login | NUnit | Obrigatório |
| Idiomas | Paridade de chaves e de placeholders, nenhum texto fixo nas Views | NUnit | Roda em todo build |
| Manual | Mesa em celular, tablet e desktop; animações; leitor de tela básico | Roteiro escrito | A cada release |

**Determinismo:** o embaralhamento, o sorteio de assento e o relógio entram por interfaces (`IEmbaralhador`, `ISorteador`, `IRelogio`). Em produção usam números criptográficos e hora real; nos testes, uma sequência fixa, para reproduzir qualquer bug de jogo.

**Pipeline de integração contínua:** a cada push: compilar com avisos como erro, rodar `dotnet format --verify-no-changes`, testes de unidade e de idiomas, auditoria de licenças e de vulnerabilidades. A cada pull request: também os testes de integração. Toda noite: simulação de 1.000 partidas.

**Definição de pronto de uma tarefa:** código em português sem abreviação, testes passando, textos nos três idiomas, nenhum dado pessoal novo sem entrada no inventário da seção 7, revisão de outra pessoa.

## 14. Infraestrutura, configuração e deploy

**Ambientes:** Desenvolvimento (máquina da pessoa, SQL Server Developer ou Express em contêiner), Homologação (cópia da produção com dados fictícios) e Produção. Dado real de jogador nunca vai para homologação.

**Configuração e segredos**

| Chave | Onde fica | Observação |
| --- | --- | --- |
| `ConnectionStrings:Bestia` | Variável de ambiente / cofre | Com `Encrypt=True` e usuário de mínimo privilégio |
| `Seguranca:ChaveMestre` | Cofre de segredos | Base de todas as chaves derivadas; nunca no repositório |
| `Google:ClientId`, `Google:ClientSecret` | Cofre | Criados no console do Google; URL de retorno cadastrada por ambiente |
| `Email:Servidor`, `Porta`, `Usuario`, `Senha`, `Remetente` | Cofre | SMTP de qualquer provedor |
| `Jogo:*` | `appsettings.json` | Valores da `ConfiguracaoDoJogo` que podem variar (tempos), sem segredo |
| `Privacidade:EncarregadoEmail` | `appsettings.json` | Exibido na política |

**Hospedagem**

- A aplicação roda no Kestrel dentro de um contêiner (imagem oficial do .NET) atrás de um proxy reverso que termina o HTTPS e repassa WebSockets; qualquer VM Linux ou serviço de contêiner serve.
- O banco SQL Server fica em serviço gerenciado ou servidor próprio, fora da rede pública, aceitando conexão apenas da aplicação.
- Chaves do `DataProtection` (cookies e tokens) ficam em volume persistente protegido, para sobreviverem a novos deploys sem deslogar todo mundo.
- Primeiro alvo de capacidade: cerca de 50 mesas simultâneas (uns 300 jogadores conectados) em uma máquina de 2 vCPU e 4 GB. É uma estimativa para dimensionar, a ser confirmada em teste de carga na fase 10.

**Operação**

- Endpoint de saúde `/saude` (health checks do ASP.NET Core) para o orquestrador e para monitoramento.
- Logs estruturados em JSON no console (`AddJsonConsole`), coletados pelo ambiente de hospedagem; sem dado pessoal (seção 7).
- Deploy: pipeline compila, testa, gera imagem, aplica o script de migração em etapa própria e só então troca a versão. Como as mesas vivem em memória, o deploy espera as partidas em curso terminarem ou avisa os jogadores com antecedência.
- Backups diários criptografados do banco, retenção de 30 dias, teste de restauração mensal; após uma exclusão de conta, os dados somem dos backups quando o ciclo de 30 dias vence, e isso está dito na política de privacidade.
- Tarefas agendadas (`BackgroundService`): apagar `RegistroDeAcesso` com mais de 6 meses, expirar tokens, avisar e excluir contas inativas, recifrar dados em rotação de chave.

## 15. Roadmap em fases

São 11 fases, cerca de 92 dias úteis para uma pessoa em tempo integral (uns 4,5 meses); duas pessoas, com as fases 1 e 2 em paralelo e a interface separada do servidor, ficam perto de 3 meses. As estimativas são de planejamento e devem ser revistas ao fim da fase 1, quando o motor das regras já estiver provado.

| Fase | Entregas | Critério de aceite | Depende de | Dias úteis |
| --- | --- | --- | --- | --- |
| 0. Fundação | Repositório, solution com 4 camadas, `.editorconfig`, pipeline de CI, `LICENCAS.md`, auditoria de licenças, SQL Server em contêiner, esqueleto do gerador de resources | `dotnet build` e `dotnet test` verdes no CI; pacote fora da lista MIT quebra o build | Nada | 4 |
| 1. Motor do jogo | Domínio completo: baralho, força, mania, naipe obrigatório, montante, baús, reposição, atalhos, ausência; interfaces de relógio e sorteio; bots; simulação | Todos os cenários da seção 9 viram testes verdes; simulação de 1.000 partidas fecha as fichas | Fase 0 | 15 |
| 2. Banco e segurança | `BestiaDbContext`, 17 tabelas, migrações, `CriptografiaAesGcm`, índice cego, rotação de chave, repositórios | Testes de integração com SQL Server real; dado pessoal ilegível no banco; adulteração detectada | Fase 0 | 8 |
| 3. Contas | Cadastro, confirmação de e-mail, login, bloqueio por tentativas, Google, recuperação de senha, consentimento | Fluxos funcionam em desenvolvimento; testes de segurança verdes; conta Google e local com mesmo e-mail se ligam | Fase 2 | 10 |
| 4. Esqueleto de tela e idiomas | `_Layout`, menu lateral esquerdo, tema taverna, telas de conta, resources em pt-BR, en e es, teste de paridade, seletor de idioma | Todas as telas até aqui nos 3 idiomas, sem texto fixo; menu funciona no celular | Fases 0 e 3 | 8 |
| 5. Salas | Lobby, criar sala (pública e com código), sala de espera, preencher com bots | Duas pessoas em navegadores diferentes veem a mesma sala em até 5 s | Fases 3 e 4 | 6 |
| 6. Partida em tempo real | `MesaHub`, mesa em memória com fila, timers no servidor, bots, visão filtrada, gravação por mão, reconexão | Partida completa de 4 pessoas e bots sem travar; fichas gravadas batem com o jogo; segunda aba derruba a primeira | Fases 1, 2 e 5 | 12 |
| 7. Tela da mesa | Porte do protótipo: assentos, cartas, baús, painel de decisão, barras de tempo, animações de fichas e cartas, emotes, diário, responsividade | Mesma experiência do protótipo em celular e desktop; carta virada por ausência e eliminação visíveis | Fase 6 | 10 |
| 8. Regras e perfil | Tela de regras com índice, janela de regras na mesa, perfil com estatísticas e histórico | Critérios de aceite da seção 12 cumpridos | Fases 4 e 6 | 5 |
| 9. LGPD completa | Tela de privacidade, baixar dados, excluir conta com anonimização, revogação, solicitações, tarefas de retenção, auditoria, textos de Termos e Privacidade revisados | Exclusão deixa só o histórico anonimizado; exportação contém tudo que o inventário lista; revisão jurídica concluída | Fases 3 e 6 | 6 |
| 10. Endurecimento e lançamento | Teste de carga, revisão de segurança contra os riscos mais comuns de aplicações web, acessibilidade, cabeçalhos, backups, beta fechado com 10 a 20 pessoas, correções | Metas de capacidade atingidas; nenhum problema grave aberto; backup restaurado com sucesso | Todas | 8 |

**Marcos (pontos de decisão)**

- **Fim da fase 1:** o motor está provado? Se alguma regra ainda gerar dúvida, resolve-se aqui, antes de existir qualquer tela.
- **Fim da fase 6:** primeira partida online jogável (sem acabamento visual); boa hora de um teste com amigos.
- **Fim da fase 9:** revisão jurídica e do encarregado de dados antes de abrir para o público.
- **Fim da fase 10:** liberar o beta e medir antes de ampliar.

**Ordem de trabalho dentro de cada fase:** teste que falha, código mínimo, refatoração, textos nos três idiomas, revisão.

## 16. Riscos e pontos em aberto

**Decisões que dependem de você**

| Nº | Ponto | Padrão adotado neste plano | Efeito se mudar |
| --- | --- | --- | --- |
| 1 | Idade mínima | 18 anos, por declaração | Abaixo disso a LGPD (art. 14) exige consentimento de responsável e tratamento específico; aumenta fases 3 e 9 |
| 2 | Fontes da taverna (Pirata One, Alegreya são SIL OFL, não MIT) | Fontes do sistema | Abrir uma exceção de licença para fontes devolve o visual do protótipo |
| 3 | Montante sobe pela taxa do segundo baú depois que outros já ficaram | Segundo baú só se todos que ficaram ainda cobrem o novo montante | Outra regra muda a máquina de estados e o texto das regras |
| 4 | Quem não cobre o montante pode pegar baú? | Não, porque pegar obriga a ficar | Mudar exige definir o que acontece com quem não consegue pagar |
| 5 | Baú pego fora de ordem (2 e depois 1) | Joga com o último baú aberto | Pode virar regra explícita no texto |
| 6 | Quem desiste ou é eliminado durante uma mão em andamento | Sai ao fim da mão; fichas na mão contam como perdidas | Afeta o ranking e o histórico |
| 7 | Quem paga a hospedagem, e-mail e domínio | A definir | Define a escolha de provedor da seção 14 |
| 8 | Encarregado de dados (DPO) | A nomear antes do lançamento | Obrigatório para publicar a política |

**Riscos técnicos**

| Risco | Impacto | Como reduzir |
| --- | --- | --- |
| Dependência mudar de licença | Quebra da regra MIT | Versões fixas, auditoria no pipeline, `LICENCAS.md` |
| Perda das chaves de criptografia | Dados pessoais irrecuperáveis | Cópia da chave mestra em cofre separado, teste de recuperação na fase 2 |
| Mesas em memória perdem partida se o servidor cair | Frustração dos jogadores | Gravar a cada mão, avisar antes de deploys, planejar reinício de mão na versão seguinte |
| Latência e quedas de conexão móvel | Jogador cai e é eliminado por ausência | Reconexão em até 2 minutos, prazo visível, mensagem clara de ausência; avaliar tempo maior na beta |
| Trapaça combinada (jogadores conversando por fora) | Quebra do jogo às cegas | Fora do alcance técnico; mitigar com salas públicas aleatórias na versão seguinte |
| Múltiplas contas para ganhar vantagem | Ranking falseado | V1 não tem ranking global; reavaliar quando houver |
| Ataques ao login (força bruta, enumeração) | Contas invadidas | Bloqueio por tentativas, limitador de requisições, mensagens idênticas |
| Textos jurídicos errados | Risco legal | Revisão por advogado e pelo encarregado antes do lançamento |

**Fora do plano, para decidir depois:** ranking e temporadas, amigos e convites, salão de espera com chat moderado, aplicativo mobile, outros provedores de login, observadores na mesa, e uma segunda instância do servidor.
