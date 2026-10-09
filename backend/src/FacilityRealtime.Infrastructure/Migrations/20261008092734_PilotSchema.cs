using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace FacilityRealtime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PilotSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "buildings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    building_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    shift_pattern = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.id);
                    table.ForeignKey(
                        name: "FK_areas_buildings_building_id",
                        column: x => x.building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "service_points",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_points", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_points_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    employee_id = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    username = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    display_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    secret_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    building_id = table.Column<int>(type: "int", nullable: true),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    cleaner_slot = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN role = 'CLEANER' AND is_active = 1 THEN area_id * 2 + (CASE WHEN shift = 'NIGHT' THEN 1 ELSE 0 END) END", stored: true),
                    supervisor_slot = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN role = 'SUPERVISOR' AND is_active = 1 THEN building_id * 2 + (CASE WHEN shift = 'NIGHT' THEN 1 ELSE 0 END) END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_login_name", "(role = 'ADMIN' AND username IS NOT NULL) OR (role <> 'ADMIN' AND employee_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_users_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_buildings_building_id",
                        column: x => x.building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "point_round_windows",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    service_point_id = table.Column<int>(type: "int", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_round_windows", x => x.id);
                    table.ForeignKey(
                        name: "FK_point_round_windows_service_points_service_point_id",
                        column: x => x.service_point_id,
                        principalTable: "service_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "signs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    service_point_id = table.Column<int>(type: "int", nullable: true),
                    code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    qr_token = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    qr_issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    checkin_area_id = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN service_point_id IS NULL THEN area_id END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signs", x => x.id);
                    table.ForeignKey(
                        name: "FK_signs_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_signs_service_points_service_point_id",
                        column: x => x.service_point_id,
                        principalTable: "service_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    session_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    rotated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "scan_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    service_point_id = table.Column<int>(type: "int", nullable: false),
                    sign_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    round_window_id = table.Column<int>(type: "int", nullable: true),
                    round_start = table.Column<TimeOnly>(type: "time", nullable: true),
                    round_end = table.Column<TimeOnly>(type: "time", nullable: true),
                    placement = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    late_minutes = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    issue_tags = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_scan_records_point_round_windows_round_window_id",
                        column: x => x.round_window_id,
                        principalTable: "point_round_windows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scan_records_service_points_service_point_id",
                        column: x => x.service_point_id,
                        principalTable: "service_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scan_records_signs_sign_id",
                        column: x => x.sign_id,
                        principalTable: "signs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scan_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inspections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    scan_record_id = table.Column<long>(type: "bigint", nullable: false),
                    service_point_id = table.Column<int>(type: "int", nullable: false),
                    supervisor_id = table.Column<int>(type: "int", nullable: false),
                    result = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    defect = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    inspected_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspections", x => x.id);
                    table.CheckConstraint("ck_inspections_rework_has_defect", "result <> 'REWORK' OR defect IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_inspections_scan_records_scan_record_id",
                        column: x => x.scan_record_id,
                        principalTable: "scan_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inspections_service_points_service_point_id",
                        column: x => x.service_point_id,
                        principalTable: "service_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inspections_users_supervisor_id",
                        column: x => x.supervisor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_areas_building_id",
                table: "areas",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "IX_areas_code",
                table: "areas",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_buildings_code",
                table: "buildings",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inspections_scan_record_id",
                table: "inspections",
                column: "scan_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspections_service_point_id_inspected_at",
                table: "inspections",
                columns: new[] { "service_point_id", "inspected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_inspections_supervisor_id_inspected_at",
                table: "inspections",
                columns: new[] { "supervisor_id", "inspected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_point_round_windows_service_point_id_shift_start_time",
                table: "point_round_windows",
                columns: new[] { "service_point_id", "shift", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_session_id",
                table: "refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_round_window_id",
                table: "scan_records",
                column: "round_window_id");

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_service_point_id_shift_date_shift",
                table: "scan_records",
                columns: new[] { "service_point_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_sign_id",
                table: "scan_records",
                column: "sign_id");

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_submitted_at",
                table: "scan_records",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_user_id_submitted_at",
                table: "scan_records",
                columns: new[] { "user_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_service_points_area_id_sort_order",
                table: "service_points",
                columns: new[] { "area_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_signs_area_id",
                table: "signs",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_signs_checkin_area_id",
                table: "signs",
                column: "checkin_area_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signs_code",
                table: "signs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signs_qr_token",
                table: "signs",
                column: "qr_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signs_service_point_id",
                table: "signs",
                column: "service_point_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_area_id",
                table: "users",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_building_id",
                table: "users",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_cleaner_slot",
                table: "users",
                column: "cleaner_slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_employee_id",
                table: "users",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_supervisor_slot",
                table: "users",
                column: "supervisor_slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inspections");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "scan_records");

            migrationBuilder.DropTable(
                name: "point_round_windows");

            migrationBuilder.DropTable(
                name: "signs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "service_points");

            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "buildings");
        }
    }
}
