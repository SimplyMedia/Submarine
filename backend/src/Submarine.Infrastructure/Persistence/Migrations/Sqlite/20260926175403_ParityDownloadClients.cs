using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParityDownloadClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RemoveCompleted/RemoveFailed were exposed in the UI but never consulted by the download monitor,
            // so every existing row is effectively "off" by accident; backfill to the intended default (on),
            // matching upstream's DownloadClientDefinition.RemoveCompletedDownloads/RemoveFailedDownloads = true.
            migrationBuilder.Sql("UPDATE \"DownloadClients\" SET \"RemoveCompleted\" = 1, \"RemoveFailed\" = 1;");

            migrationBuilder.AlterColumn<bool>(
                name: "RemoveFailed",
                table: "DownloadClients",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "RemoveCompleted",
                table: "DownloadClients",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "RemoveFailed",
                table: "DownloadClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "RemoveCompleted",
                table: "DownloadClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);
        }
    }
}
