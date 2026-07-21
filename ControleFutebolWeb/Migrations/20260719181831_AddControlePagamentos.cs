using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddControlePagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "acessopagoate",
                table: "aspnetusers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pagamentosusuario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    datapagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    pagoate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valor = table.Column<decimal>(type: "numeric", nullable: false),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    dataregistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentosusuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentosusuario_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamentosusuario_usuarioid",
                table: "pagamentosusuario",
                column: "usuarioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentosusuario");

            migrationBuilder.DropColumn(
                name: "acessopagoate",
                table: "aspnetusers");
        }
    }
}
