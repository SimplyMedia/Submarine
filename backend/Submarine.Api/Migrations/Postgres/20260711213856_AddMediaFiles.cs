using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddMediaFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MovieFileId",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EpisodeFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<int[]>(type: "integer[]", nullable: false),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: true),
                    NamedFromPlaceholder = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodeFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovieFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<int[]>(type: "integer[]", nullable: false),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: true),
                    Edition = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieFiles", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_EpisodeFiles_EpisodeFileId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_MovieFiles_MovieFileId",
                table: "Movies");

            migrationBuilder.DropTable(
                name: "EpisodeFiles");

            migrationBuilder.DropTable(
                name: "MovieFiles");

            migrationBuilder.DropIndex(
                name: "IX_Movies_MovieFileId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_EpisodeFileId",
                table: "Episodes");

            migrationBuilder.DropColumn(
                name: "MovieFileId",
                table: "Movies");
        }
    }
}
