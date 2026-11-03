using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRadar.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryMethod",
                table: "Tools",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryCategory",
                table: "Tools",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryCategory",
                table: "Tools",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_PrimaryCategory",
                table: "Tools",
                column: "PrimaryCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_SecondaryCategory",
                table: "Tools",
                column: "SecondaryCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tools_PrimaryCategory",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_SecondaryCategory",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "CategoryMethod",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "PrimaryCategory",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SecondaryCategory",
                table: "Tools");
        }
    }
}
