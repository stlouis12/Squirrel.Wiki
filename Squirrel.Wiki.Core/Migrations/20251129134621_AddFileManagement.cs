using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Squirrel.Wiki.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFileManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "squirrel_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                name: "squirrel_file_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileId = table.Column<int>(type: "INTEGER", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "squirrel_file_versions");

            migrationBuilder.DropTable(
                name: "squirrel_files");

            migrationBuilder.DropTable(
                name: "squirrel_file_contents");

            migrationBuilder.DropTable(
                name: "squirrel_folders");
        }
    }
}
