using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace FacilityRealtime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlertReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    alert_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    record_id = table.Column<long>(type: "bigint", nullable: false),
                    reviewed_by_id = table.Column<int>(type: "int", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_reviews_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_alert_reviews_alert_type_record_id",
                table: "alert_reviews",
                columns: new[] { "alert_type", "record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_reviews_reviewed_by_id",
                table: "alert_reviews",
                column: "reviewed_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_reviews");
        }
    }
}
