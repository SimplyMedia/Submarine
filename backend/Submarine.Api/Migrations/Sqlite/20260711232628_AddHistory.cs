using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: true),
                    Languages = table.Column<string>(type: "TEXT", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_History_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_History_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_History_EpisodeId",
                table: "History",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_History_MovieId",
                table: "History",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_History_SeriesId",
                table: "History",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "History");
        }
    }
}
