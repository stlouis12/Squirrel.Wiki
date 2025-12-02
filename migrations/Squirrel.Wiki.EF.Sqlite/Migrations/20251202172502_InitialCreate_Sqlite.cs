using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Squirrel.Wiki.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_Sqlite : Migration
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
                name: "squirrel_data_protection_keys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: true),
                    Xml = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_data_protection_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_file_contents",
                columns: table => new
                {
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Local"),
                    ReferenceCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_file_contents", x => x.FileHash);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_folders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ParentFolderId = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_folders_squirrel_folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "squirrel_folders",
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
                name: "squirrel_plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PluginType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsConfigured = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoadOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCorePlugin = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_site_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsFromEnvironment = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnvironmentVariableName = table.Column<string>(type: "TEXT", nullable: true)
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
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LockedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PasswordResetToken = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PasswordResetExpiry = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPasswordChangeOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEditor = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "squirrel_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FolderId = table.Column<int>(type: "INTEGER", nullable: true),
                    StorageProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Local"),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_files_squirrel_file_contents_FileHash",
                        column: x => x.FileHash,
                        principalTable: "squirrel_file_contents",
                        principalColumn: "FileHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_squirrel_files_squirrel_folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "squirrel_folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_plugin_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsFromEnvironment = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnvironmentVariableName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsSecret = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_plugin_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_plugin_settings_squirrel_plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "squirrel_plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_plugin_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PluginName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Changes = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_plugin_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_plugin_audit_logs_squirrel_plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "squirrel_plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_squirrel_plugin_audit_logs_squirrel_users_UserId",
                        column: x => x.UserId,
                        principalTable: "squirrel_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "squirrel_user_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_user_roles_squirrel_users_UserId",
                        column: x => x.UserId,
                        principalTable: "squirrel_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "squirrel_file_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangeDescription = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_squirrel_file_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_squirrel_file_versions_squirrel_file_contents_FileHash",
                        column: x => x.FileHash,
                        principalTable: "squirrel_file_contents",
                        principalColumn: "FileHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_squirrel_file_versions_squirrel_files_FileId",
                        column: x => x.FileId,
                        principalTable: "squirrel_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_categories_DisplayOrder",
                table: "squirrel_categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_categories_Name",
                table: "squirrel_categories",
                column: "Name");

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
                name: "IX_squirrel_file_contents_StorageProvider",
                table: "squirrel_file_contents",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_file_versions_FileHash",
                table: "squirrel_file_versions",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_file_versions_FileId_VersionNumber",
                table: "squirrel_file_versions",
                columns: new[] { "FileId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_FileHash",
                table: "squirrel_files",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_FileName",
                table: "squirrel_files",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_FilePath",
                table: "squirrel_files",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_FolderId",
                table: "squirrel_files",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_IsDeleted",
                table: "squirrel_files",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_StorageProvider",
                table: "squirrel_files",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_files_UploadedOn",
                table: "squirrel_files",
                column: "UploadedOn");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_folders_IsDeleted",
                table: "squirrel_folders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_folders_ParentFolderId",
                table: "squirrel_folders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_folders_Slug",
                table: "squirrel_folders",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_DisplayOrder",
                table: "squirrel_menus",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_MenuType_DisplayOrder",
                table: "squirrel_menus",
                columns: new[] { "MenuType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_MenuType_IsEnabled",
                table: "squirrel_menus",
                columns: new[] { "MenuType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_menus_MenuType_IsEnabled_DisplayOrder",
                table: "squirrel_menus",
                columns: new[] { "MenuType", "IsEnabled", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_page_contents_EditedBy_EditedOn",
                table: "squirrel_page_contents",
                columns: new[] { "EditedBy", "EditedOn" });

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
                name: "IX_squirrel_pages_CategoryId_IsDeleted_Title",
                table: "squirrel_pages",
                columns: new[] { "CategoryId", "IsDeleted", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_CreatedBy_IsDeleted_CreatedOn",
                table: "squirrel_pages",
                columns: new[] { "CreatedBy", "IsDeleted", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_IsDeleted",
                table: "squirrel_pages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_IsDeleted_Title",
                table: "squirrel_pages",
                columns: new[] { "IsDeleted", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_pages_ModifiedBy_IsDeleted_ModifiedOn",
                table: "squirrel_pages",
                columns: new[] { "ModifiedBy", "IsDeleted", "ModifiedOn" });

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
                name: "IX_squirrel_plugin_audit_logs_Operation",
                table: "squirrel_plugin_audit_logs",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugin_audit_logs_PluginId",
                table: "squirrel_plugin_audit_logs",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugin_audit_logs_PluginId_Timestamp",
                table: "squirrel_plugin_audit_logs",
                columns: new[] { "PluginId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugin_audit_logs_Timestamp",
                table: "squirrel_plugin_audit_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugin_audit_logs_UserId",
                table: "squirrel_plugin_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugin_settings_PluginId_Key",
                table: "squirrel_plugin_settings",
                columns: new[] { "PluginId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_plugins_PluginId",
                table: "squirrel_plugins",
                column: "PluginId",
                unique: true);

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
                name: "IX_squirrel_user_roles_UserId",
                table: "squirrel_user_roles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_user_roles_UserId_Role",
                table: "squirrel_user_roles",
                columns: new[] { "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_Email",
                table: "squirrel_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_ExternalId",
                table: "squirrel_users",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_IsActive_Username",
                table: "squirrel_users",
                columns: new[] { "IsActive", "Username" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_IsAdmin_Username",
                table: "squirrel_users",
                columns: new[] { "IsAdmin", "Username" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_IsEditor_Username",
                table: "squirrel_users",
                columns: new[] { "IsEditor", "Username" });

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_Provider",
                table: "squirrel_users",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_users_Username",
                table: "squirrel_users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "squirrel_data_protection_keys");

            migrationBuilder.DropTable(
                name: "squirrel_file_versions");

            migrationBuilder.DropTable(
                name: "squirrel_menus");

            migrationBuilder.DropTable(
                name: "squirrel_page_contents");

            migrationBuilder.DropTable(
                name: "squirrel_page_tags");

            migrationBuilder.DropTable(
                name: "squirrel_plugin_audit_logs");

            migrationBuilder.DropTable(
                name: "squirrel_plugin_settings");

            migrationBuilder.DropTable(
                name: "squirrel_site_configurations");

            migrationBuilder.DropTable(
                name: "squirrel_user_roles");

            migrationBuilder.DropTable(
                name: "squirrel_files");

            migrationBuilder.DropTable(
                name: "squirrel_pages");

            migrationBuilder.DropTable(
                name: "squirrel_tags");

            migrationBuilder.DropTable(
                name: "squirrel_plugins");

            migrationBuilder.DropTable(
                name: "squirrel_users");

            migrationBuilder.DropTable(
                name: "squirrel_file_contents");

            migrationBuilder.DropTable(
                name: "squirrel_folders");

            migrationBuilder.DropTable(
                name: "squirrel_categories");
        }
    }
}
