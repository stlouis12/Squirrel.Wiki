using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Squirrel.Wiki.Core.Migrations
{
    /// <inheritdoc />
    public partial class ConvertFileIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support altering column types directly, especially for primary keys
            // We need to recreate the tables with the new schema
            
            // Step 1: Drop foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_squirrel_file_versions_squirrel_files_FileId",
                table: "squirrel_file_versions");

            // Step 2: Create temporary tables with new schema
            migrationBuilder.Sql(@"
                CREATE TABLE squirrel_files_new (
                    Id TEXT NOT NULL PRIMARY KEY,
                    FileHash TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    ContentType TEXT NOT NULL,
                    Description TEXT,
                    FolderId INTEGER,
                    StorageProvider TEXT NOT NULL DEFAULT 'Local',
                    UploadedBy TEXT NOT NULL,
                    UploadedOn TEXT NOT NULL,
                    Visibility INTEGER NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL,
                    ThumbnailPath TEXT,
                    CurrentVersion INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (FileHash) REFERENCES squirrel_file_contents(FileHash) ON DELETE RESTRICT,
                    FOREIGN KEY (FolderId) REFERENCES squirrel_folders(Id) ON DELETE RESTRICT
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE squirrel_file_versions_new (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    FileId TEXT NOT NULL,
                    VersionNumber INTEGER NOT NULL,
                    FileHash TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    ChangeDescription TEXT,
                    FOREIGN KEY (FileHash) REFERENCES squirrel_file_contents(FileHash) ON DELETE RESTRICT,
                    FOREIGN KEY (FileId) REFERENCES squirrel_files_new(Id) ON DELETE CASCADE
                );
            ");

            // Step 3: Migrate existing data (if any)
            // Note: This generates new GUIDs for existing files
            migrationBuilder.Sql(@"
                INSERT INTO squirrel_files_new (
                    Id, FileHash, FileName, FilePath, FileSize, ContentType, 
                    Description, FolderId, StorageProvider, UploadedBy, UploadedOn, 
                    Visibility, IsDeleted, ThumbnailPath, CurrentVersion
                )
                SELECT 
                    lower(hex(randomblob(16))), -- Generate new GUID
                    FileHash, FileName, FilePath, FileSize, ContentType,
                    Description, FolderId, StorageProvider, UploadedBy, UploadedOn,
                    Visibility, IsDeleted, ThumbnailPath, CurrentVersion
                FROM squirrel_files;
            ");

            // Step 4: Migrate file versions with new GUIDs
            // Note: This creates a mapping between old int IDs and new GUIDs
            migrationBuilder.Sql(@"
                INSERT INTO squirrel_file_versions_new (
                    FileId, VersionNumber, FileHash, FileSize, CreatedBy, CreatedOn, ChangeDescription
                )
                SELECT 
                    fn.Id, -- New GUID from squirrel_files_new
                    fv.VersionNumber, fv.FileHash, fv.FileSize, fv.CreatedBy, fv.CreatedOn, fv.ChangeDescription
                FROM squirrel_file_versions fv
                INNER JOIN squirrel_files fo ON fv.FileId = fo.Id
                INNER JOIN squirrel_files_new fn ON fo.FileName = fn.FileName 
                    AND fo.FilePath = fn.FilePath 
                    AND fo.UploadedOn = fn.UploadedOn;
            ");

            // Step 5: Drop old tables
            migrationBuilder.DropTable(name: "squirrel_file_versions");
            migrationBuilder.DropTable(name: "squirrel_files");

            // Step 6: Rename new tables to original names
            migrationBuilder.RenameTable(
                name: "squirrel_files_new",
                newName: "squirrel_files");

            migrationBuilder.RenameTable(
                name: "squirrel_file_versions_new",
                newName: "squirrel_file_versions");

            // Step 7: Recreate indexes
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
                name: "IX_squirrel_file_versions_FileHash",
                table: "squirrel_file_versions",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_file_versions_FileId_VersionNumber",
                table: "squirrel_file_versions",
                columns: new[] { "FileId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: Downgrade will result in data loss as GUIDs cannot be converted back to sequential integers
            
            // Drop foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_squirrel_file_versions_squirrel_files_FileId",
                table: "squirrel_file_versions");

            // Recreate tables with old schema
            migrationBuilder.Sql(@"
                CREATE TABLE squirrel_files_old (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    FileHash TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    ContentType TEXT NOT NULL,
                    Description TEXT,
                    FolderId INTEGER,
                    StorageProvider TEXT NOT NULL DEFAULT 'Local',
                    UploadedBy TEXT NOT NULL,
                    UploadedOn TEXT NOT NULL,
                    Visibility INTEGER NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL,
                    ThumbnailPath TEXT,
                    CurrentVersion INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (FileHash) REFERENCES squirrel_file_contents(FileHash) ON DELETE RESTRICT,
                    FOREIGN KEY (FolderId) REFERENCES squirrel_folders(Id) ON DELETE RESTRICT
                );
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE squirrel_file_versions_old (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    FileId INTEGER NOT NULL,
                    VersionNumber INTEGER NOT NULL,
                    FileHash TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    ChangeDescription TEXT,
                    FOREIGN KEY (FileHash) REFERENCES squirrel_file_contents(FileHash) ON DELETE RESTRICT,
                    FOREIGN KEY (FileId) REFERENCES squirrel_files_old(Id) ON DELETE CASCADE
                );
            ");

            // Note: Data migration on downgrade is not implemented as it would lose GUID information
            // If you need to downgrade, you should restore from a backup

            migrationBuilder.DropTable(name: "squirrel_file_versions");
            migrationBuilder.DropTable(name: "squirrel_files");

            migrationBuilder.RenameTable(
                name: "squirrel_files_old",
                newName: "squirrel_files");

            migrationBuilder.RenameTable(
                name: "squirrel_file_versions_old",
                newName: "squirrel_file_versions");

            // Recreate indexes
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
                name: "IX_squirrel_file_versions_FileHash",
                table: "squirrel_file_versions",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_squirrel_file_versions_FileId_VersionNumber",
                table: "squirrel_file_versions",
                columns: new[] { "FileId", "VersionNumber" },
                unique: true);
        }
    }
}
