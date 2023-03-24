using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoAPI.Migrations
{
    /// <inheritdoc />
    public partial class AccountPublicKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicKeys",
                table: "Account",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicKeys",
                table: "Account");
        }
    }
}
