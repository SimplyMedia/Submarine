using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParityIndexers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrabLimit",
                table: "Indexers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitsUnit",
                table: "Indexers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QueryLimit",
                table: "Indexers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Redirect",
                table: "Indexers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequiredFlags",
                table: "Indexers",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SeasonSearchMaximumSingleEpisodeAge",
                table: "Indexers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VipExpiration",
                table: "Indexers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrabLimit",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "LimitsUnit",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "QueryLimit",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "Redirect",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "RequiredFlags",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "SeasonSearchMaximumSingleEpisodeAge",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "VipExpiration",
                table: "Indexers");
        }
    }
}
