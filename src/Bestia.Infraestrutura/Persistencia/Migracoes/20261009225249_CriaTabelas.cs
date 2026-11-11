using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bestia.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CriaTabelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jogador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApelidoCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ApelidoHash = table.Column<byte[]>(type: "varbinary(900)", nullable: true),
                    EmailCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    EmailHash = table.Column<byte[]>(type: "varbinary(900)", nullable: true),
                    SenhaHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailConfirmadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Idioma = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CorDoAvatar = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FalhasDeLogin = table.Column<int>(type: "int", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoAcessoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarimboDeSeguranca = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jogador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistroDeAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OcorreuEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroDeAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Consentimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Versao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AceitoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpHash = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    RevogadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consentimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consentimento_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginExterno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provedor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdExternoHash = table.Column<byte[]>(type: "varbinary(900)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginExterno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginExterno_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistroDeAcesso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    OcorreuEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroDeAcesso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistroDeAcesso_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sala",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    CriadaPorJogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaximoDeJogadores = table.Column<int>(type: "int", nullable: false),
                    EhPrivada = table.Column<bool>(type: "bit", nullable: false),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sala_Jogador_CriadaPorJogadorId",
                        column: x => x.CriadaPorJogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SolicitacaoPrivacidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrazoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacaoPrivacidade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacaoPrivacidade_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenDeConta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenDeConta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenDeConta_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Partida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IniciadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuantidadeDeJogadores = table.Column<int>(type: "int", nullable: false),
                    VencedorJogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Situacao = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partida_Jogador_VencedorJogadorId",
                        column: x => x.VencedorJogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Partida_Sala_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaJogador",
                columns: table => new
                {
                    SalaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntrouEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SaiuEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaJogador", x => new { x.SalaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_SalaJogador_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalaJogador_Sala_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    AssentoDoDistribuidor = table.Column<int>(type: "int", nullable: false),
                    Mania = table.Column<byte>(type: "tinyint", nullable: false),
                    Montante = table.Column<int>(type: "int", nullable: false),
                    TaxaDeBau = table.Column<int>(type: "int", nullable: false),
                    Desfecho = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mao_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartidaJogador",
                columns: table => new
                {
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Assento = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeDoBot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FichasIniciais = table.Column<int>(type: "int", nullable: false),
                    FichasFinais = table.Column<int>(type: "int", nullable: false),
                    Colocacao = table.Column<int>(type: "int", nullable: true),
                    MotivoDaSaida = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartidaJogador", x => new { x.PartidaId, x.Assento });
                    table.CheckConstraint("CK_PartidaJogador_FichasFinais", "[FichasFinais] >= 0");
                    table.CheckConstraint("CK_PartidaJogador_FichasIniciais", "[FichasIniciais] >= 0");
                    table.ForeignKey(
                        name: "FK_PartidaJogador_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PartidaJogador_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bau",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    PegoPorAssento = table.Column<int>(type: "int", nullable: true),
                    TaxaPaga = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bau", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bau_Mao_MaoId",
                        column: x => x.MaoId,
                        principalTable: "Mao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaoJogador",
                columns: table => new
                {
                    MaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Assento = table.Column<int>(type: "int", nullable: false),
                    Decisao = table.Column<byte>(type: "tinyint", nullable: false),
                    BausPegos = table.Column<int>(type: "int", nullable: false),
                    RodadasVencidas = table.Column<int>(type: "int", nullable: false),
                    ReposicaoPaga = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaoJogador", x => new { x.MaoId, x.Assento });
                    table.ForeignKey(
                        name: "FK_MaoJogador_Mao_MaoId",
                        column: x => x.MaoId,
                        principalTable: "Mao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimentoDeFicha",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Assento = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Valor = table.Column<int>(type: "int", nullable: false),
                    OcorreuEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentoDeFicha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentoDeFicha_Mao_MaoId",
                        column: x => x.MaoId,
                        principalTable: "Mao",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovimentoDeFicha_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rodada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    AbridorAssento = table.Column<int>(type: "int", nullable: false),
                    VencedorAssento = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rodada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rodada_Mao_MaoId",
                        column: x => x.MaoId,
                        principalTable: "Mao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jogada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RodadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Assento = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Naipe = table.Column<byte>(type: "tinyint", nullable: false),
                    Valor = table.Column<byte>(type: "tinyint", nullable: false),
                    FoiAutomatica = table.Column<bool>(type: "bit", nullable: false),
                    FoiVirada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jogada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jogada_Rodada_RodadaId",
                        column: x => x.RodadaId,
                        principalTable: "Rodada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bau_MaoId_Numero",
                table: "Bau",
                columns: new[] { "MaoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consentimento_JogadorId",
                table: "Consentimento",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogada_RodadaId",
                table: "Jogada",
                column: "RodadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_ApelidoHash",
                table: "Jogador",
                column: "ApelidoHash",
                unique: true,
                filter: "[ApelidoHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_EmailHash",
                table: "Jogador",
                column: "EmailHash",
                unique: true,
                filter: "[EmailHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginExterno_JogadorId",
                table: "LoginExterno",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginExterno_Provedor_IdExternoHash",
                table: "LoginExterno",
                columns: new[] { "Provedor", "IdExternoHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mao_PartidaId_Numero",
                table: "Mao",
                columns: new[] { "PartidaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentoDeFicha_MaoId",
                table: "MovimentoDeFicha",
                column: "MaoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentoDeFicha_PartidaId",
                table: "MovimentoDeFicha",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_SalaId",
                table: "Partida",
                column: "SalaId");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_VencedorJogadorId",
                table: "Partida",
                column: "VencedorJogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PartidaJogador_JogadorId",
                table: "PartidaJogador",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDeAcesso_JogadorId",
                table: "RegistroDeAcesso",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDeAcesso_OcorreuEm",
                table: "RegistroDeAcesso",
                column: "OcorreuEm");

            migrationBuilder.CreateIndex(
                name: "IX_Rodada_MaoId_Numero",
                table: "Rodada",
                columns: new[] { "MaoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sala_Codigo",
                table: "Sala",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sala_CriadaPorJogadorId",
                table: "Sala",
                column: "CriadaPorJogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaJogador_JogadorId",
                table: "SalaJogador",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacaoPrivacidade_JogadorId",
                table: "SolicitacaoPrivacidade",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenDeConta_JogadorId",
                table: "TokenDeConta",
                column: "JogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bau");

            migrationBuilder.DropTable(
                name: "Consentimento");

            migrationBuilder.DropTable(
                name: "Jogada");

            migrationBuilder.DropTable(
                name: "LoginExterno");

            migrationBuilder.DropTable(
                name: "MaoJogador");

            migrationBuilder.DropTable(
                name: "MovimentoDeFicha");

            migrationBuilder.DropTable(
                name: "PartidaJogador");

            migrationBuilder.DropTable(
                name: "RegistroDeAcesso");

            migrationBuilder.DropTable(
                name: "RegistroDeAuditoria");

            migrationBuilder.DropTable(
                name: "SalaJogador");

            migrationBuilder.DropTable(
                name: "SolicitacaoPrivacidade");

            migrationBuilder.DropTable(
                name: "TokenDeConta");

            migrationBuilder.DropTable(
                name: "Rodada");

            migrationBuilder.DropTable(
                name: "Mao");

            migrationBuilder.DropTable(
                name: "Partida");

            migrationBuilder.DropTable(
                name: "Sala");

            migrationBuilder.DropTable(
                name: "Jogador");
        }
    }
}
