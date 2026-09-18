using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomOS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_tokens",
                columns: table => new
                {
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    access_token = table.Column<string>(type: "TEXT", nullable: false),
                    refresh_token = table.Column<string>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_tokens", x => x.provider);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_tokens");
        }
    }
}
