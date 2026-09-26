using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class MediaVersionMonitoringOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaVersionEpisodeMonitorings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaVersionEpisodeMonitorings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaVersionEpisodeMonitorings_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaVersionEpisodeMonitorings_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaVersionSeasonMonitorings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaVersionSeasonMonitorings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaVersionSeasonMonitorings_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaVersionEpisodeMonitorings_EpisodeId",
                table: "MediaVersionEpisodeMonitorings",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaVersionEpisodeMonitorings_MediaVersionId_EpisodeId",
                table: "MediaVersionEpisodeMonitorings",
                columns: new[] { "MediaVersionId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaVersionSeasonMonitorings_MediaVersionId_SeasonNumber",
                table: "MediaVersionSeasonMonitorings",
                columns: new[] { "MediaVersionId", "SeasonNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaVersionEpisodeMonitorings");

            migrationBuilder.DropTable(
                name: "MediaVersionSeasonMonitorings");
        }
    }
}
