using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PointMonthlySummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "point_monthly_summaries",
                columns: table => new
                {
                    service_point_id = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<DateTime>(type: "date", nullable: false),
                    rounds_total = table.Column<int>(type: "int", nullable: false),
                    submitted_on_time = table.Column<int>(type: "int", nullable: false),
                    submitted_late = table.Column<int>(type: "int", nullable: false),
                    not_done = table.Column<int>(type: "int", nullable: false),
                    inspected_pass = table.Column<int>(type: "int", nullable: false),
                    inspected_fail = table.Column<int>(type: "int", nullable: false),
                    not_inspected = table.Column<int>(type: "int", nullable: false),
                    issues_reported = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_monthly_summaries", x => new { x.service_point_id, x.month });
                    table.ForeignKey(
                        name: "FK_point_monthly_summaries_service_points_service_point_id",
                        column: x => x.service_point_id,
                        principalTable: "service_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "point_monthly_summaries");
        }
    }
}
