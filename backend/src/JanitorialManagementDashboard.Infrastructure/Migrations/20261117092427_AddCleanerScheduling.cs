using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCleanerScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "area_transfers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    from_area_id = table.Column<int>(type: "int", nullable: false),
                    to_area_id = table.Column<int>(type: "int", nullable: false),
                    effective_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    is_cancelled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_by_id = table.Column<int>(type: "int", nullable: true),
                    cancellation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_transfers", x => x.id);
                    table.CheckConstraint("ck_area_transfers_other_area", "from_area_id <> to_area_id");
                    table.ForeignKey(
                        name: "FK_area_transfers_areas_from_area_id",
                        column: x => x.from_area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfers_areas_to_area_id",
                        column: x => x.to_area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfers_users_cancelled_by_id",
                        column: x => x.cancelled_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfers_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfers_users_person_id",
                        column: x => x.person_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "attendance_gaps",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    event_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    recorded_by_id = table.Column<int>(type: "int", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_gaps", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_gaps_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_gaps_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_gaps_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "attendance_slot_lifecycles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    version = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    activated_by_id = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "cleaner_profile_periods",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    home_building_id = table.Column<int>(type: "int", nullable: true),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    effective_from = table.Column<DateTime>(type: "date", nullable: false),
                    effective_until = table.Column<DateTime>(type: "date", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cleaner_profile_periods", x => x.id);
                    table.CheckConstraint("ck_cleaner_profile_periods_effective_range", "effective_until IS NULL OR effective_until > effective_from");
                    table.ForeignKey(
                        name: "FK_cleaner_profile_periods_buildings_home_building_id",
                        column: x => x.home_building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cleaner_profile_periods_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cleaner_profile_periods_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "relief_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    break_group = table.Column<int>(type: "int", nullable: true),
                    replaced_user_id = table.Column<int>(type: "int", nullable: true),
                    source_building_id = table.Column<int>(type: "int", nullable: false),
                    source_supervisor_id = table.Column<int>(type: "int", nullable: true),
                    agreement_reported = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_cancelled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_by_id = table.Column<int>(type: "int", nullable: true),
                    cancellation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relief_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_relief_assignments_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_buildings_source_building_id",
                        column: x => x.source_building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_users_cancelled_by_id",
                        column: x => x.cancelled_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_users_replaced_user_id",
                        column: x => x.replaced_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_users_source_supervisor_id",
                        column: x => x.source_supervisor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relief_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_anchors",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_anchors", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_anchors_users_person_id",
                        column: x => x.person_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_baseline_anchors",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_baseline_anchors", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_baseline_anchors_users_person_id",
                        column: x => x.person_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_baselines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    effective_from = table.Column<DateTime>(type: "date", nullable: false),
                    effective_until = table.Column<DateTime>(type: "date", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_baselines", x => x.id);
                    table.CheckConstraint("ck_schedule_baselines_effective_range", "effective_until IS NULL OR effective_until > effective_from");
                    table.ForeignKey(
                        name: "FK_schedule_baselines_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedule_baselines_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_command_receipts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    command_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    actor_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    payload_fingerprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    expected_revisions_json = table.Column<string>(type: "json", nullable: false),
                    outcome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    result_json = table.Column<string>(type: "json", nullable: false),
                    committed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_command_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_command_receipts_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_exceptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    shift_date = table.Column<DateTime>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    break_group = table.Column<int>(type: "int", nullable: true),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_cancelled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_by_id = table.Column<int>(type: "int", nullable: true),
                    active_key = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN kind IN ('AREA', 'BREAK_GROUP') AND is_cancelled = 0 THEN 1 END", stored: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_exceptions", x => x.id);
                    table.CheckConstraint("ck_schedule_exceptions_shape", "(kind = 'AREA' AND area_id IS NOT NULL AND break_group IS NULL AND starts_at IS NULL AND ends_at IS NULL) OR (kind = 'BREAK_GROUP' AND area_id IS NULL AND break_group IS NOT NULL AND starts_at IS NULL AND ends_at IS NULL) OR (kind = 'LEAVE' AND area_id IS NULL AND break_group IS NULL AND starts_at IS NOT NULL AND ends_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_schedule_exceptions_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedule_exceptions_users_cancelled_by_id",
                        column: x => x.cancelled_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedule_exceptions_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedule_exceptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_scope_anchors",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    building_id = table.Column<int>(type: "int", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_scope_anchors", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_scope_anchors_buildings_building_id",
                        column: x => x.building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supervisor_scope_periods",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    supervisor_id = table.Column<int>(type: "int", nullable: false),
                    building_id = table.Column<int>(type: "int", nullable: false),
                    shift = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    effective_from = table.Column<DateTime>(type: "date", nullable: false),
                    effective_until = table.Column<DateTime>(type: "date", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supervisor_scope_periods", x => x.id);
                    table.CheckConstraint("ck_supervisor_scope_periods_effective_range", "effective_until IS NULL OR effective_until > effective_from");
                    table.ForeignKey(
                        name: "FK_supervisor_scope_periods_buildings_building_id",
                        column: x => x.building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supervisor_scope_periods_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supervisor_scope_periods_users_supervisor_id",
                        column: x => x.supervisor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "weekly_days_off",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    week_start = table.Column<DateTime>(type: "date", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_days_off", x => x.id);
                    table.ForeignKey(
                        name: "FK_weekly_days_off_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_days_off_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "area_transfer_confirmations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    transfer_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    sign_id = table.Column<int>(type: "int", nullable: false),
                    confirmed_by_id = table.Column<int>(type: "int", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    accuracy_m = table.Column<short>(type: "smallint", nullable: true),
                    distance_m = table.Column<short>(type: "smallint", nullable: true),
                    within_radius = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_transfer_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "FK_area_transfer_confirmations_area_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "area_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfer_confirmations_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfer_confirmations_signs_sign_id",
                        column: x => x.sign_id,
                        principalTable: "signs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_transfer_confirmations_users_confirmed_by_id",
                        column: x => x.confirmed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_baseline_days",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    baseline_id = table.Column<int>(type: "int", nullable: false),
                    weekday = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    break_group = table.Column<int>(type: "int", nullable: true),
                    is_day_off = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_baseline_days", x => x.id);
                    table.CheckConstraint("ck_schedule_baseline_days_shape", "(is_day_off = 1 AND area_id IS NULL AND break_group IS NULL) OR (is_day_off = 0 AND (break_group IS NULL OR area_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_schedule_baseline_days_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schedule_baseline_days_schedule_baselines_baseline_id",
                        column: x => x.baseline_id,
                        principalTable: "schedule_baselines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedule_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    receipt_id = table.Column<int>(type: "int", nullable: false),
                    entity_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<int>(type: "int", nullable: true),
                    operation = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    before_json = table.Column<string>(type: "json", nullable: true),
                    after_json = table.Column<string>(type: "json", nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_changes_schedule_command_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "schedule_command_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "weekly_day_off_choices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    weekly_day_off_id = table.Column<int>(type: "int", nullable: false),
                    off_date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_day_off_choices", x => x.id);
                    table.ForeignKey(
                        name: "FK_weekly_day_off_choices_weekly_days_off_weekly_day_off_id",
                        column: x => x.weekly_day_off_id,
                        principalTable: "weekly_days_off",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfer_confirmations_area_id",
                table: "area_transfer_confirmations",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfer_confirmations_confirmed_by_id",
                table: "area_transfer_confirmations",
                column: "confirmed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfer_confirmations_sign_id",
                table: "area_transfer_confirmations",
                column: "sign_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfer_confirmations_transfer_id",
                table: "area_transfer_confirmations",
                column: "transfer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_area_transfers_cancelled_by_id",
                table: "area_transfers",
                column: "cancelled_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfers_created_by_id",
                table: "area_transfers",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfers_from_area_id",
                table: "area_transfers",
                column: "from_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_transfers_person_id_shift_date_shift",
                table: "area_transfers",
                columns: new[] { "person_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_area_transfers_to_area_id_shift_date_shift",
                table: "area_transfers",
                columns: new[] { "to_area_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_gaps_area_id",
                table: "attendance_gaps",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_gaps_recorded_by_id",
                table: "attendance_gaps",
                column: "recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_gaps_user_id_shift_date_shift_event_type",
                table: "attendance_gaps",
                columns: new[] { "user_id", "shift_date", "shift", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_slot_lifecycles_activated_by_id",
                table: "attendance_slot_lifecycles",
                column: "activated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_slot_lifecycles_shift_date_shift",
                table: "attendance_slot_lifecycles",
                columns: new[] { "shift_date", "shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cleaner_profile_periods_created_by_id",
                table: "cleaner_profile_periods",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_cleaner_profile_periods_home_building_id",
                table: "cleaner_profile_periods",
                column: "home_building_id");

            migrationBuilder.CreateIndex(
                name: "IX_cleaner_profile_periods_user_id_kind_effective_from",
                table: "cleaner_profile_periods",
                columns: new[] { "user_id", "kind", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_area_id_shift_date_shift",
                table: "relief_assignments",
                columns: new[] { "area_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_cancelled_by_id",
                table: "relief_assignments",
                column: "cancelled_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_created_by_id",
                table: "relief_assignments",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_replaced_user_id_shift_date_shift",
                table: "relief_assignments",
                columns: new[] { "replaced_user_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_source_building_id",
                table: "relief_assignments",
                column: "source_building_id");

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_source_supervisor_id",
                table: "relief_assignments",
                column: "source_supervisor_id");

            migrationBuilder.CreateIndex(
                name: "IX_relief_assignments_user_id_shift_date_shift",
                table: "relief_assignments",
                columns: new[] { "user_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_anchors_person_id_shift_date_shift",
                table: "schedule_anchors",
                columns: new[] { "person_id", "shift_date", "shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_anchors_shift_date_shift",
                table: "schedule_anchors",
                columns: new[] { "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_baseline_anchors_person_id",
                table: "schedule_baseline_anchors",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_baseline_days_area_id",
                table: "schedule_baseline_days",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_baseline_days_baseline_id_weekday",
                table: "schedule_baseline_days",
                columns: new[] { "baseline_id", "weekday" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_baselines_created_by_id",
                table: "schedule_baselines",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_baselines_user_id_kind_effective_from",
                table: "schedule_baselines",
                columns: new[] { "user_id", "kind", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_changes_created_at",
                table: "schedule_changes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_changes_entity_type_entity_id",
                table: "schedule_changes",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_changes_receipt_id",
                table: "schedule_changes",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_command_receipts_actor_id_committed_at",
                table: "schedule_command_receipts",
                columns: new[] { "actor_id", "committed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_command_receipts_command_id",
                table: "schedule_command_receipts",
                column: "command_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_area_id",
                table: "schedule_exceptions",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_cancelled_by_id",
                table: "schedule_exceptions",
                column: "cancelled_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_created_by_id",
                table: "schedule_exceptions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_user_id_shift_date_shift",
                table: "schedule_exceptions",
                columns: new[] { "user_id", "shift_date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_user_id_shift_date_shift_kind_active_key",
                table: "schedule_exceptions",
                columns: new[] { "user_id", "shift_date", "shift", "kind", "active_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_user_id_starts_at",
                table: "schedule_exceptions",
                columns: new[] { "user_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_scope_anchors_building_id_shift",
                table: "schedule_scope_anchors",
                columns: new[] { "building_id", "shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supervisor_scope_periods_building_id_shift_effective_from",
                table: "supervisor_scope_periods",
                columns: new[] { "building_id", "shift", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_supervisor_scope_periods_created_by_id",
                table: "supervisor_scope_periods",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_supervisor_scope_periods_supervisor_id_building_id_shift_eff~",
                table: "supervisor_scope_periods",
                columns: new[] { "supervisor_id", "building_id", "shift", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_day_off_choices_weekly_day_off_id_off_date",
                table: "weekly_day_off_choices",
                columns: new[] { "weekly_day_off_id", "off_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_days_off_created_by_id",
                table: "weekly_days_off",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_days_off_user_id_week_start",
                table: "weekly_days_off",
                columns: new[] { "user_id", "week_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_transfer_confirmations");

            migrationBuilder.DropTable(
                name: "attendance_gaps");

            migrationBuilder.DropTable(
                name: "attendance_slot_lifecycles");

            migrationBuilder.DropTable(
                name: "cleaner_profile_periods");

            migrationBuilder.DropTable(
                name: "relief_assignments");

            migrationBuilder.DropTable(
                name: "schedule_anchors");

            migrationBuilder.DropTable(
                name: "schedule_baseline_anchors");

            migrationBuilder.DropTable(
                name: "schedule_baseline_days");

            migrationBuilder.DropTable(
                name: "schedule_changes");

            migrationBuilder.DropTable(
                name: "schedule_exceptions");

            migrationBuilder.DropTable(
                name: "schedule_scope_anchors");

            migrationBuilder.DropTable(
                name: "supervisor_scope_periods");

            migrationBuilder.DropTable(
                name: "weekly_day_off_choices");

            migrationBuilder.DropTable(
                name: "area_transfers");

            migrationBuilder.DropTable(
                name: "schedule_baselines");

            migrationBuilder.DropTable(
                name: "schedule_command_receipts");

            migrationBuilder.DropTable(
                name: "weekly_days_off");
        }
    }
}
