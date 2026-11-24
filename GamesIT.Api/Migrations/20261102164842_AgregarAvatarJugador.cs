using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamesIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAvatarJugador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarId",
                table: "Jugadores",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "bytefox");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarId",
                table: "Jugadores");
        }
    }
}
