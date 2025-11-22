using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Squirrel.Wiki.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPageVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "squirrel_pages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "squirrel_pages");
        }
    }
}
