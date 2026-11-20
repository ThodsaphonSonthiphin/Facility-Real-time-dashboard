using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseSixAttendanceActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            NormalizeBreakEvents(migrationBuilder, "shift_attendances");
            NormalizeBreakEvents(migrationBuilder, "attendance_gaps");

            migrationBuilder.DropTable(
                name: "attendance_slot_lifecycles");
        }

        private static void NormalizeBreakEvents(MigrationBuilder migrationBuilder, string table)
        {
            // Keep the numbered record when both names exist for the same person and shift.
            migrationBuilder.Sql($"""
                DELETE old_record FROM `{table}` AS old_record
                JOIN `{table}` AS numbered
                    ON numbered.user_id = old_record.user_id
                    AND numbered.shift_date = old_record.shift_date
                    AND numbered.shift = old_record.shift
                    AND numbered.event_type = CASE old_record.event_type
                        WHEN 'BREAK_OUT' THEN 'BREAK1_OUT'
                        WHEN 'BREAK_IN' THEN 'BREAK1_IN'
                    END
                WHERE old_record.event_type IN ('BREAK_OUT', 'BREAK_IN')
                """);
            migrationBuilder.Sql($"""
                UPDATE `{table}`
                SET event_type = CASE event_type
                    WHEN 'BREAK_OUT' THEN 'BREAK1_OUT'
                    WHEN 'BREAK_IN' THEN 'BREAK1_IN'
                END
                WHERE event_type IN ('BREAK_OUT', 'BREAK_IN')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_slot_lifecycles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    activated_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    version = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_slot_lifecycles", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_slot_lifecycles_users_activated_by_id",
                        column: x => x.activated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_slot_lifecycles_activated_by_id",
                table: "attendance_slot_lifecycles",
                column: "activated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_slot_lifecycles_shift_date_shift",
                table: "attendance_slot_lifecycles",
                columns: new[] { "shift_date", "shift" },
                unique: true);
        }
    }
}
