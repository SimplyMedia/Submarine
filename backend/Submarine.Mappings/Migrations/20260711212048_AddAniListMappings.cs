using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Mappings.Migrations
{
    /// <inheritdoc />
    public partial class AddAniListMappings : Migration
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

            migrationBuilder.CreateIndex(
                name: "IX_AniListMappings_AniListId",
                table: "AniListMappings",
                column: "AniListId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniListMappings_TvdbId",
                table: "AniListMappings",
                column: "TvdbId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AniListMappings");
        }
    }
}
