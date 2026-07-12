using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddAutomationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blocklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseTitle = table.Column<string>(type: "text", nullable: false),
                    Guid = table.Column<string>(type: "text", nullable: true),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    Indexer = table.Column<string>(type: "text", nullable: true),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Blocklist_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Blocklist_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DownloadConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EnableFailedDownloadHandling = table.Column<bool>(type: "boolean", nullable: false),
                    RedownloadFailedReleases = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveFailedFromClient = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexerConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RssSyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumAgeMinutes = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaximumSizeMb = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseGroupQualityOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseGroupQualityOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blocklist_MovieId",
                table: "Blocklist",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocklist_SeriesId",
                table: "Blocklist",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroupQualityOverrides_ReleaseGroup",
                table: "ReleaseGroupQualityOverrides",
                column: "ReleaseGroup",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blocklist");

            migrationBuilder.DropTable(
                name: "DownloadConfigs");

            migrationBuilder.DropTable(
                name: "IndexerConfigs");

            migrationBuilder.DropTable(
                name: "ReleaseGroupQualityOverrides");

            migrationBuilder.DropTable(
                name: "SecurityConfigs");
        }
    }
}
