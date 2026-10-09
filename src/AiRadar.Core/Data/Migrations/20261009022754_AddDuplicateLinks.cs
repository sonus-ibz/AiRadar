using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRadar.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuplicateOfId",
                table: "Tools",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "Tools",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UrlKey",
                table: "Tools",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteKey",
                table: "Tools",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_DuplicateOfId",
                table: "Tools",
                column: "DuplicateOfId");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_NameKey",
                table: "Tools",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_UrlKey",
                table: "Tools",
                column: "UrlKey");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_WebsiteKey",
                table: "Tools",
                column: "WebsiteKey");

            migrationBuilder.AddForeignKey(
                name: "FK_Tools_Tools_DuplicateOfId",
                table: "Tools",
                column: "DuplicateOfId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tools_Tools_DuplicateOfId",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_DuplicateOfId",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_NameKey",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_UrlKey",
                table: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Tools_WebsiteKey",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "DuplicateOfId",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "UrlKey",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "WebsiteKey",
                table: "Tools");
        }
    }
}
