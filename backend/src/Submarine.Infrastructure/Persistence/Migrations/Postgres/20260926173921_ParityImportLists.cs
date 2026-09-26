using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CleanLibraryLevel = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportListStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportListId = table.Column<int>(type: "integer", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisabledUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitialFailure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MostRecentFailure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
