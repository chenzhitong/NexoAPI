using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdditionalSigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalSigner",
                table: "Transaction",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalSigner",
                table: "Transaction");
        }
    }
}
