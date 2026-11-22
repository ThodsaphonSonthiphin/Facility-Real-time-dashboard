using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCleanerProfileShiftNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "shift",
                table: "cleaner_profile_periods",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE cleaner_profile_periods SET shift = 'DAY' WHERE shift IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "shift",
                table: "cleaner_profile_periods",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "DAY",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }
    }
}
