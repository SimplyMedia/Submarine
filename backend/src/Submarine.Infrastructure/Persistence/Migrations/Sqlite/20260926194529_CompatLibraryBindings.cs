using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CompatLibraryBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompatLibraryBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Facade = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Excluded = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompatLibraryBindings", x => x.Id);
                    table.CheckConstraint("CK_CompatLibraryBindings_FacadeTitle", "(\"Facade\" = 'sonarr' AND \"SeriesId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"Facade\" = 'radarr' AND \"SeriesId\" IS NULL AND \"MovieId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CompatLibraryBindings_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompatLibraryBindings_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompatLibraryBindings_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_Facade_MediaVersionId",
                table: "CompatLibraryBindings",
                columns: new[] { "Facade", "MediaVersionId" },
                unique: true,
                filter: "\"MediaVersionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_Facade_MovieId",
                table: "CompatLibraryBindings",
                columns: new[] { "Facade", "MovieId" },
                unique: true,
                filter: "\"MovieId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_Facade_SeriesId",
                table: "CompatLibraryBindings",
                columns: new[] { "Facade", "SeriesId" },
                unique: true,
                filter: "\"SeriesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_MediaVersionId",
                table: "CompatLibraryBindings",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_MovieId",
                table: "CompatLibraryBindings",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_CompatLibraryBindings_SeriesId",
                table: "CompatLibraryBindings",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompatLibraryBindings");
        }
    }
}
