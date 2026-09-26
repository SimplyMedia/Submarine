using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ParityDecisionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "RequiredFlags",
                table: "Indexers",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<bool>(
                name: "AllowHardcodedSubs",
                table: "IndexerConfig",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhitelistedHardcodedSubs",
                table: "IndexerConfig",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnableTorrent",
                table: "DelayProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableUsenet",
                table: "DelayProfiles",
                type: "boolean",
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
