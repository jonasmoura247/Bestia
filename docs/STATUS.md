# Bestia — estado da execução do plano

Data: 09/10/2026 (repositório publicado em 10/11/2026). Veja também `OPERACAO.md` e `../LICENCAS.md`.

## Fases do plano

| Fase | Situação | Observações |
| --- | --- | --- |
| 0. Fundação | feita | Solução com 5 projetos de código e 4 de teste, `.editorconfig`, `Directory.Build.props` (avisos como erro), `LICENCAS.md`, auditoria de licenças como teste, `Dockerfile`, CI em `.github/workflows/ci.yml` (nunca executado no GitHub) |
| 1. Motor do jogo | feita | Domínio completo, bots, simulação de 1.000 partidas com fichas fechando |
| 2. Banco e segurança | feita | 17 tabelas, migração, AES-256-GCM, índice cego, rotação de chave, testes contra SQL Server real |
| 3. Contas | feita | Cadastro, confirmação, login com bloqueio, recuperação de senha, Google, consentimento |
| 4. Esqueleto e idiomas | feita | Layout, menu lateral, tema, pt-BR/en/es, teste de paridade e de texto solto |
| 5. Salas | feita | Lobby, criar (pública/privada), entrar por código, sala de espera, bots |
| 6. Partida em tempo real | feita | `MesaHub`, mesa em memória, prazos no servidor, visão filtrada, gravação por mão, reconexão |
| 7. Tela da mesa | feita | Verificada de verdade no Edge (cadastro, sala, bots, decisões, cartas, regras, emote, celular) |
| 8. Regras e perfil | feita | 12 seções, janela na mesa, perfil com estatísticas e histórico |
| 9. LGPD completa | feita | Meus dados, baixar JSON, alterar apelido/e-mail, revogar, excluir (com anonimização), pedidos, rotina diária |
| 10. Endurecimento | parcial | Cabeçalhos, limite de requisições, HSTS, health check, carga simulada, Docker. Faltam itens que dependem de pessoas (abaixo) |

## Qualidade

- `dotnet test`: **~1.260 testes**, todos passando, **100% de linhas e ramos** nos módulos `Dominio`, `Aplicacao`,
  `Recursos`, `Infraestrutura` e `Web` (o build falha abaixo disso).
- Exclusões de cobertura, sempre justificadas: `Program.cs`, `*.cshtml`, migrações geradas, o adaptador SMTP
  (fino, sobre o `SmtpClient`) e a chamada de migração ao subir (precisa de SQL Server real).
- JavaScript do navegador (`mesa.js`, `lobby.js`, `site.js`): sem teste automatizado; checado com `node --check` e
  exercitado no Edge por roteiro. Não conta nos 100%.
- `dotnet format --verify-no-changes` limpo.
- Carga: 50 mesas de 6 jogadores (300 jogadores simulados) terminam sem erro (`CargaTestes`).
  É carga em memória com relógio simulado; **não substitui** um teste com rede e navegadores reais.

## Desvios do plano (e por quê)

| Plano | Feito | Motivo |
| --- | --- | --- |
| `.resx` gerados por uma ferramenta | `textos-mestre.json` embutido + classe `Textos` + testes de paridade | Mesma garantia (3 idiomas, chave tipada, sem texto solto) com menos peças |
| Rota `/partida/{id}` separada | Uma página `/salas/{codigo}` que alterna espera, mesa e fim | A conexão do SignalR é a mesma; evita recarregar |
| `JogadorId` nas tabelas da mão | `Assento` | Bots não têm conta; `PartidaJogador` liga assento a pessoa |
| `IEmbaralhador` + `ISorteador` | só `ISorteador` | Embaralhar é feito em cima do sorteio |
| `IEstrategiaDeBot` | classe `EstrategiaDeBot` | Uma implementação só |
| Modelos de persistência na Infraestrutura | em `Aplicacao/Modelos`, com porta genérica `IRepositorio` | Casos de uso testáveis sem EF e sem um repositório por entidade |
| Bootstrap e Bootstrap Icons | CSS próprio (`taverna.css`) e emojis | Menos dependências, nenhuma licença extra |
| Colunas do plano | `Sala.EhPrivada` e `Jogador.CarimboDeSeguranca` adicionadas | Sala privada por código; invalidar sessões ao trocar a senha |

## Pontos que dependem de decisão ou de pessoas

1. ~~Licenças não-MIT transitivas~~ **Decidido**: exceção aceita (SQL Server Express mantido). Ver `../LICENCAS.md`.
2. **Google real**: o fluxo foi testado com um provedor simulado; falta criar o `ClientId`/`ClientSecret` e
   cadastrar a URL de retorno (`/signin-google`).
3. **E-mail real**: o envio por SMTP não foi exercitado (em desenvolvimento os e-mails vão para o log).
4. **Jurídico** (MVP): decidido manter os textos de referência, com o aviso "sujeito a revisão jurídica" visível,
   e um e-mail de encarregado provisório (`privacidade@bestia.example`, em `appsettings.json`). Trocar antes de
   abrir para o público; advogado e DPO continuam pendentes.
5. ~~Regras em aberto 3 a 6~~ **Decidido**: mantidos os padrões do plano.
6. **Fontes da taverna**: usam fontes do sistema (Georgia) por causa da regra MIT.
7. **Pontos 7 e 8 da seção 16**: quem paga hospedagem/e-mail/domínio e a nomeação do encarregado.
8. **Beta fechado** com 10 a 20 pessoas, teste de carga com rede real, auditoria de acessibilidade com leitor
   de tela e revisão de segurança por terceiros: fora do alcance de testes automatizados.
9. **Pipeline de CI**: o arquivo existe e roda a cada push, mas os testes de Integração e de Web precisam do SQL Server em contêiner; conferir a primeira execução no GitHub.
10. **Segredos**: gerar e guardar `Seguranca__ChaveMestra` e `Seguranca__ChaveDeIndice` em cofre, com cópia
    separada; perder a chave mestra torna os dados pessoais irrecuperáveis.

## Riscos conhecidos

- Mesas em memória: uma queda do servidor perde a mão em curso (a partida fica `Interrompida`).
- Cadastro revela se um e-mail já existe (`Erro_EmailEmUso`); trocar por resposta genérica se isso importar.
- Uma instância só: para escalar é preciso o backplane Redis e afinidade de sessão por mesa.
- O limite de requisições é por IP; atrás de proxy, configurar o repasse do IP real.
