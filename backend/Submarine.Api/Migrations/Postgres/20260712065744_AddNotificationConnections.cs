using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Submarine.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddNotificationConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Host",
                table: "Connections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "Connections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BotToken",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatId",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Connections",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "Connections",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotToken",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "Connections");

            migrationBuilder.AlterColumn<string>(
                name: "Host",
                table: "Connections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "Connections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
