using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddParityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedToken",
                table: "SecurityConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CollectionTitle",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAvailability",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TmdbCollectionId",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaInfo",
                table: "MovieFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WriteNfo",
                table: "MediaManagementConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MediaInfo",
                table: "EpisodeFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DelayProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredProtocol = table.Column<int>(type: "INTEGER", nullable: false),
                    UsenetDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    TorrentDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    BypassIfHighestQuality = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Required = table.Column<string>(type: "TEXT", nullable: false),
                    Ignored = table.Column<string>(type: "TEXT", nullable: false),
                    Indexer = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemotePathMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    RemotePath = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemotePathMappings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelayProfiles");

            migrationBuilder.DropTable(
                name: "ReleaseProfiles");

            migrationBuilder.DropTable(
                name: "RemotePathMappings");

            migrationBuilder.DropColumn(
                name: "FeedToken",
                table: "SecurityConfigs");

            migrationBuilder.DropColumn(
                name: "CollectionTitle",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "MinimumAvailability",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "TmdbCollectionId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "MediaInfo",
                table: "MovieFiles");

            migrationBuilder.DropColumn(
                name: "WriteNfo",
                table: "MediaManagementConfigs");

            migrationBuilder.DropColumn(
                name: "MediaInfo",
                table: "EpisodeFiles");
        }
    }
}
