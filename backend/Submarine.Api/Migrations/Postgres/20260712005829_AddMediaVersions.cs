using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddMediaVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_EpisodeFiles_EpisodeFileId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_MovieFiles_MovieFileId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_MovieFileId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_EpisodeFileId",
                table: "Episodes");

            migrationBuilder.DropColumn(
                name: "LanguageProfileId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "QualityProfileId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "LanguageProfileId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "MovieFileId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "QualityProfileId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "EpisodeFileId",
                table: "Episodes");

            migrationBuilder.AddColumn<int>(
                name: "MediaVersionId",
                table: "TrackedDownloads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaVersionId",
                table: "MovieFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediaVersionId",
                table: "EpisodeFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EpisodeFileEpisodes",
                columns: table => new
                {
                    EpisodesId = table.Column<int>(type: "integer", nullable: false),
                    FilesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodeFileEpisodes", x => new { x.EpisodesId, x.FilesId });
                    table.ForeignKey(
                        name: "FK_EpisodeFileEpisodes_EpisodeFiles_FilesId",
                        column: x => x.FilesId,
                        principalTable: "EpisodeFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EpisodeFileEpisodes_Episodes_EpisodesId",
                        column: x => x.EpisodesId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    QualityProfileId = table.Column<int>(type: "integer", nullable: false),
                    LanguageProfileId = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Versions_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Versions_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_MediaVersionId",
                table: "TrackedDownloads",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieFiles_MediaVersionId",
                table: "MovieFiles",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieFiles_MovieId",
                table: "MovieFiles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeFiles_MediaVersionId",
                table: "EpisodeFiles",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeFileEpisodes_FilesId",
                table: "EpisodeFileEpisodes",
                column: "FilesId");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_MovieId",
                table: "Versions",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_SeriesId",
                table: "Versions",
                column: "SeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_EpisodeFiles_Versions_MediaVersionId",
                table: "EpisodeFiles",
                column: "MediaVersionId",
                principalTable: "Versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieFiles_Movies_MovieId",
                table: "MovieFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieFiles_Versions_MediaVersionId",
                table: "MovieFiles",
                column: "MediaVersionId",
                principalTable: "Versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedDownloads_Versions_MediaVersionId",
                table: "TrackedDownloads",
                column: "MediaVersionId",
                principalTable: "Versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EpisodeFiles_Versions_MediaVersionId",
                table: "EpisodeFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieFiles_Movies_MovieId",
                table: "MovieFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieFiles_Versions_MediaVersionId",
                table: "MovieFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedDownloads_Versions_MediaVersionId",
                table: "TrackedDownloads");

            migrationBuilder.DropTable(
                name: "EpisodeFileEpisodes");

            migrationBuilder.DropTable(
                name: "Versions");

            migrationBuilder.DropIndex(
                name: "IX_TrackedDownloads_MediaVersionId",
                table: "TrackedDownloads");

            migrationBuilder.DropIndex(
                name: "IX_MovieFiles_MediaVersionId",
                table: "MovieFiles");

            migrationBuilder.DropIndex(
                name: "IX_MovieFiles_MovieId",
                table: "MovieFiles");

            migrationBuilder.DropIndex(
                name: "IX_EpisodeFiles_MediaVersionId",
                table: "EpisodeFiles");

            migrationBuilder.DropColumn(
                name: "MediaVersionId",
                table: "TrackedDownloads");

            migrationBuilder.DropColumn(
                name: "MediaVersionId",
                table: "MovieFiles");

            migrationBuilder.DropColumn(
                name: "MediaVersionId",
                table: "EpisodeFiles");

            migrationBuilder.AddColumn<int>(
                name: "LanguageProfileId",
                table: "Series",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Series",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QualityProfileId",
                table: "Series",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LanguageProfileId",
                table: "Movies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MovieFileId",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Movies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QualityProfileId",
                table: "Movies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EpisodeFileId",
                table: "Episodes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_MovieFileId",
                table: "Movies",
                column: "MovieFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_EpisodeFileId",
                table: "Episodes",
                column: "EpisodeFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_EpisodeFiles_EpisodeFileId",
                table: "Episodes",
                column: "EpisodeFileId",
                principalTable: "EpisodeFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_MovieFiles_MovieFileId",
                table: "Movies",
                column: "MovieFileId",
                principalTable: "MovieFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
