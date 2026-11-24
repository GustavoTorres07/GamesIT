using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class RegistroVerificadoYProgreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Bits",
                table: "Jugadores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Experiencia",
                table: "Jugadores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FavoritosCsv",
                table: "Jugadores",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "Jugadores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PartidasGanadas",
                table: "Jugadores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PartidasJugadas",
                table: "Jugadores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RegistrosPendientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorreoElectronico = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    NombreJugador = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContrasenaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenceUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoEnvioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosPendientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosPendientes_CorreoElectronico",
                table: "RegistrosPendientes",
                column: "CorreoElectronico",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosPendientes");

            migrationBuilder.DropColumn(
                name: "Bits",
                table: "Jugadores");

            migrationBuilder.DropColumn(
                name: "Experiencia",
                table: "Jugadores");

            migrationBuilder.DropColumn(
                name: "FavoritosCsv",
                table: "Jugadores");

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "Jugadores");

            migrationBuilder.DropColumn(
                name: "PartidasGanadas",
                table: "Jugadores");

            migrationBuilder.DropColumn(
                name: "PartidasJugadas",
                table: "Jugadores");
        }
    }
}
