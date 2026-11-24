using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class UltimaActividadYMotivoPartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoFinalizacion",
                table: "Partidas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaActividadUtc",
                table: "Partidas",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Estado_UltimaActividad",
                table: "Partidas",
                columns: new[] { "Estado", "UltimaActividadUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Partidas_Estado_UltimaActividad",
                table: "Partidas");

            migrationBuilder.DropColumn(
                name: "MotivoFinalizacion",
                table: "Partidas");

            migrationBuilder.DropColumn(
                name: "UltimaActividadUtc",
                table: "Partidas");
        }
    }
}
