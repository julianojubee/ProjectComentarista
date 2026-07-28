using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddBlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ehautorblog",
                table: "aspnetusers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "blogcategorias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogcategorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blogtags",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogtags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blogposts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resumo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    conteudomarkdown = table.Column<string>(type: "text", nullable: false),
                    conteudohtml = table.Column<string>(type: "text", nullable: false),
                    imagemcapaurl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    imagemcapaalt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publicadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorid = table.Column<string>(type: "text", nullable: false),
                    categoriaid = table.Column<int>(type: "integer", nullable: true),
                    tempoleituramin = table.Column<int>(type: "integer", nullable: false),
                    visualizacoes = table.Column<int>(type: "integer", nullable: false),
                    metatitulo = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    metadescricao = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    excluidoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    jogoid = table.Column<int>(type: "integer", nullable: true),
                    timeid = table.Column<int>(type: "integer", nullable: true),
                    jogadorid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogposts", x => x.id);
                    table.ForeignKey(
                        name: "FK_blogposts_aspnetusers_autorid",
                        column: x => x.autorid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_blogposts_blogcategorias_categoriaid",
                        column: x => x.categoriaid,
                        principalTable: "blogcategorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_blogposts_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_blogposts_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_blogposts_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "blogposttags",
                columns: table => new
                {
                    postid = table.Column<int>(type: "integer", nullable: false),
                    tagid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogposttags", x => new { x.postid, x.tagid });
                    table.ForeignKey(
                        name: "FK_blogposttags_blogposts_postid",
                        column: x => x.postid,
                        principalTable: "blogposts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_blogposttags_blogtags_tagid",
                        column: x => x.tagid,
                        principalTable: "blogtags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blogcategorias_slug",
                table: "blogcategorias",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_autorid",
                table: "blogposts",
                column: "autorid");

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_categoriaid",
                table: "blogposts",
                column: "categoriaid");

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_jogadorid",
                table: "blogposts",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_jogoid",
                table: "blogposts",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_slug",
                table: "blogposts",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_status_publicadoem",
                table: "blogposts",
                columns: new[] { "status", "publicadoem" });

            migrationBuilder.CreateIndex(
                name: "IX_blogposts_timeid",
                table: "blogposts",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_blogposttags_tagid",
                table: "blogposttags",
                column: "tagid");

            migrationBuilder.CreateIndex(
                name: "IX_blogtags_slug",
                table: "blogtags",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blogposttags");

            migrationBuilder.DropTable(
                name: "blogposts");

            migrationBuilder.DropTable(
                name: "blogtags");

            migrationBuilder.DropTable(
                name: "blogcategorias");

            migrationBuilder.DropColumn(
                name: "ehautorblog",
                table: "aspnetusers");
        }
    }
}
