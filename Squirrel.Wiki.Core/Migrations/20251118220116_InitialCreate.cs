using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Squirrel.Wiki.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "squirrel_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_categories_squirrel_categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "squirrel_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MenuType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Markup = table.Column<string>(type: "TEXT", nullable: true),
                    FooterLeftZone = table.Column<string>(type: "TEXT", nullable: true),
                    FooterRightZone = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_site_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_site_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEditor = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_pages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_pages_squirrel_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "squirrel_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_page_contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    EditedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EditedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeComment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_page_contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_page_contents_squirrel_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "squirrel_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_page_tags",
                columns: table => new
                {
                    PageId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_page_tags", x => new { x.PageId, x.TagId });
                    table.ForeignKey(
                        name: "FK_squirrel_page_tags_squirrel_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "squirrel_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_squirrel_page_tags_squirrel_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "squirrel_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_categories_DisplayOrder",
                table: "squirrel_categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_categories_ParentCategoryId",
                table: "squirrel_categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_categories_ParentCategoryId_Slug",
                table: "squirrel_categories",
                columns: new[] { "ParentCategoryId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_DisplayOrder",
                table: "squirrel_menus",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_MenuType_IsEnabled",
                table: "squirrel_menus",
                columns: new[] { "MenuType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_page_contents_EditedOn",
                table: "squirrel_page_contents",
                column: "EditedOn");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_page_contents_PageId",
                table: "squirrel_page_contents",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_page_contents_PageId_VersionNumber",
                table: "squirrel_page_contents",
                columns: new[] { "PageId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_page_tags_TagId",
                table: "squirrel_page_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_CategoryId",
                table: "squirrel_pages",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_IsDeleted",
                table: "squirrel_pages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_Slug",
                table: "squirrel_pages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_Title",
                table: "squirrel_pages",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_site_configurations_Key",
                table: "squirrel_site_configurations",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_tags_Name",
                table: "squirrel_tags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_tags_NormalizedName",
                table: "squirrel_tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_Email",
                table: "squirrel_users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_ExternalId",
                table: "squirrel_users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_Username",
                table: "squirrel_users",
                column: "Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "squirrel_menus");

            migrationBuilder.DropTable(
                name: "squirrel_page_contents");

            migrationBuilder.DropTable(
                name: "squirrel_page_tags");

            migrationBuilder.DropTable(
                name: "squirrel_site_configurations");

            migrationBuilder.DropTable(
                name: "squirrel_users");

            migrationBuilder.DropTable(
                name: "squirrel_pages");

            migrationBuilder.DropTable(
                name: "squirrel_tags");

            migrationBuilder.DropTable(
                name: "squirrel_categories");
        }
    }
}
