using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacilityRealtime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupervisorInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "accuracy_m",
                table: "inspections",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cover_assignment_id",
                table: "inspections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "distance_m",
                table: "inspections",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "inspections",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "inspections",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "within_radius",
                table: "inspections",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "area_id",
                table: "cover_assignments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "building_id",
                table: "cover_assignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inspections_cover_assignment_id",
                table: "inspections",
                column: "cover_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspections_inspected_at",
                table: "inspections",
                column: "inspected_at");

            migrationBuilder.CreateIndex(
                name: "IX_cover_assignments_building_id_shift_date_shift",
                table: "cover_assignments",
                columns: new[] { "building_id", "shift_date", "shift" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cover_assignments_one_place",
                table: "cover_assignments",
                sql: "(area_id IS NOT NULL AND building_id IS NULL) OR (area_id IS NULL AND building_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_cover_assignments_buildings_building_id",
                table: "cover_assignments",
                column: "building_id",
                principalTable: "buildings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inspections_cover_assignments_cover_assignment_id",
                table: "inspections",
                column: "cover_assignment_id",
                principalTable: "cover_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cover_assignments_buildings_building_id",
                table: "cover_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_inspections_cover_assignments_cover_assignment_id",
                table: "inspections");

            migrationBuilder.DropIndex(
                name: "IX_inspections_cover_assignment_id",
                table: "inspections");

            migrationBuilder.DropIndex(
                name: "IX_inspections_inspected_at",
                table: "inspections");

            migrationBuilder.DropIndex(
                name: "IX_cover_assignments_building_id_shift_date_shift",
                table: "cover_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cover_assignments_one_place",
                table: "cover_assignments");

            migrationBuilder.DropColumn(
                name: "accuracy_m",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "cover_assignment_id",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "distance_m",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "within_radius",
                table: "inspections");

            migrationBuilder.DropColumn(
                name: "building_id",
                table: "cover_assignments");

            migrationBuilder.AlterColumn<int>(
                name: "area_id",
                table: "cover_assignments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
