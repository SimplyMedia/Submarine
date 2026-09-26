using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: false),
                    EpisodeId = table.Column<int>(type: "integer", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
