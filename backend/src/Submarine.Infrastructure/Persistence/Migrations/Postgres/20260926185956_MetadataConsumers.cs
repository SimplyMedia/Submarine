using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MetadataConsumers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "Series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "Movies",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileDate",
                table: "MediaManagementConfig",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AutoTaggingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Enable = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveTagsAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    Specifications = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoTaggingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataConsumers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Enable = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataConsumers", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "MetadataConsumers" ("Name", "Type", "Enable", "SettingsJson", "CreatedAt", "UpdatedAt")
                SELECT 'Kodi (XBMC) / Emby', 0, true, '{}', now(), now()
                FROM "MediaManagementConfig"
                WHERE "Id" = 1 AND "WriteNfo" = true;
                """);

            migrationBuilder.DropColumn(
                name: "WriteNfo",
                table: "MediaManagementConfig");

            migrationBuilder.CreateTable(
                name: "AutoTaggingRuleTag",
                columns: table => new
                {
                    AutoTaggingRuleId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "FileDate",
                table: "MediaManagementConfig");

            migrationBuilder.AddColumn<bool>(
                name: "WriteNfo",
                table: "MediaManagementConfig",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
