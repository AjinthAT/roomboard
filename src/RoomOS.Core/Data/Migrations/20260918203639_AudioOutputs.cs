using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomOS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AudioOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audio_outputs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    pc_device_id = table.Column<string>(type: "TEXT", nullable: false),
                    windows_device_id = table.Column<string>(type: "TEXT", nullable: true),
                    match_hint = table.Column<string>(type: "TEXT", nullable: false),
                    friendly_name = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_outputs", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_outputs");
        }
    }
}
