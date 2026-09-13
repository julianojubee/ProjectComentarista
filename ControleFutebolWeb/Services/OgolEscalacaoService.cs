using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Aplica ao jogo a escalação que o ogol publica na ficha da partida (o bloco
    /// "Escalações", em #game_report).
    ///
    /// Vale só para o Brasileirão Feminino, que é a única liga do de-para
    /// wwwroot/data/ligas-ogol.json — ver OgolService. Não é fallback de ninguém: nessa
    /// competição a api-football traz o jogo sem escalação, e a ESPN e o FotMob nem
    /// catalogam a liga, então o ogol é a primeira e a última fonte.
    ///
    /// A REGRA de aplicação não está aqui — está em
    /// <see cref="EspnEscalacaoService.AplicarAtletasAsync"/>, que é fonte-agnóstica.
    /// Este serviço só traduz o HTML do ogol para a lista de
    /// <see cref="EspnEscalacaoService.AtletaEscalado"/> que aquele método consome,
    /// exatamente como o FifaEscalacaoService faz com o JSON da FIFA.
    /// </summary>
    public class OgolEscalacaoService
    {
        private readonly OgolService _ogol;
        private readonly EspnEscalacaoService _aplicador;
        private readonly ILogger<OgolEscalacaoService> _logger;

        public OgolEscalacaoService(
            OgolService ogol,
            EspnEscalacaoService aplicador,
            ILogger<OgolEscalacaoService> logger)
        {
            _ogol = ogol;
            _aplicador = aplicador;
            _logger = logger;
        }

        /// <param name="filtroLado">
        /// Recebe true para o mandante e false para o visitante; devolver false pula o
        /// lado. Mesmo contrato das outras fontes — serve ao fallback automático da
        /// reimportação, que só quer preencher o lado que ninguém trouxe.
        /// </param>
        public async Task<ResultadoOgol> AplicarAsync(
            FutebolContext context, int jogoId, string usuarioId,
            Func<bool, bool>? filtroLado = null, CancellationToken ct = default)
        {
            var (jogo, doc, idJogo, erro) = await _ogol.AbrirPartidaAsync(context, jogoId, ct);
            if (erro != null) return erro;

            var ficha = LerFicha(doc!, jogo!);
            if (ficha == null)
                return new ResultadoOgol(false,
                    "O ogol não publicou a escalação desta partida (ou os times não casaram).", idJogo);

            // A posição sai da ficha "performance", que é a única página do ogol que a
            // publica. Ela custa uma requisição a mais, mas sem posição o aplicador
            // empilha o XI inteiro na mesma linha do campinho.
            var posicoes = await LerPosicoesAsync(idJogo!, ct);

            var lados = 0;
            var criados = 0;

            foreach (var (ehCasa, atletas) in new[]
                     { (true, ficha.Casa), (false, ficha.Visitante) })
            {
                if (filtroLado != null && !filtroLado(ehCasa)) continue;
                if (atletas.Count == 0) continue;

                var resultado = await _aplicador.AplicarAtletasAsync(
                    context, jogo!, ehCasa,
                    // O ogol não publica a formação tática da partida — o aplicador cai
                    // na formação padrão do time, como com qualquer fonte sem esse dado.
                    formacaoDaFonte: null,
                    atletas.Select(a => new EspnEscalacaoService.AtletaEscalado(
                        a.Nome, a.Numero, posicoes.GetValueOrDefault(a.IdOgol, ""), a.Titular, a.Atuou)).ToList(),
                    usuarioId, FonteEscalacao.Ogol, ct);

                if (resultado == null) continue;
                lados++;
                criados += resultado.Value;
            }

            if (lados == 0)
                return new ResultadoOgol(false,
                    filtroLado == null
                        ? "O ogol não publicou a escalação desta partida."
                        : "O ogol não tinha o lado que faltava desta partida.",
                    idJogo);

            await context.SaveChangesAsync(ct);

            var msg = $"Escalação inicial de {lados} time(s) atualizada pelo ogol";
            msg += criados > 0 ? $" ({criados} jogadora(s) cadastrada(s) no elenco)." : ".";
            return new ResultadoOgol(true, msg, idJogo);
        }

        // ── Leitura da ficha ─────────────────────────────────────────────────

        /// <summary>Uma jogadora do bloco de escalações, ainda sem posição.</summary>
        internal record AtletaOgol(string IdOgol, string Nome, int? Numero, bool Titular, bool Atuou);

        internal record FichaOgol(List<AtletaOgol> Casa, List<AtletaOgol> Visitante);

        /// <summary>
        /// Lê o bloco #game_report: uma linha com os dois XI (o subtítulo de cada coluna
        /// é o nome do time), outra com os dois bancos ("Reservas") e uma terceira com os
        /// treinadores, que não entram aqui.
        ///
        /// A coluna da esquerda é a do mandante, mas isso é CONFERIDO pelos subtítulos da
        /// primeira linha em vez de assumido: gravar a escalação de um time no lugar do
        /// outro é pior do que não gravar nada. Sem essa conferência, devolve null.
        /// </summary>
        internal static FichaOgol? LerFicha(HtmlDocument doc, Jogo jogo)
        {
            var linhas = doc.DocumentNode.SelectNodes(
                "//div[@id='game_report']" +
                "//div[contains(concat(' ', normalize-space(@class), ' '), ' game_report ')]");
            if (linhas == null || linhas.Count == 0) return null;

            var colunasPorLinha = linhas
                .Select(l => l.SelectNodes(
                    "./div[contains(concat(' ', normalize-space(@class), ' '), ' zz-tpl-col ')]"))
                .Where(c => c != null && c.Count >= 2)
                .Select(c => c!.Take(2).ToList())
                .ToList();

            if (colunasPorLinha.Count == 0) return null;

            var titulares = colunasPorLinha[0];
            var nomeEsquerda = OgolService.Texto(titulares[0].SelectSingleNode(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' subtitle ')]"));
            var nomeDireita = OgolService.Texto(titulares[1].SelectSingleNode(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' subtitle ')]"));

            if (!TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeEsquerda) ||
                !TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeDireita))
                return null;

            var casa = LerJogadoras(titulares[0], titular: true).ToList();
            var visitante = LerJogadoras(titulares[1], titular: true).ToList();

            // A linha seguinte é a dos bancos. A dos treinadores vem depois e é
            // reconhecida pelos links, que apontam para /treinador/ e não para
            // /jogador/ — LerJogadoras só lê os segundos, então ela sai vazia sozinha.
            foreach (var linha in colunasPorLinha.Skip(1))
            {
                casa.AddRange(LerJogadoras(linha[0], titular: false));
                visitante.AddRange(LerJogadoras(linha[1], titular: false));
            }

            return casa.Count == 0 && visitante.Count == 0 ? null : new FichaOgol(casa, visitante);
        }

        /// <summary>
        /// As jogadoras de uma coluna. Cada uma é um .player com a camisa em .number, o
        /// nome no link para /jogador/{slug}/{id} e os lances em .events.
        ///
        /// A marca "inactive" no .player é o que separa "ficou no banco o jogo todo" de
        /// "entrou" — a mesma distinção que o "appearances" da ESPN dá, e ela importa
        /// porque a tela de análise usa "atuou" para montar o banco.
        /// </summary>
        private static IEnumerable<AtletaOgol> LerJogadoras(HtmlNode coluna, bool titular)
        {
            var nos = coluna.SelectNodes(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' player ')]");
            if (nos == null) yield break;

            foreach (var no in nos)
            {
                var link = no.SelectSingleNode(".//a[starts-with(@href, '/jogador/')]");
                if (link == null) continue;

                var nome = OgolService.Texto(link);
                if (nome.Length == 0) continue;

                var inativa = no.GetAttributeValue("class", "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains("inactive");

                yield return new AtletaOgol(
                    IdOgol: link.GetAttributeValue("href", "").Split('/').LastOrDefault() ?? "",
                    Nome: nome,
                    Numero: Camisa(no),
                    Titular: titular,
                    // Titular sempre atuou; reserva só quando o ogol não a marcou como
                    // inativa (é assim que ele desenha quem nem entrou).
                    Atuou: titular || !inativa);
            }
        }

        /// <summary>
        /// O número da camisa. Vem "&amp;nbsp;" na linha dos treinadores e pode faltar em
        /// jogo antigo, então null é um resultado esperado, não erro.
        /// </summary>
        private static int? Camisa(HtmlNode player)
        {
            var texto = OgolService.Texto(player.SelectSingleNode(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' number ')]"));

            return int.TryParse(texto, out var n) ? n : null;
        }

        // ── Posições ─────────────────────────────────────────────────────────

        /// <summary>
        /// Id da jogadora no ogol → posição, lida da ficha "performance". O ogol já
        /// escreve "Goleiro", "Defensor", "Meia" e "Atacante" — exatamente o vocabulário
        /// que ApiFootballService.MapearPosicao produz e que
        /// EspnEscalacaoService.RankLinha sabe interpretar, então não há tradução aqui.
        ///
        /// Dicionário vazio quando a página não respondeu: a escalação entra sem posição,
        /// o aplicador distribui pelos slots que sobrarem e o analista arrasta quem
        /// estiver trocado — o mesmo que acontece com as outras fontes sem coordenada.
        /// </summary>
        private async Task<Dictionary<string, string>> LerPosicoesAsync(
            string idJogo, CancellationToken ct)
        {
            var posicoes = new Dictionary<string, string>(StringComparer.Ordinal);

            var doc = await _ogol.AbrirPerformanceAsync(idJogo, ct);
            if (doc == null)
            {
                _logger.LogInformation(
                    "[OgolEscalacao] Partida {Id}: ficha de performance indisponível — escalação entra sem posição.",
                    idJogo);
                return posicoes;
            }

            var linhas = doc.DocumentNode.SelectNodes(
                "//tr[contains(concat(' ', normalize-space(@class), ' '), ' match_player_stats ')]");
            if (linhas == null) return posicoes;

            foreach (var linha in linhas)
            {
                var id = linha.GetAttributeValue("data-player-id", "");
                if (id.Length == 0 || posicoes.ContainsKey(id)) continue;

                // Nome e posição são dois <span> irmãos na primeira célula; o segundo é a
                // posição. A tabela não usa classe nenhuma neles, então é a ordem que
                // identifica — e por isso a leitura exige os dois.
                var spans = linha.SelectNodes(".//td[1]//span");
                if (spans == null || spans.Count < 2) continue;

                var posicao = OgolService.Texto(spans[1]);
                if (posicao.Length > 0) posicoes[id] = posicao;
            }

            return posicoes;
        }
    }
}
