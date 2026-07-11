using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UseHardlinks = table.Column<bool>(type: "boolean", nullable: false),
                    ImportExtraFiles = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumFreeSpaceMb = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaManagementConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RenameEpisodes = table.Column<bool>(type: "boolean", nullable: false),
                    StandardEpisodeFormat = table.Column<string>(type: "text", nullable: false),
                    AnimeEpisodeFormat = table.Column<string>(type: "text", nullable: false),
                    MovieFormat = table.Column<string>(type: "text", nullable: false),
                    SeriesFolderFormat = table.Column<string>(type: "text", nullable: false),
                    SeasonFolderFormat = table.Column<string>(type: "text", nullable: false),
                    MovieFolderFormat = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
