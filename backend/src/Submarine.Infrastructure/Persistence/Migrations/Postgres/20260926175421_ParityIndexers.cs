using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
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
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitsUnit",
                table: "Indexers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QueryLimit",
                table: "Indexers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Redirect",
                table: "Indexers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequiredFlags",
                table: "Indexers",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SeasonSearchMaximumSingleEpisodeAge",
                table: "Indexers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VipExpiration",
                table: "Indexers",
                type: "text",
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
