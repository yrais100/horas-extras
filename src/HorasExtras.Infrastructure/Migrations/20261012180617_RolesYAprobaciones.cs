using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorasExtras.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolesYAprobaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lo que ya existía antes de las aprobaciones queda aprobado y las cuentas, activas.
            migrationBuilder.AddColumn<string>(
                name: "ComentarioRevision",
                schema: "horasextras",
                table: "Sesiones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisadaEnUtc",
                schema: "horasextras",
                table: "Sesiones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisadaPor",
                schema: "horasextras",
                table: "Sesiones",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
                schema: "horasextras",
                table: "Sesiones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Aprobada");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                schema: "horasextras",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_Revision",
                schema: "horasextras",
                table: "Sesiones",
                column: "Revision");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sesiones_Revision",
                schema: "horasextras",
                table: "Sesiones");

            migrationBuilder.DropColumn(
                name: "ComentarioRevision",
                schema: "horasextras",
                table: "Sesiones");

            migrationBuilder.DropColumn(
                name: "RevisadaEnUtc",
                schema: "horasextras",
                table: "Sesiones");

            migrationBuilder.DropColumn(
                name: "RevisadaPor",
                schema: "horasextras",
                table: "Sesiones");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "horasextras",
                table: "Sesiones");

            migrationBuilder.DropColumn(
                name: "Estado",
                schema: "horasextras",
                table: "AspNetUsers");
        }
    }
}
