using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParityDecisionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredFlags",
                table: "Indexers",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "AllowHardcodedSubs",
                table: "IndexerConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhitelistedHardcodedSubs",
                table: "IndexerConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnableTorrent",
                table: "DelayProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableUsenet",
                table: "DelayProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredFlags",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "AllowHardcodedSubs",
                table: "IndexerConfig");

            migrationBuilder.DropColumn(
                name: "WhitelistedHardcodedSubs",
                table: "IndexerConfig");

            migrationBuilder.DropColumn(
                name: "EnableTorrent",
                table: "DelayProfiles");

            migrationBuilder.DropColumn(
                name: "EnableUsenet",
                table: "DelayProfiles");
        }
    }
}
