using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ParitySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdateAutomatically",
                table: "GeneralConfig");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "UpdateAutomatically",
                table: "GeneralConfig",
                type: "INTEGER",
                nullable: true);
        }
    }
}
