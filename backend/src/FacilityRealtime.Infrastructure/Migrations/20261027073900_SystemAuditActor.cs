using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacilityRealtime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SystemAuditActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // facility-0092: a row the server writes by itself (the first Admin, reset-admin) has no Admin as its actor
            migrationBuilder.AlterColumn<int>(
                name: "actor_id",
                table: "audit_log",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "actor_id",
                table: "audit_log",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
