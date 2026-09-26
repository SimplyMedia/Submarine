using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParityMetadataConsumers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WriteNfo",
                table: "MediaManagementConfig",
                newName: "FileDate");

            migrationBuilder.CreateTable(
                name: "AutoTaggingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemoveTagsAutomatically = table.Column<bool>(type: "INTEGER", nullable: false),
                    Specifications = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoTaggingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataConsumers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataConsumers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoTaggingRuleTag",
                columns: table => new
                {
                    AutoTaggingRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoTaggingRuleTag", x => new { x.AutoTaggingRuleId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_AutoTaggingRuleTag_AutoTaggingRules_AutoTaggingRuleId",
                        column: x => x.AutoTaggingRuleId,
                        principalTable: "AutoTaggingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutoTaggingRuleTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoTaggingRuleTag_TagsId",
                table: "AutoTaggingRuleTag",
                column: "TagsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoTaggingRuleTag");

            migrationBuilder.DropTable(
                name: "MetadataConsumers");

            migrationBuilder.DropTable(
                name: "AutoTaggingRules");

            migrationBuilder.RenameColumn(
                name: "FileDate",
                table: "MediaManagementConfig",
                newName: "WriteNfo");
        }
    }
}
