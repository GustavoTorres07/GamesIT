using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class CrearRecompensasPartidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecompensasPartidas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExperienciaOtorgada = table.Column<long>(type: "bigint", nullable: false),
                    BitsBase = table.Column<long>(type: "bigint", nullable: false),
                    BitsPorNiveles = table.Column<long>(type: "bigint", nullable: false),
                    BitsTotales = table.Column<long>(type: "bigint", nullable: false),
                    PuntosOtorgados = table.Column<long>(type: "bigint", nullable: false),
                    NivelAnterior = table.Column<int>(type: "int", nullable: false),
                    NivelNuevo = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecompensasPartidas", x => x.Id);
                    table.CheckConstraint("CK_Recompensas_NoNegativas", "[ExperienciaOtorgada] >= 0 AND [BitsBase] >= 0 AND [BitsPorNiveles] >= 0 AND [BitsTotales] >= 0 AND [PuntosOtorgados] >= 0 AND [NivelNuevo] >= [NivelAnterior]");
                    table.ForeignKey(
                        name: "FK_RecompensasPartidas_Jugadores_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Jugadores",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecompensasPartidas_Partidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recompensas_Jugador_Fecha",
                table: "RecompensasPartidas",
                columns: new[] { "JugadorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Recompensas_Partida",
                table: "RecompensasPartidas",
                column: "PartidaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecompensasPartidas");
        }
    }
}
