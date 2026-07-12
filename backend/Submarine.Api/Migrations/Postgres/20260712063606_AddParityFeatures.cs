using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
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
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CollectionTitle",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAvailability",
                table: "Movies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TmdbCollectionId",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaInfo",
                table: "MovieFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WriteNfo",
                table: "MediaManagementConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MediaInfo",
                table: "EpisodeFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DelayProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PreferredProtocol = table.Column<int>(type: "integer", nullable: false),
                    UsenetDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    TorrentDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    BypassIfHighestQuality = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Required = table.Column<List<string>>(type: "text[]", nullable: false),
                    Ignored = table.Column<List<string>>(type: "text[]", nullable: false),
                    Indexer = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemotePathMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Host = table.Column<string>(type: "text", nullable: false),
                    RemotePath = table.Column<string>(type: "text", nullable: false),
                    LocalPath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
