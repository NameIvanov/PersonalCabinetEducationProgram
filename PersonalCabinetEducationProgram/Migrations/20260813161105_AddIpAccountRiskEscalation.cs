using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAccountRiskEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "account_risk_escalation_level",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "account_risk_last_blocked_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "account_risk_marked_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "account_risk_score",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "account_risk_window_reset_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                type: "datetime(6)",
                precision: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account_risk_escalation_level",
                schema: "personal_cabinet",
                table: "ip_address_security_states");

            migrationBuilder.DropColumn(
                name: "account_risk_last_blocked_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states");

            migrationBuilder.DropColumn(
                name: "account_risk_marked_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states");

            migrationBuilder.DropColumn(
                name: "account_risk_score",
                schema: "personal_cabinet",
                table: "ip_address_security_states");

            migrationBuilder.DropColumn(
                name: "account_risk_window_reset_at_utc",
                schema: "personal_cabinet",
                table: "ip_address_security_states");
        }
    }
}
