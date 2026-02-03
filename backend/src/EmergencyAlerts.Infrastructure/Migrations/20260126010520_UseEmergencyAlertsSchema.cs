using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmergencyAlerts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseEmergencyAlertsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "emergency_alerts");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "admin_boundaries",
                schema: "emergency_alerts",
                columns: table => new
                {
                    boundary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    boundary_polygon_wkt = table.Column<string>(type: "text", nullable: false),
                    admin_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_boundaries", x => x.boundary_id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "emergency_alerts",
                columns: table => new
                {
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    channel_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "PendingApproval"),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "en-GB"),
                    delivery_status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.alert_id);
                });

            migrationBuilder.CreateTable(
                name: "correlation_events",
                schema: "emergency_alerts",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_type = table.Column<string>(type: "text", nullable: false),
                    alert_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    detection_timestamp = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    cluster_severity = table.Column<string>(type: "text", nullable: true),
                    region_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correlation_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "recipients",
                schema: "emergency_alerts",
                columns: table => new
                {
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipients", x => x.recipient_id);
                });

            migrationBuilder.CreateTable(
                name: "approval_records",
                schema: "emergency_alerts",
                columns: table => new
                {
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approver_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_records", x => x.approval_id);
                    table.ForeignKey(
                        name: "FK_approval_records_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "emergency_alerts",
                        principalTable: "alerts",
                        principalColumn: "alert_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                schema: "emergency_alerts",
                columns: table => new
                {
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    region_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    area_polygon_wkt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.area_id);
                    table.ForeignKey(
                        name: "FK_areas_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "emergency_alerts",
                        principalTable: "alerts",
                        principalColumn: "alert_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_attempts",
                schema: "emergency_alerts",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    acs_operation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attempted_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_attempts", x => x.attempt_id);
                    table.ForeignKey(
                        name: "FK_delivery_attempts_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "emergency_alerts",
                        principalTable: "alerts",
                        principalColumn: "alert_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_attempts_recipients_recipient_id",
                        column: x => x.recipient_id,
                        principalSchema: "emergency_alerts",
                        principalTable: "recipients",
                        principalColumn: "recipient_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_boundaries_region_code",
                schema: "emergency_alerts",
                table: "admin_boundaries",
                column: "region_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alerts_delivery_status",
                schema: "emergency_alerts",
                table: "alerts",
                column: "delivery_status",
                filter: "delivery_status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_expires_at",
                schema: "emergency_alerts",
                table: "alerts",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_status",
                schema: "emergency_alerts",
                table: "alerts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_approval_records_alert_id",
                schema: "emergency_alerts",
                table: "approval_records",
                column: "alert_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_areas_alert_id",
                schema: "emergency_alerts",
                table: "areas",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_correlation_events_detection_timestamp",
                schema: "emergency_alerts",
                table: "correlation_events",
                column: "detection_timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_alert_id",
                schema: "emergency_alerts",
                table: "delivery_attempts",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_recipient_id",
                schema: "emergency_alerts",
                table: "delivery_attempts",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_status",
                schema: "emergency_alerts",
                table: "delivery_attempts",
                column: "status",
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_recipients_email_address",
                schema: "emergency_alerts",
                table: "recipients",
                column: "email_address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_boundaries",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "approval_records",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "areas",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "correlation_events",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "alerts",
                schema: "emergency_alerts");

            migrationBuilder.DropTable(
                name: "recipients",
                schema: "emergency_alerts");
        }
    }
}
