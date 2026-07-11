using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaManagementConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    UseHardlinks = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportExtraFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumFreeSpaceMb = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaManagementConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    RenameEpisodes = table.Column<bool>(type: "INTEGER", nullable: false),
                    StandardEpisodeFormat = table.Column<string>(type: "TEXT", nullable: false),
                    AnimeEpisodeFormat = table.Column<string>(type: "TEXT", nullable: false),
                    MovieFormat = table.Column<string>(type: "TEXT", nullable: false),
                    SeriesFolderFormat = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonFolderFormat = table.Column<string>(type: "TEXT", nullable: false),
                    MovieFolderFormat = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaManagementConfigs");

            migrationBuilder.DropTable(
                name: "NamingConfigs");
        }
    }
}
