using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Importa os lances de uma partida (gols, cartões e substituições) do bloco de
    /// escalações do ogol.
    ///
    /// Existe pelo mesmo motivo do OgolEscalacaoService: no Brasileirão Feminino não
    /// falta só a escalação — falta o jogo inteiro. Sem isto o analista terminaria com o
    /// XI certo e o placar zerado.
    ///
    /// O ogol não publica lista de lances: ele desenha os ícones no cartão de CADA
    /// jogadora, com o minuto ao lado ("68' (g.c.)"). Isso muda duas coisas em relação à
    /// leitura da FIFA e da ESPN:
    ///   • quem marcou já vem identificada, e o lado sai da coluna em que ela está —
    ///     não há como pôr o lance no time errado;
    ///   • a substituição chega partida em duas metades (a seta de quem saiu no cartão
    ///     de uma, a de quem entrou no cartão de outra), e é aqui que elas voltam a ser
    ///     um par — ver <see cref="Parear"/>.
    ///
    /// Assistência não entra: o ogol não a publica no cartão da jogadora, e deduzi-la
    /// seria invenção. Ninguém é cadastrado aqui — quem aparece num lance e não está no
    /// elenco é divergência para conferir, exatamente como nas outras fontes.
    /// </summary>
    public class OgolEventosService
    {
        private readonly OgolService _ogol;
        private readonly ILogger<OgolEventosService> _logger;

        public OgolEventosService(OgolService ogol, ILogger<OgolEventosService> logger)
        {
            _ogol = ogol;
            _logger = logger;
        }

        public async Task<ResultadoOgol> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, idJogo, erro) = await _ogol.AbrirPartidaAsync(context, jogoId, ct);
            if (erro != null) return erro;

            var lances = LerLances(doc!, jogo!);
            if (lances == null)
                return new ResultadoOgol(false,
                    "O ogol não publicou a ficha desta partida (ou os times não casaram).", idJogo);

            if (lances.Count == 0)
                return new ResultadoOgol(false, "O ogol não registrou nenhum lance nesta partida.", idJogo);

            var elencos = new Dictionary<bool, List<Jogador>>
            {
                [true] = await EspnEscalacaoService.ElencoAsync(context, jogo!.TimeCasaId, ct),
                [false] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeVisitanteId, ct),
            };

            var naoResolvidos = new List<string>();

            Jogador? Resolver(LanceOgol lance)
            {
                var achado = EspnEscalacaoService.Casar(
                    elencos[lance.EhCasa], lance.Nome, lance.Numero)?.Jogador;
                if (achado == null) naoResolvidos.Add(lance.Nome);
                return achado;
            }

            var gols = new List<Gol>();
            var cartoes = new List<Cartao>();
            // Quantos gols cada lado marcou, contando o gol contra para o adversário.
            // Fica aqui porque o lado vem do lance (a coluna da ficha), e não da
            // escalação — que neste ponto pode nem estar gravada ainda.
            int golsCasa = 0, golsVisitante = 0;

            foreach (var lance in lances.Where(l => l.Tipo is TipoLance.Gol or TipoLance.Amarelo or TipoLance.Vermelho))
            {
                var jogadora = Resolver(lance);
                if (jogadora == null) continue;

                if (lance.Tipo == TipoLance.Gol)
                {
                    gols.Add(new Gol
                    {
                        JogoId = jogo.Id,
                        JogadorId = jogadora.Id,
                        Minuto = lance.Minuto,
                        Contra = lance.Contra,
                    });

                    if (lance.EhCasa != lance.Contra) golsCasa++; else golsVisitante++;
                }
                else
                    cartoes.Add(new Cartao
                    {
                        JogoId = jogo.Id,
                        JogadorId = jogadora.Id,
                        Minuto = lance.Minuto,
                        Tipo = lance.Tipo == TipoLance.Vermelho ? "Vermelho" : "Amarelo",
                    });
            }

            var substituicoes = new List<Substituicao>();
            foreach (var ehCasa in new[] { true, false })
                foreach (var (saiu, entrou, minuto) in Parear(lances.Where(l => l.EhCasa == ehCasa)))
                {
                    var quemSaiu = saiu == null ? null : Resolver(saiu);
                    var quemEntrou = entrou == null ? null : Resolver(entrou);
                    if (quemSaiu == null && quemEntrou == null) continue;

                    substituicoes.Add(new Substituicao
                    {
                        JogoId = jogo.Id,
                        JogadorSaiuId = quemSaiu?.Id,
                        JogadorEntrouId = quemEntrou?.Id,
                        Minuto = minuto,
                        IsTimeCasa = ehCasa,
                    });
                }

            var total = gols.Count + cartoes.Count + substituicoes.Count;
            if (total == 0)
                return new ResultadoOgol(false,
                    "O ogol tem a partida, mas nenhum lance dela casou com o elenco cadastrado.", idJogo);

            // Troca completa, como fazem a reimportação da api-football, a da ESPN e a
            // da FIFA: o que existia era digitado à mão ou de uma importação anterior, e
            // misturar duplicaria gol.
            context.Gols.RemoveRange(await context.Gols.Where(g => g.JogoId == jogo.Id).ToListAsync(ct));
            context.Cartoes.RemoveRange(await context.Cartoes.Where(c => c.JogoId == jogo.Id).ToListAsync(ct));
            context.Substituicoes.RemoveRange(await context.Substituicoes.Where(s => s.JogoId == jogo.Id).ToListAsync(ct));

            context.Gols.AddRange(gols);
            context.Cartoes.AddRange(cartoes);
            context.Substituicoes.AddRange(substituicoes);
            await context.SaveChangesAsync(ct);

            if (naoResolvidos.Count > 0)
                _logger.LogInformation(
                    "[OgolEventos] Jogo {Id}: {N} nome(s) sem correspondência no elenco: {Nomes}",
                    jogo.Id, naoResolvidos.Count, string.Join(", ", naoResolvidos.Distinct()));

            var partes = new List<string>();
            if (gols.Count > 0) partes.Add($"{gols.Count} gol(s)");
            if (cartoes.Count > 0) partes.Add($"{cartoes.Count} cartão(ões)");
            if (substituicoes.Count > 0) partes.Add($"{substituicoes.Count} substituição(ões)");

            var msg = $"Lances importados do ogol: {string.Join(", ", partes)}.";

            // Confere o que foi gravado contra o placar que a api-football já tinha. É a
            // única checagem possível aqui: o ogol não rotula o gol de pênalti de forma
            // diferente do normal, e um ícone que ele deixasse de desenhar passaria
            // despercebido. Divergência não impede a gravação — vira aviso, porque na
            // maioria das vezes o que está errado é o placar de um jogo ainda em
            // andamento, não a ficha.
            if (Divergencia(jogo, golsCasa, golsVisitante) is { } aviso) msg += " " + aviso;

            if (naoResolvidos.Count > 0)
                msg += $" {naoResolvidos.Distinct().Count()} nome(s) não casaram com o elenco e ficaram de fora.";

            return new ResultadoOgol(true, msg, idJogo);
        }

        /// <summary>
        /// Compara os gols lidos com o placar do jogo. Null quando batem (ou quando o
        /// jogo ainda não tem placar).
        /// </summary>
        private string? Divergencia(Jogo jogo, int golsCasa, int golsVisitante)
        {
            if (jogo.PlacarCasa is not { } placarCasa || jogo.PlacarVisitante is not { } placarVisitante)
                return null;

            if (golsCasa == placarCasa && golsVisitante == placarVisitante) return null;

            _logger.LogWarning(
                "[OgolEventos] Jogo {Id}: ogol trouxe {C}x{V} e o placar salvo é {PC}x{PV}.",
                jogo.Id, golsCasa, golsVisitante, placarCasa, placarVisitante);

            return $"Atenção: os gols do ogol somam {golsCasa}x{golsVisitante} e o placar do jogo é " +
                   $"{placarCasa}x{placarVisitante} — confira a ficha.";
        }

        // ── Leitura dos lances ───────────────────────────────────────────────

        internal enum TipoLance { Gol, Amarelo, Vermelho, Entrou, Saiu }

        internal record LanceOgol(
            TipoLance Tipo, string Nome, int? Numero, bool EhCasa, int Minuto, bool Contra);

        /// <summary>
        /// Todos os lances do bloco #game_report, já com o lado resolvido pela coluna em
        /// que a jogadora está. Null quando o bloco não existe ou os subtítulos não
        /// casam com os times do jogo — a mesma conferência que a escalação faz, e pela
        /// mesma razão: sem ela o gol iria para o time errado.
        /// </summary>
        internal static List<LanceOgol>? LerLances(HtmlDocument doc, Jogo jogo)
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

            var esquerda = OgolService.Texto(colunasPorLinha[0][0].SelectSingleNode(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' subtitle ')]"));
            var direita = OgolService.Texto(colunasPorLinha[0][1].SelectSingleNode(
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' subtitle ')]"));

            if (!TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, esquerda) ||
                !TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, direita))
                return null;

            var lances = new List<LanceOgol>();
            foreach (var linha in colunasPorLinha)
            {
                lances.AddRange(LerColuna(linha[0], ehCasa: true));
                lances.AddRange(LerColuna(linha[1], ehCasa: false));
            }

            return lances;
        }

        /// <summary>
        /// Os lances de uma coluna. Dentro de .events os ícones e os minutos são irmãos
        /// alternados (&lt;span&gt; do ícone, &lt;div&gt; do minuto), então cada minuto é
        /// o do ícone imediatamente anterior.
        /// </summary>
        private static IEnumerable<LanceOgol> LerColuna(HtmlNode coluna, bool ehCasa)
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

                var numero = int.TryParse(OgolService.Texto(no.SelectSingleNode(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), ' number ')]")), out var n)
                    ? n : (int?)null;

                var eventos = no.SelectSingleNode(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), ' events ')]");
                if (eventos == null) continue;

                TipoLance? pendente = null;
                foreach (var filho in eventos.ChildNodes.Where(f => f.NodeType == HtmlNodeType.Element))
                {
                    if (filho.Name.Equals("span", StringComparison.OrdinalIgnoreCase))
                    {
                        pendente = Classificar(filho);
                        continue;
                    }

                    if (pendente == null) continue;

                    // Um lance por MINUTO do rótulo, e não por ícone: quem marcou duas
                    // vezes ganha um desenho de bola só, com os dois minutos juntos
                    // ("3' 34'") — ver OgolService.Minutos.
                    foreach (var (minuto, anotacao) in OgolService.Minutos(OgolService.Texto(filho)))
                        yield return new LanceOgol(
                            pendente.Value, nome, numero, ehCasa, minuto,
                            // O ogol marca o gol contra no cartão de quem MARCOU, com a
                            // anotação junto do minuto ("68' (g.c.)"). "(pen.)" aparece do
                            // mesmo jeito, mas não muda nada: Gol não distingue pênalti.
                            Contra: anotacao?.Contains("g.c.", StringComparison.OrdinalIgnoreCase) == true);

                    pendente = null;
                }
            }
        }

        /// <summary>
        /// Que lance é o ícone. O título é o que identifica ("Gols", "Amarelos",
        /// "Vermelhos", "Entrou"); a seta de quem SAIU é a única sem título, e é
        /// reconhecida pelo caractere da fonte de ícones. Ícone desconhecido devolve
        /// null e é ignorado — é o erro barato quando o site acrescentar um símbolo novo.
        /// </summary>
        private static TipoLance? Classificar(HtmlNode span)
        {
            var titulo = span.GetAttributeValue("title", "").Trim();
            var classe = span.GetAttributeValue("class", "");

            if (titulo.Equals("Gols", StringComparison.OrdinalIgnoreCase)) return TipoLance.Gol;
            if (titulo.Equals("Amarelos", StringComparison.OrdinalIgnoreCase)) return TipoLance.Amarelo;
            if (titulo.Equals("Vermelhos", StringComparison.OrdinalIgnoreCase)) return TipoLance.Vermelho;
            if (titulo.Equals("Entrou", StringComparison.OrdinalIgnoreCase)) return TipoLance.Entrou;

            // "8" na fonte icn_zerozero é a seta para baixo (saiu); "7" é a de entrada,
            // que já foi tratada pelo título acima.
            if (classe.Contains("icn_zerozero", StringComparison.OrdinalIgnoreCase) &&
                OgolService.Texto(span) == "8")
                return TipoLance.Saiu;

            return null;
        }

        /// <summary>
        /// Junta as duas metades de cada substituição de um time: quem saiu e quem entrou
        /// chegam em cartões diferentes, e o que as liga é o minuto — o ogol grava o
        /// mesmo nos dois lados da troca.
        ///
        /// Troca dupla no mesmo minuto (comum no intervalo) casa por ordem de leitura,
        /// que é a ordem em que as jogadoras aparecem na ficha. Sobrando uma metade sem
        /// par — o ogol registrou só a saída, ou só a entrada — ela vira uma substituição
        /// com o outro lado nulo, que é o que Substituicao já modela para o mesmo buraco
        /// vindo da api-football.
        /// </summary>
        internal static IEnumerable<(LanceOgol? Saiu, LanceOgol? Entrou, int Minuto)> Parear(
            IEnumerable<LanceOgol> lances)
        {
            var porMinuto = lances
                .Where(l => l.Tipo is TipoLance.Saiu or TipoLance.Entrou)
                .GroupBy(l => l.Minuto)
                .OrderBy(g => g.Key);

            foreach (var grupo in porMinuto)
            {
                var sairam = grupo.Where(l => l.Tipo == TipoLance.Saiu).ToList();
                var entraram = grupo.Where(l => l.Tipo == TipoLance.Entrou).ToList();

                for (var i = 0; i < Math.Max(sairam.Count, entraram.Count); i++)
                    yield return (
                        i < sairam.Count ? sairam[i] : null,
                        i < entraram.Count ? entraram[i] : null,
                        grupo.Key);
            }
        }
    }
}
