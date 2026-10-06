using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCorrectiveActionAssignedToOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CorrectiveActions_AspNetUsers_AssignedToId",
                table: "CorrectiveActions");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToId",
                table: "CorrectiveActions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "FK_CorrectiveActions_AspNetUsers_AssignedToId",
                table: "CorrectiveActions",
                column: "AssignedToId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CorrectiveActions_AspNetUsers_AssignedToId",
                table: "CorrectiveActions");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToId",
                table: "CorrectiveActions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CorrectiveActions_AspNetUsers_AssignedToId",
                table: "CorrectiveActions",
                column: "AssignedToId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
