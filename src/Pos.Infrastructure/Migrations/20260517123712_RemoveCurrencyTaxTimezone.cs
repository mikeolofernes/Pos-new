using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCurrencyTaxTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "taxes");

            migrationBuilder.DropColumn(
                name: "default_currency",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "tax_total",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "sale_payments");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "sale_payments");

            migrationBuilder.DropColumn(
                name: "line_tax",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "tax_inclusive",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "tax_rate_snapshot",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "default_price_currency",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tax_code",
                table: "products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_currency",
                table: "tenants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "shops",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "shops",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "tax_total",
                table: "sales",
                type: "numeric(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "sale_payments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "sale_payments",
                type: "numeric(19,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "line_tax",
                table: "sale_items",
                type: "numeric(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "tax_inclusive",
                table: "sale_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate_snapshot",
                table: "sale_items",
                type: "numeric(9,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "default_price_currency",
                table: "products",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "products",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_inclusive = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_taxes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_taxes_tenant_id_code",
                table: "taxes",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }
    }
}
