using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace FacilityRealtime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScanRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "signs",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "located_at",
                table: "signs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "location_accuracy_m",
                table: "signs",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location_source",
                table: "signs",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "signs",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "radius_m",
                table: "signs",
                type: "smallint",
                nullable: false,
                defaultValue: (short)50);

            migrationBuilder.AddColumn<short>(
                name: "accuracy_m",
                table: "scan_records",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cover_assignment_id",
                table: "scan_records",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "distance_m",
                table: "scan_records",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "scan_records",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "scan_records",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "within_radius",
                table: "scan_records",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    actor_id = table.Column<int>(type: "int", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    summary = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    before_json = table.Column<string>(type: "json", nullable: true),
                    after_json = table.Column<string>(type: "json", nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_log_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "blocked_scans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    sign_id = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    scanned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    accuracy_m = table.Column<short>(type: "smallint", nullable: true),
                    distance_m = table.Column<short>(type: "smallint", nullable: true),
                    within_radius = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_scans", x => x.id);
                    table.ForeignKey(
                        name: "FK_blocked_scans_signs_sign_id",
                        column: x => x.sign_id,
                        principalTable: "signs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_blocked_scans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cover_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    assigned_by_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    cancelled_by_id = table.Column<int>(type: "int", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cover_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_cover_assignments_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cover_assignments_users_assigned_by_id",
                        column: x => x.assigned_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cover_assignments_users_cancelled_by_id",
                        column: x => x.cancelled_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cover_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_attendances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    event_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    sign_id = table.Column<int>(type: "int", nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    accuracy_m = table.Column<short>(type: "smallint", nullable: true),
                    distance_m = table.Column<short>(type: "smallint", nullable: true),
                    within_radius = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_attendances", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_attendances_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_attendances_signs_sign_id",
                        column: x => x.sign_id,
                        principalTable: "signs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_attendances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_scan_records_cover_assignment_id",
                table: "scan_records",
                column: "cover_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_actor_id",
                table: "audit_log",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_occurred_at",
                table: "audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_scans_scanned_at",
                table: "blocked_scans",
                column: "scanned_at");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_scans_sign_id_scanned_at",
                table: "blocked_scans",
                columns: new[] { "sign_id", "scanned_at" });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_scans_user_id",
                table: "blocked_scans",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cover_assignments_area_id_shift_date_shift",
                table: "cover_assignments",
                columns: new[] { "area_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_cover_assignments_assigned_by_id",
                table: "cover_assignments",
                column: "assigned_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_cover_assignments_cancelled_by_id",
                table: "cover_assignments",
                column: "cancelled_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_cover_assignments_user_id_shift_date_shift",
                table: "cover_assignments",
                columns: new[] { "user_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_attendances_area_id",
                table: "shift_attendances",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_attendances_shift_date_shift_area_id",
                table: "shift_attendances",
                columns: new[] { "shift_date", "shift", "area_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_attendances_sign_id",
                table: "shift_attendances",
                column: "sign_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_attendances_user_id_shift_date_shift_event_type",
                table: "shift_attendances",
                columns: new[] { "user_id", "shift_date", "shift", "event_type" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_scan_records_cover_assignments_cover_assignment_id",
                table: "scan_records",
                column: "cover_assignment_id",
                principalTable: "cover_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scan_records_cover_assignments_cover_assignment_id",
                table: "scan_records");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "blocked_scans");

            migrationBuilder.DropTable(
                name: "cover_assignments");

            migrationBuilder.DropTable(
                name: "shift_attendances");

            migrationBuilder.DropIndex(
                name: "IX_scan_records_cover_assignment_id",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "located_at",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "location_accuracy_m",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "location_source",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "radius_m",
                table: "signs");

            migrationBuilder.DropColumn(
                name: "accuracy_m",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "cover_assignment_id",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "distance_m",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "scan_records");

            migrationBuilder.DropColumn(
                name: "within_radius",
                table: "scan_records");
        }
    }
}
