using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Mappings.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AniListMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    TvdbSeason = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeStart = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    AbsoluteOffset = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniListMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SceneEpisodeMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SceneSeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SceneEpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneEpisodeMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SceneMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SceneSeasonNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SceneNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: false),
                    SceneName = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneNames", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AniListMappings_AniListId",
                table: "AniListMappings",
                column: "AniListId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniListMappings_TvdbId_TvdbSeason",
                table: "AniListMappings",
                columns: new[] { "TvdbId", "TvdbSeason" });

            migrationBuilder.CreateIndex(
                name: "IX_SceneEpisodeMappings_TvdbId_SeasonNumber_EpisodeNumber",
                table: "SceneEpisodeMappings",
                columns: new[] { "TvdbId", "SeasonNumber", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SceneMappings_TvdbId_SeasonNumber",
                table: "SceneMappings",
                columns: new[] { "TvdbId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SceneNames_TvdbId_SceneName",
                table: "SceneNames",
                columns: new[] { "TvdbId", "SceneName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AniListMappings");

            migrationBuilder.DropTable(
                name: "SceneEpisodeMappings");

            migrationBuilder.DropTable(
                name: "SceneMappings");

            migrationBuilder.DropTable(
                name: "SceneNames");
        }
    }
}
