using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class CrearHistorialAhorcado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialDesafiosAhorcado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PalabraNormalizada = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Pista = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaGeneracionUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialDesafiosAhorcado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialDesafiosAhorcado_Jugadores_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Jugadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_HistorialAhorcado_Jugador_Palabra",
                table: "HistorialDesafiosAhorcado",
                columns: new[] { "JugadorId", "PalabraNormalizada" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialDesafiosAhorcado");
        }
    }
}
