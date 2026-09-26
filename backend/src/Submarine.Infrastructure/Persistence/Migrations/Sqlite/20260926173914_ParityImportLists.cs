using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParityImportLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportListConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CleanLibraryLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportListStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportListId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisabledUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InitialFailure = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MostRecentFailure = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EscalationLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportListStatuses_ImportLists_ImportListId",
                        column: x => x.ImportListId,
                        principalTable: "ImportLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportListStatuses_ImportListId",
                table: "ImportListStatuses",
                column: "ImportListId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportListConfig");

            migrationBuilder.DropTable(
                name: "ImportListStatuses");
        }
    }
}
