using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomOS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Scenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scenes",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    room_id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    icon = table.Column<string>(type: "TEXT", nullable: false),
                    steps_json = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenes", x => x.id);
                    table.ForeignKey(
                        name: "FK_scenes_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scenes_room_id",
                table: "scenes",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenes");
        }
    }
}
