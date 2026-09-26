using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CompatBindingSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompatLibraryBindings_MediaVersions_MediaVersionId",
                table: "CompatLibraryBindings");

            migrationBuilder.AddForeignKey(
                name: "FK_CompatLibraryBindings_MediaVersions_MediaVersionId",
                table: "CompatLibraryBindings",
                column: "MediaVersionId",
                principalTable: "MediaVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompatLibraryBindings_MediaVersions_MediaVersionId",
                table: "CompatLibraryBindings");

            migrationBuilder.AddForeignKey(
                name: "FK_CompatLibraryBindings_MediaVersions_MediaVersionId",
                table: "CompatLibraryBindings",
                column: "MediaVersionId",
                principalTable: "MediaVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
