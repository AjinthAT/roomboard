using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomOS.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AudioOutputForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audio_outputs_pc_device_id",
                table: "audio_outputs",
                column: "pc_device_id");

            migrationBuilder.AddForeignKey(
                name: "FK_audio_outputs_devices_pc_device_id",
                table: "audio_outputs",
                column: "pc_device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audio_outputs_devices_pc_device_id",
                table: "audio_outputs");

            migrationBuilder.DropIndex(
                name: "IX_audio_outputs_pc_device_id",
                table: "audio_outputs");
        }
    }
}
