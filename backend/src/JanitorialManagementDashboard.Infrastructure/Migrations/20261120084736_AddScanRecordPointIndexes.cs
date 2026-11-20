using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScanRecordPointIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_scan_records_service_point_id_id",
                table: "scan_records",
                columns: new[] { "service_point_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_service_point_id_submitted_at",
                table: "scan_records",
                columns: new[] { "service_point_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_shift_date_shift_submitted_at",
                table: "scan_records",
                columns: new[] { "shift_date", "shift", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scan_records_service_point_id_id",
                table: "scan_records");

            migrationBuilder.DropIndex(
                name: "IX_scan_records_service_point_id_submitted_at",
                table: "scan_records");

            migrationBuilder.DropIndex(
                name: "IX_scan_records_shift_date_shift_submitted_at",
                table: "scan_records");
        }
    }
}
