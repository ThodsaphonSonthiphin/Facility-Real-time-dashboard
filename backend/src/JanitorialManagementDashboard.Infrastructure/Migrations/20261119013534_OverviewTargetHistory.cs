using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace JanitorialManagementDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OverviewTargetHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "round_origin_id",
                table: "scan_records",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE scan_records SET round_origin_id = round_window_id WHERE round_window_id IS NOT NULL AND placement <> 'OFF_ROUND';");

            migrationBuilder.CreateTable(
                name: "overview_target_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    effective_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    schema_version = table.Column<int>(type: "int", nullable: false),
                    payload_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "json", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overview_target_revisions", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "overview_target_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    latest_revision_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overview_target_state", x => x.id);
                    table.ForeignKey(
                        name: "FK_overview_target_state_overview_target_revisions_latest_revis~",
                        column: x => x.latest_revision_id,
                        principalTable: "overview_target_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "overview_target_state",
                columns: new[] { "id", "latest_revision_id" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_overview_target_revisions_effective_at_id",
                table: "overview_target_revisions",
                columns: new[] { "effective_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_overview_target_state_latest_revision_id",
                table: "overview_target_state",
                column: "latest_revision_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "overview_target_state");

            migrationBuilder.DropTable(
                name: "overview_target_revisions");

            migrationBuilder.DropColumn(
                name: "round_origin_id",
                table: "scan_records");
        }
    }
}
