using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class FeatureParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdateAutomatically",
                table: "GeneralConfig");

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
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SeasonSearchMaximumSingleEpisodeAge",
                table: "Indexers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VipExpiration",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUrl",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AuthenticationRequired",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BackupFolder",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BackupIntervalDays",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BackupRetention",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CertificateValidation",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProxyBypassFilter",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ProxyBypassLocalAddresses",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProxyEnabled",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProxyHost",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProxyPassword",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProxyPort",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProxyType",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProxyUsername",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustedProxies",
                table: "GeneralConfig",
                type: "TEXT",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.CreateTable(
                name: "NotificationStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisabledUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InitialFailure = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MostRecentFailure = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EscalationLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationStatuses_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportListStatuses_ImportListId",
                table: "ImportListStatuses",
                column: "ImportListId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationStatuses_NotificationId",
                table: "NotificationStatuses",
                column: "NotificationId",
                unique: true);

            // Per-client removal toggles were never read before, so existing clients keep removing like they did.
            migrationBuilder.Sql("UPDATE \"DownloadClients\" SET \"RemoveCompleted\" = 1, \"RemoveFailed\" = 1;");

            // Usenet indexers now require redirect downloads; Protocol 1 is USENET.
            migrationBuilder.Sql("UPDATE \"Indexers\" SET \"Redirect\" = 1 WHERE \"Protocol\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportListConfig");

            migrationBuilder.DropTable(
                name: "ImportListStatuses");

            migrationBuilder.DropTable(
                name: "NotificationStatuses");

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

            migrationBuilder.DropColumn(
                name: "AllowHardcodedSubs",
                table: "IndexerConfig");

            migrationBuilder.DropColumn(
                name: "WhitelistedHardcodedSubs",
                table: "IndexerConfig");

            migrationBuilder.DropColumn(
                name: "ApplicationUrl",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "AuthenticationRequired",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "BackupFolder",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "BackupIntervalDays",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "BackupRetention",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "CertificateValidation",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyBypassFilter",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyBypassLocalAddresses",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyEnabled",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyHost",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyPassword",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyPort",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyType",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "ProxyUsername",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "TrustedProxies",
                table: "GeneralConfig");

            migrationBuilder.DropColumn(
                name: "EnableTorrent",
                table: "DelayProfiles");

            migrationBuilder.DropColumn(
                name: "EnableUsenet",
                table: "DelayProfiles");

            migrationBuilder.AddColumn<bool>(
                name: "UpdateAutomatically",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: true);

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
