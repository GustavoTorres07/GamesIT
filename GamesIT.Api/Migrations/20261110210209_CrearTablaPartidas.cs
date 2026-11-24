using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaPartidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Partidas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoJuego = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DesafioAhorcadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaInicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFinalizacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CantidadIntentos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CantidadErrores = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DuracionSegundos = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partidas", x => x.Id);
                    table.CheckConstraint("CK_Partidas_DesafioAhorcado", "([TipoJuego] = N'Ahorcado' AND [DesafioAhorcadoId] IS NOT NULL) OR ([TipoJuego] <> N'Ahorcado' AND [DesafioAhorcadoId] IS NULL)");
                    table.CheckConstraint("CK_Partidas_Duracion", "[DuracionSegundos] IS NULL OR [DuracionSegundos] >= 0");
                    table.CheckConstraint("CK_Partidas_Fechas", "[FechaFinalizacionUtc] IS NULL OR [FechaFinalizacionUtc] >= [FechaInicioUtc]");
                    table.CheckConstraint("CK_Partidas_IntentosErrores", "[CantidadIntentos] >= 0 AND [CantidadErrores] >= 0 AND [CantidadErrores] <= [CantidadIntentos]");
                    table.ForeignKey(
                        name: "FK_Partidas_HistorialDesafiosAhorcado_DesafioAhorcadoId",
                        column: x => x.DesafioAhorcadoId,
                        principalTable: "HistorialDesafiosAhorcado",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Partidas_Jugadores_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Jugadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugador_FechaInicio",
                table: "Partidas",
                columns: new[] { "JugadorId", "FechaInicioUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Partidas_DesafioAhorcado",
                table: "Partidas",
                column: "DesafioAhorcadoId",
                unique: true,
                filter: "[DesafioAhorcadoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Partidas");
        }
    }
}
