using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using HtmlAgilityPack;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Leitura da ficha de partida do ogol.com.br. É raspagem de HTML, então o que se
    /// testa aqui é exatamente o que quebra em silêncio: a coluna de cada time, quem é
    /// titular, quem nem entrou, o minuto colado no acréscimo e o par de uma
    /// substituição que chega partido em dois cartões.
    ///
    /// Os fragmentos são sintéticos: reproduzem a ESTRUTURA observada na página (as
    /// classes zz-tpl-row/zz-tpl-col, .player, .events, os títulos dos ícones) com
    /// nomes inventados, no mesmo espírito dos testes do FotMob — a página real do site
    /// não entra no repositório.
    /// </summary>
    public class OgolParsersTests
    {
        private static HtmlDocument Html(string texto)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(texto);
            return doc;
        }

        private static Jogo Jogo(string casa = "Cruzeiro W", string visitante = "Corinthians W") =>
            new()
            {
                Id = 1,
                TimeCasa = new Time { Id = 10, Nome = casa },
                TimeVisitante = new Time { Id = 20, Nome = visitante },
            };

        private static string Jogadora(
            string id, string nome, string camisa, string eventos = "", bool inativa = false) =>
            $"""
             <div class="player {(inativa ? "inactive " : "")}fl-r-cen">
               <div class="number fls-0">{camisa}</div>
               <div class="name"><div class="micrologo_and_text"><div class="text">
                 <a href="/jogador/{id}/{id}">{nome}</a>
               </div></div></div>
               <div class="events">{eventos}</div>
             </div>
             """;

        private const string Saiu = """<span class="icn_zerozero grey">8</span><div>80'</div>""";
        private const string Entrou = """<span title="Entrou" class="icn_zerozero grey">7</span><div>80'</div>""";
        private const string Amarelo = """<span title="Amarelos" class="icn_zerozero yellow">R</span><div>51'</div>""";
        private const string GolNoAcrescimo =
            """<span title="Gols" class="zz-icn zz-icn-fut-11"></span><div>90+5' </div>""";
        private const string GolContra =
            """<span title="Gols" class="zz-icn zz-icn-fut-11"></span><div>68' (g.c.)</div>""";
        private const string DoisGols =
            """<span title="Gols" class="zz-icn zz-icn-fut-11"></span><div>3' 34' </div>""";

        /// <summary>
        /// A ficha completa: uma linha com os dois XI, outra com os bancos e uma
        /// terceira com os treinadores — que é o formato que a página monta.
        /// </summary>
        private static string Ficha(
            string casa = "Cruzeiro", string visitante = "Corinthians",
            string titularesCasa = "", string titularesVisitante = "",
            string reservasCasa = "", string reservasVisitante = "") =>
            $"""
             <div id="game_report" class="card-data__body">
               <div class="zz-tpl-row game_report">
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle">{casa}</div>{titularesCasa}</div>
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle">{visitante}</div>{titularesVisitante}</div>
               </div>
               <div class="zz-tpl-row game_report mt">
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle">Reservas</div>{reservasCasa}</div>
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle">Reservas</div>{reservasVisitante}</div>
               </div>
               <div class="zz-tpl-row game_report mt">
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle">Treinadores</div>
                   <div class="player"><div class="number">&nbsp;</div><div class="name"><div class="text">
                     <a href="/treinador/jonas-urias/26713">Jonas Urias</a>
                   </div></div><div class="events"></div></div>
                 </div>
                 <div class="zz-tpl-col is-6 fl-c"><div class="subtitle"></div>
                   <div class="player"><div class="number">&nbsp;</div><div class="name"><div class="text">
                     <a href="/treinador/emily-lima/26714">Emily Lima</a>
                   </div></div><div class="events"></div></div>
                 </div>
               </div>
             </div>
             """;

        // ── Escalação ─────────────────────────────────────────────────────────

        [Fact]
        public void LerFicha_separa_titulares_reservas_e_ignora_treinadores()
        {
            var doc = Html(Ficha(
                titularesCasa: Jogadora("707695", "Cláudia Oliveira", "64"),
                titularesVisitante: Jogadora("532499", "Nicole Ramos", "1"),
                reservasCasa: Jogadora("532500", "Ketlin", "25", Entrou) +
                              Jogadora("532501", "Rafa Levis", "23", inativa: true),
                reservasVisitante: Jogadora("538450", "Victória Albuquerque", "17", Entrou)));

            var ficha = OgolEscalacaoService.LerFicha(doc, Jogo());

            Assert.NotNull(ficha);

            var casa = ficha!.Casa;
            Assert.Equal(3, casa.Count);

            var titular = Assert.Single(casa, a => a.Titular);
            Assert.Equal("Cláudia Oliveira", titular.Nome);
            Assert.Equal(64, titular.Numero);
            Assert.True(titular.Atuou);

            // Reserva que entrou joga; reserva marcada "inactive" ficou no banco — é essa
            // diferença que a tela de análise usa para montar o banco.
            Assert.True(casa.Single(a => a.Nome == "Ketlin").Atuou);
            Assert.False(casa.Single(a => a.Nome == "Rafa Levis").Atuou);

            // O treinador está na mesma estrutura .player, mas com link /treinador/ —
            // ele não pode virar jogadora do elenco.
            Assert.DoesNotContain(casa, a => a.Nome == "Jonas Urias");
            Assert.DoesNotContain(ficha.Visitante, a => a.Nome == "Emily Lima");

            Assert.Equal(2, ficha.Visitante.Count);
        }

        [Fact]
        public void LerFicha_recusa_ficha_de_outro_confronto()
        {
            // Página de outra partida (ou colunas invertidas): gravar isto poria a
            // escalação de um time no lugar do outro, então a leitura tem de falhar.
            var doc = Html(Ficha(
                casa: "Palmeiras", visitante: "Santos",
                titularesCasa: Jogadora("1", "Alguém", "1")));

            Assert.Null(OgolEscalacaoService.LerFicha(doc, Jogo()));
        }

        [Fact]
        public void LerFicha_sem_bloco_de_escalacao_devolve_null()
        {
            Assert.Null(OgolEscalacaoService.LerFicha(
                Html("<div class=\"card-data\">Escalações ainda não publicadas</div>"), Jogo()));
        }

        // ── Lances ────────────────────────────────────────────────────────────

        [Fact]
        public void LerLances_le_gol_cartao_e_setas_com_o_lado_certo()
        {
            var doc = Html(Ficha(
                titularesCasa: Jogadora("5", "Lorena Bedoya", "5", Amarelo + Saiu),
                titularesVisitante: Jogadora("9", "Jhonson", "9", Saiu),
                reservasCasa: Jogadora("15", "Michelle Romero", "15", Entrou),
                reservasVisitante: Jogadora("17", "Victória Albuquerque", "17",
                    Entrou + GolNoAcrescimo)));

            var lances = OgolEventosService.LerLances(doc, Jogo());
            Assert.NotNull(lances);

            var amarelo = Assert.Single(lances!, l => l.Tipo == OgolEventosService.TipoLance.Amarelo);
            Assert.Equal("Lorena Bedoya", amarelo.Nome);
            Assert.Equal(51, amarelo.Minuto);
            Assert.True(amarelo.EhCasa);

            var gol = Assert.Single(lances, l => l.Tipo == OgolEventosService.TipoLance.Gol);
            Assert.False(gol.EhCasa);
            Assert.False(gol.Contra);
            // "90+5'" vira 90: é a convenção da api-football que o resto do sistema usa.
            Assert.Equal(90, gol.Minuto);

            Assert.Equal(2, lances.Count(l => l.Tipo == OgolEventosService.TipoLance.Saiu));
            Assert.Equal(2, lances.Count(l => l.Tipo == OgolEventosService.TipoLance.Entrou));
        }

        [Fact]
        public void LerLances_marca_gol_contra_pela_anotacao_do_minuto()
        {
            var doc = Html(Ficha(
                titularesCasa: Jogadora("1", "Alguém", "1"),
                titularesVisitante: Jogadora("499312", "Zóio", "4", GolContra)));

            var gol = Assert.Single(
                OgolEventosService.LerLances(doc, Jogo())!,
                l => l.Tipo == OgolEventosService.TipoLance.Gol);

            Assert.True(gol.Contra);
            Assert.Equal(68, gol.Minuto);
            // O gol contra fica no cartão de quem marcou — o lado é o DELA, e é a marca
            // Contra que manda o gol para o placar do adversário.
            Assert.False(gol.EhCasa);
        }

        [Fact]
        public void LerLances_desdobra_o_icone_unico_de_quem_marcou_duas_vezes()
        {
            // O ogol não repete o desenho da bola: os dois gols da mesma jogadora vêm
            // como um ícone só, com os dois minutos no mesmo rótulo. Ler só o primeiro
            // deixava o placar da ficha menor que o do jogo, sem erro nenhum aparecer.
            var doc = Html(Ficha(
                titularesCasa: Jogadora("417349", "Djeni", "11", DoisGols),
                titularesVisitante: Jogadora("1", "Alguém", "1")));

            var gols = OgolEventosService.LerLances(doc, Jogo())!
                .Where(l => l.Tipo == OgolEventosService.TipoLance.Gol)
                .ToList();

            Assert.Equal(2, gols.Count);
            Assert.Equal(new[] { 3, 34 }, gols.Select(g => g.Minuto));
            Assert.All(gols, g => Assert.Equal("Djeni", g.Nome));
            Assert.All(gols, g => Assert.False(g.Contra));
        }

        [Fact]
        public void Minutos_le_cada_minuto_com_a_sua_anotacao()
        {
            Assert.Equal(new[] { (45, "pen."), (70, (string?)null) },
                OgolService.Minutos("45' (pen.) 70'"));
        }

        // ── Substituições ─────────────────────────────────────────────────────

        [Fact]
        public void Parear_junta_saida_e_entrada_do_mesmo_minuto()
        {
            var lances = new[]
            {
                Lance(OgolEventosService.TipoLance.Saiu, "Saiu A", 80),
                Lance(OgolEventosService.TipoLance.Saiu, "Saiu B", 80),
                Lance(OgolEventosService.TipoLance.Entrou, "Entrou A", 80),
                Lance(OgolEventosService.TipoLance.Entrou, "Entrou B", 80),
                Lance(OgolEventosService.TipoLance.Saiu, "Saiu C", 60),
                Lance(OgolEventosService.TipoLance.Entrou, "Entrou C", 60),
            };

            var pares = OgolEventosService.Parear(lances).ToList();

            Assert.Equal(3, pares.Count);
            // Ordenadas por minuto, e a troca dupla casa por ordem de leitura.
            Assert.Equal(60, pares[0].Minuto);
            Assert.Equal("Saiu C", pares[0].Saiu!.Nome);
            Assert.Equal("Entrou C", pares[0].Entrou!.Nome);
            Assert.Equal("Saiu A", pares[1].Saiu!.Nome);
            Assert.Equal("Entrou A", pares[1].Entrou!.Nome);
            Assert.Equal("Saiu B", pares[2].Saiu!.Nome);
            Assert.Equal("Entrou B", pares[2].Entrou!.Nome);
        }

        [Fact]
        public void Parear_deixa_metade_solta_com_o_outro_lado_nulo()
        {
            var pares = OgolEventosService.Parear(new[]
            {
                Lance(OgolEventosService.TipoLance.Saiu, "Só saiu", 70),
                Lance(OgolEventosService.TipoLance.Entrou, "Só entrou", 46),
            }).ToList();

            Assert.Equal(2, pares.Count);
            Assert.Null(pares[0].Saiu);
            Assert.Equal("Só entrou", pares[0].Entrou!.Nome);
            Assert.Equal("Só saiu", pares[1].Saiu!.Nome);
            Assert.Null(pares[1].Entrou);
        }

        private static OgolEventosService.LanceOgol Lance(
            OgolEventosService.TipoLance tipo, string nome, int minuto) =>
            new(tipo, nome, null, EhCasa: true, minuto, Contra: false);

        // ── Estatísticas ──────────────────────────────────────────────────────

        [Fact]
        public void LerEstatisticas_le_mandante_e_visitante_e_fica_com_a_primeira_ocorrencia()
        {
            // O bloco "Destaque" repete indicadores que também estão nas abas; vale a
            // primeira leitura, e o rótulo repetido não pode sobrescrever nada.
            var doc = Html("""
                <div class="graph-bar"><div class="bar-header">
                  <div class="num">47 %</div>
                  <div title="Posse de Bola" class="bars-title">Posse de Bola</div>
                  <div class="num">53 %</div>
                </div></div>
                <div class="graph-bar"><div class="bar-header">
                  <div class="num">7</div>
                  <div title="Chutes" class="bars-title">Chutes</div>
                  <div class="num">19</div>
                </div></div>
                <div class="graph-bar"><div class="bar-header">
                  <div class="num">99</div>
                  <div title="Chutes" class="bars-title">Chutes</div>
                  <div class="num">99</div>
                </div></div>
                """);

            var barras = OgolService.LerEstatisticas(doc);

            Assert.Equal((47d, 53d), barras["Posse de Bola"]);
            Assert.Equal((7d, 19d), barras["Chutes"]);
        }

        // ── Minuto ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("80'", 80)]
        [InlineData("90+5' ", 90)]
        [InlineData("68' (g.c.)", 68)]
        [InlineData("52' (pen.)", 52)]
        [InlineData("", 0)]
        public void Minuto_ignora_acrescimo_e_anotacao(string texto, int esperado) =>
            Assert.Equal(esperado, OgolService.Minuto(texto));
    }
}
