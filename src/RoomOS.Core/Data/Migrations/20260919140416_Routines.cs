using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomOS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Routines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "routines",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    scene_id = table.Column<string>(type: "TEXT", nullable: false),
                    minute_of_day = table.Column<int>(type: "INTEGER", nullable: false),
                    days = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routines", x => x.id);
                    table.ForeignKey(
                        name: "FK_routines_scenes_scene_id",
                        column: x => x.scene_id,
                        principalTable: "scenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_routines_scene_id",
                table: "routines",
                column: "scene_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "routines");
        }
    }
}
