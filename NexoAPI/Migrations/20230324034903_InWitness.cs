using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoAPI.Migrations
{
    /// <inheritdoc />
    public partial class InWitness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InWitness",
                table: "SignResult",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InWitness",
                table: "SignResult");
        }
    }
}
