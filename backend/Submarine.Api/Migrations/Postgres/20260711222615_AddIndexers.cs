using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddIndexers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<int>>(
                name: "AnimeCategories",
                table: "Providers",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "Categories",
                table: "Providers",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "TorznabIndexer_AnimeCategories",
                table: "Providers",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "TorznabIndexer_Categories",
                table: "Providers",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TorznabIndexer_MinimumSeeders",
                table: "Providers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TorznabIndexer_SeasonPackSeedTime",
                table: "Providers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "TorznabIndexer_SeedRatio",
                table: "Providers",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TorznabIndexer_SeedTime",
                table: "Providers",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnimeCategories",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "Categories",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_AnimeCategories",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_Categories",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_MinimumSeeders",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_SeasonPackSeedTime",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_SeedRatio",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "TorznabIndexer_SeedTime",
                table: "Providers");
        }
    }
}
