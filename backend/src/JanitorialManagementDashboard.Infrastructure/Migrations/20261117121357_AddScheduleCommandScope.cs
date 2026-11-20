using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleCommandScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec §11 (Task 5 fix): the building/shift a command applied to is retained with its result, so a status
            // lookup can require that scope instead of any authority the actor holds today. Nullable: a receipt written
            // before this column existed keeps answering from the actor's current authority alone.
            migrationBuilder.AddColumn<string>(
                name: "scope_json",
                table: "schedule_command_receipts",
                type: "json",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope_json",
                table: "schedule_command_receipts");
        }
    }
}
