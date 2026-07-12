using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddNotificationAndNamingOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MultiEpisodeStyle",
                table: "NamingConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChmodFile",
                table: "MediaManagementConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChmodFolder",
                table: "MediaManagementConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChownGroup",
                table: "MediaManagementConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChownUser",
                table: "MediaManagementConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "Connections",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(21)",
                oldMaxLength: 21);

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppToken",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnDelete",
                table: "Connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnHealthIssue",
                table: "Connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnUpgrade",
                table: "Connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PushoverConnection_AppToken",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScriptPath",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerUrl",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlackConnection_WebhookUrl",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserKey",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookConnection_Password",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookConnection_Username",
                table: "Connections",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MultiEpisodeStyle",
                table: "NamingConfigs");

            migrationBuilder.DropColumn(
                name: "ChmodFile",
                table: "MediaManagementConfigs");

            migrationBuilder.DropColumn(
                name: "ChmodFolder",
                table: "MediaManagementConfigs");

            migrationBuilder.DropColumn(
                name: "ChownGroup",
                table: "MediaManagementConfigs");

            migrationBuilder.DropColumn(
                name: "ChownUser",
                table: "MediaManagementConfigs");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "AppToken",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "OnDelete",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "OnHealthIssue",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "OnUpgrade",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "PushoverConnection_AppToken",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ScriptPath",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ServerUrl",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "SlackConnection_WebhookUrl",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "UserKey",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "WebhookConnection_Password",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "WebhookConnection_Username",
                table: "Connections");

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "Connections",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(34)",
                oldMaxLength: 34);
        }
    }
}
