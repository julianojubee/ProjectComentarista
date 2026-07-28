using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Painéis de contexto da partida em /Jogos/Analisar: modal Pré-jogo (resumo dos
    /// times, match-up, últimos confrontos) e modal Pós-jogo (placar, notas,
    /// observações e estatísticas). Só devolvem JSON, consumido por analisar.js.
    ///
    /// Rota declarada como "Jogos/[action]/{id?}" para preservar as URLs originais,
    /// que estão escritas literalmente no JS e trazem o id no caminho
    /// (/Jogos/PosJogo/5), como fazia a rota convencional {controller}/{action}/{id?}.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosPreJogoController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<JogosPreJogoController> _logger;
        private readonly ApiFootballService _transfermarkt;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosPreJogoController(FutebolContext context, ILogger<JogosPreJogoController> logger,
            ApiFootballService transfermarkt, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _transfermarkt = transfermarkt;
            _userManager = userManager;
        }

        // GET: Jogos/UltimosConfrontos/5 — retorna JSON com últimos H2H
        [HttpGet]
        public async Task<IActionResult> UltimosConfrontos(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            if (jogo.TimeCasa?.IdApi == 0 || jogo.TimeVisitante?.IdApi == 0)
                return BadRequest("Um dos times não tem ID da API configurado.");

            try
            {
                var confrontos = await _transfermarkt.BuscarH2HAsync(
                    jogo.TimeCasa!.IdApi, jogo.TimeVisitante!.IdApi, 5,
                    HttpContext.RequestAborted);

                var resultado = confrontos.Select(f => new
                {
                    data = f.Fixture.Date?.ToString("dd/MM/yyyy") ?? "-",
                    competicao = f.League.Name,
                    temporada = f.League.Season,
                    mandante = f.Teams.Home.Name,
                    visitante = f.Teams.Away.Name,
                    placarMandante = f.Goals.Home,
                    placarVisitante = f.Goals.Away,
                    logoMandante = f.Teams.Home.Logo,
                    logoVisitante = f.Teams.Away.Logo,
                    status = f.Fixture.Status.Short,
                    vencedor = f.Teams.Home.Winner == true ? "home"
                             : f.Teams.Away.Winner == true ? "away"
                             : "draw"
                }).ToList();

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[H2H] Erro ao buscar confrontos para jogo {Id}", id);
                return StatusCode(500, "Erro ao buscar confrontos na API.");
            }
        }

        // GET: Jogos/PreJogo/5 — resumo pré-jogo (V/E/D, forma, destaques, observações)
        // dos dois times, calculado a partir do banco local (sem depender da API externa).
        [HttpGet]
        public async Task<IActionResult> PreJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var casa = await MontarResumoPreJogoAsync(jogo, jogo.TimeCasaId, jogo.TimeCasa);
            var visitante = await MontarResumoPreJogoAsync(jogo, jogo.TimeVisitanteId, jogo.TimeVisitante);

            return Json(new { casa, visitante });
        }

        // GET: Jogos/MatchUpPreJogo/5 — aba Match-up do modal Pré-jogo.
        // Mesmo esquema da aba Match Up de /Relatorios (via MatchUpHelper): última
        // escalação titular registrada de cada time no campo horizontal compartilhado,
        // com o restante do elenco no banco. Somente visual — nada é salvo.
        [HttpGet]
        public async Task<IActionResult> MatchUpPreJogo(int id)
        {
            var uid = _userManager.GetUserId(User);

            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var t1 = await MatchUpHelper.MontarTimeAsync(_context, jogo.TimeCasaId, esquerda: true, uid);
            var t2 = await MatchUpHelper.MontarTimeAsync(_context, jogo.TimeVisitanteId, esquerda: false, uid);

            var inv = CultureInfo.InvariantCulture;

            object? Map(MatchUpTimeViewModel? t) => t == null ? null : new
            {
                nome = t.Time.Nome,
                escudo = string.IsNullOrEmpty(t.Time.EscudoUrl)
                    ? null
                    : Url.Action("Imagem", "MediaProxy", new { url = t.Time.EscudoUrl }),
                adversario = t.JogoOrigemEhCasa ? t.JogoOrigem?.TimeVisitante?.Nome : t.JogoOrigem?.TimeCasa?.Nome,
                data = t.JogoOrigem?.Data?.ToString("dd/MM/yyyy"),
                escalacao = t.Escalacao.Select(e => new
                {
                    id = e.Jogador.Id,
                    numero = e.Jogador.NumeroCamisa?.ToString() ?? "",
                    nome = e.Jogador.Nome,
                    sigla = PosicaoJogadorHelper.Sigla(e.Posicao),
                    x = Math.Round(e.PosicaoX, 2),
                    y = Math.Round(e.PosicaoY, 2),
                }),
                elenco = t.Elenco.Select(j => new
                {
                    id = j.Id,
                    numero = j.NumeroCamisa?.ToString() ?? "",
                    nome = j.Nome,
                    sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                }),
            };

            return Json(new
            {
                casa = Map(t1),
                visitante = Map(t2),
                nomeCasa = jogo.TimeCasa?.Nome,
                nomeVisitante = jogo.TimeVisitante?.Nome,
            });
        }

        // GET: Jogos/PosJogo/5 — resumo pós-jogo (placar, notas dos jogadores,
        // observações digitadas na partida e estatísticas), no estilo do modal Pré-jogo.
        [HttpGet]
        public async Task<IActionResult> PosJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var usuarioId = _userManager.GetUserId(User);

            // ── Notas dos jogadores (do usuário atual) ─────────────────────────
            // Nota "oficial" do jogador é sempre a calculada nos rankings (base fixa +
            // ações), nunca a manual. Quando o usuário nunca abriu a avaliação desse
            // jogador nessa partida (não existe linha em Notas), o ranking em
            // JogadoresController ainda mostra uma "nota automática" calculada em cima
            // das estatísticas importadas (EstatisticaJogador) — reproduz o mesmo
            // fallback aqui, senão jogadores só com estatística importada (ex.: Harry
            // Kane num jogo nunca avaliado manualmente) ficam sem nota no pós-jogo.
            double NotaFinal(double notaValor) =>
                Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + notaValor)), 1);

            var notas = await _context.Notas
                .Where(n => n.JogoId == id && n.UsuarioId == usuarioId)
                .ToListAsync();
            var notasPorJogador = notas.ToDictionary(n => n.JogadorId, n => NotaFinal(n.Valor));

            // A api-football importa uma linha de EstatisticaJogador pra todo o elenco
            // relacionado, inclusive quem ficou no banco o jogo inteiro (com Minutos
            // zerado/nulo) — só entra no fallback quem de fato jogou, senão reservas
            // que não entraram apareceriam com nota (4,0, só a base) igual quem jogou.
            var jogadorIdsComNotaManual = notasPorJogador.Keys.ToHashSet();
            var estatisticasJogo = await _context.EstatisticasJogador
                .Where(e => e.JogoId == id && !jogadorIdsComNotaManual.Contains(e.JogadorId) && e.Minutos > 0)
                .ToListAsync();
            if (estatisticasJogo.Count > 0)
            {
                var criteriosCompartilhados = await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync();
                var criteriosUsuario = await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync();
                var criteriosBanco = CriteriosNotaHelper.MergeCriterios(criteriosCompartilhados, criteriosUsuario);

                foreach (var e in estatisticasJogo)
                    notasPorJogador[e.JogadorId] = NotaFinal(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco));
            }

            double? NotaDe(int? jogadorId) =>
                jogadorId.HasValue && notasPorJogador.TryGetValue(jogadorId.Value, out var v) ? v : (double?)null;

            // ── Cartões, substituições, gols e assistências (ícones de evento) ──
            var cartoes = await _context.Cartoes.Where(c => c.JogoId == id).ToListAsync();
            var substituicoes = await _context.Substituicoes.Where(s => s.JogoId == id).ToListAsync();
            var golsJogo = await _context.Gols.Where(g => g.JogoId == id && !g.Contra).ToListAsync();
            var assistsJogo = await _context.Assistencias.Where(a => a.JogoId == id).ToListAsync();

            // ── Escalação inicial — usada tanto na lista (com os eventos ocorridos
            // durante a partida) quanto no campinho, que mostra a formação de quem
            // começou o jogo, não a formação final pós-substituições. ──
            var escInicial = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == "INICIAL")
                .ToListAsync();

            // Posição granular (ex.: "Lateral Direito") a partir da coordenada do slot
            // na formação usada nesse jogo — mesma lógica do histórico de jogador em
            // /Jogadores/Estatisticas. Cai pra categoria ampla (Escalacao.Posicao,
            // "Goleiro"/"Defensor"/...) quando não dá pra casar com uma formação.
            var slotsPorFormacao = (await _context.PosicoesFormacao.ToListAsync())
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());
            string? PosicaoGranularDe(Escalacao e)
            {
                var formacaoId = e.IsTimeCasa ? jogo.FormacaoCasaId : jogo.FormacaoVisitanteId;
                if (formacaoId == null || !slotsPorFormacao.TryGetValue(formacaoId.Value, out var slots))
                    return null;
                return PosicaoJogadorHelper.PosicaoGranular(slots, e.PosicaoX, e.PosicaoY);
            }

            object MontarJogadorLista(Escalacao e)
            {
                var j = e.Jogador!;
                return new
                {
                    jogadorId = j.Id,
                    nome = j.Nome,
                    numero = j.NumeroCamisa,
                    titular = e.Titular,
                    posicao = PosicaoGranularDe(e) ?? e.Posicao,
                    nota = NotaDe(j.Id),
                    gols = golsJogo.Count(g => g.JogadorId == j.Id),
                    assistencias = assistsJogo.Count(a => a.JogadorId == j.Id),
                    cartoesAmarelos = cartoes.Where(c => c.JogadorId == j.Id && c.Tipo == "Amarelo").Select(c => c.Minuto).OrderBy(m => m).ToList(),
                    cartaoVermelho = cartoes.Where(c => c.JogadorId == j.Id && c.Tipo == "Vermelho").Select(c => (int?)c.Minuto).FirstOrDefault(),
                    saiuMinuto = substituicoes.Where(s => s.JogadorSaiuId == j.Id).Select(s => (int?)s.Minuto).FirstOrDefault(),
                    entrouMinuto = substituicoes.Where(s => s.JogadorEntrouId == j.Id).Select(s => (int?)s.Minuto).FirstOrDefault()
                };
            }

            object MontarJogadorCampo(Escalacao e)
            {
                var j = e.Jogador!;
                return new
                {
                    jogadorId = j.Id,
                    nome = j.Nome,
                    numero = j.NumeroCamisa,
                    posicaoX = e.PosicaoX,
                    posicaoY = e.PosicaoY,
                    nota = NotaDe(j.Id),
                    gols = golsJogo.Count(g => g.JogadorId == j.Id),
                    assistencias = assistsJogo.Count(a => a.JogadorId == j.Id)
                };
            }

            // Lista ordenada por posição em campo (goleiro → defensor → meia →
            // atacante), como num escrete real — Escalacao.Posicao guarda essas
            // categorias amplas ("Goleiro"/"Defensor"/"Meia"/"Atacante").
            int OrdemPosicao(Escalacao e) => (e.Posicao ?? "").Trim().ToUpperInvariant() switch
            {
                "GOLEIRO" => 0,
                "DEFENSOR" => 1,
                "MEIA" => 2,
                "ATACANTE" => 3,
                _ => 4,
            };
            int Numero(Escalacao e) => e.Jogador?.NumeroCamisa ?? 999;

            var lineup = new
            {
                casaTitulares = escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                casaReservas = escInicial.Where(e => e.IsTimeCasa && !e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                visTitulares = escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                visReservas = escInicial.Where(e => !e.IsTimeCasa && !e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
            };

            // ── Média de nota dos titulares (cabeçalho do modal) ────────────────
            double? Media(IEnumerable<Escalacao> titulares)
            {
                var valores = titulares.Select(e => NotaDe(e.Jogador?.Id)).Where(n => n.HasValue).Select(n => n!.Value).ToList();
                return valores.Count > 0 ? Math.Round(valores.Average(), 1) : (double?)null;
            }
            var mediaCasa = Media(escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null));
            var mediaVisitante = Media(escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null));

            // ── Forma recente (últimos 5 jogos até esta partida, na mesma
            // competição/temporada) — mesmo padrão do modal Pré-jogo. ────────────
            async Task<List<string>> FormaRecenteAsync(int timeId)
            {
                var jogosTime = await _context.Jogos
                    .Where(j => j.CompeticaoId == jogo.CompeticaoId
                             && j.Temporada == jogo.Temporada
                             && j.Id != id
                             && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                             && j.PlacarCasa != null && j.PlacarVisitante != null
                             && (jogo.Data == null || j.Data <= jogo.Data))
                    .OrderByDescending(j => j.Data)
                    .Take(5)
                    .Select(j => new { j.TimeCasaId, j.PlacarCasa, j.PlacarVisitante })
                    .ToListAsync();

                var resultado = jogosTime.Select(j =>
                {
                    bool ehCasa = j.TimeCasaId == timeId;
                    int golsTime = (ehCasa ? j.PlacarCasa : j.PlacarVisitante) ?? 0;
                    int golsOpp = (ehCasa ? j.PlacarVisitante : j.PlacarCasa) ?? 0;
                    return golsTime > golsOpp ? "V" : golsTime == golsOpp ? "E" : "D";
                }).ToList();
                resultado.Reverse();
                return resultado;
            }
            var formaCasa = await FormaRecenteAsync(jogo.TimeCasaId);
            var formaVisitante = await FormaRecenteAsync(jogo.TimeVisitanteId);

            var campo = new
            {
                casa = escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null).Select(MontarJogadorCampo).ToList(),
                visitante = escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null).Select(MontarJogadorCampo).ToList(),
            };

            // ── Observações digitadas na partida (tags do usuário atual) ──────
            var observacoes = await _context.ObservacoesJogoTag
                .Include(o => o.Jogador)
                .Where(o => o.JogoId == id && o.UsuarioId == usuarioId)
                .OrderBy(o => o.Ordem)
                .Select(o => new { id = o.Id, tipo = o.Tipo, jogadorNome = o.Jogador != null ? o.Jogador.Nome : null, texto = o.Texto })
                .ToListAsync();

            // ── Estatísticas da partida (mesma fonte usada no painel de Estatísticas) ──
            var statsCasa = new Dictionary<string, string>();
            var statsVis = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(jogo.EstatisticasJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(jogo.EstatisticasJson);
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var timeId = item.GetProperty("TimeId").GetInt32();
                        var stats = new Dictionary<string, string>();
                        if (item.TryGetProperty("Stats", out var statsEl))
                        {
                            foreach (var prop in statsEl.EnumerateObject())
                                stats[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                                    ? "0" : (prop.Value.GetString() ?? "0");
                        }

                        if (timeId == jogo.TimeCasa?.IdApi) statsCasa = stats;
                        else if (timeId == jogo.TimeVisitante?.IdApi) statsVis = stats;
                    }
                }
                catch { /* JSON inválido/antigo — ignora */ }
            }

            string Pegar(Dictionary<string, string> s, string k) => s.TryGetValue(k, out var v) ? v : "0";

            var metricas = new (string label, string chave)[]
            {
                ("Posse de bola", "Ball Possession"),
                ("Finalizações totais", "Total Shots"),
                ("Finalizações no gol", "Shots on Goal"),
                ("Escanteios", "Corner Kicks"),
                ("Faltas", "Fouls"),
                ("Cartões amarelos", "Yellow Cards"),
                ("Cartões vermelhos", "Red Cards"),
                ("Defesas do goleiro", "Goalkeeper Saves"),
            };

            var estatisticas = metricas
                .Where(m => statsCasa.ContainsKey(m.chave) || statsVis.ContainsKey(m.chave))
                .Select(m => new { label = m.label, casa = Pegar(statsCasa, m.chave), vis = Pegar(statsVis, m.chave) })
                .ToList();

            // ── Estatísticas por jogador (destaque de cada time por métrica) ────
            // Mesma fonte da nota automática (EstatisticaJogador, importada da
            // api-football) — para cada métrica, pega quem mais se destacou em cada
            // time e compara os dois, igual ao painel "Estatísticas Jogador".
            var estatisticasJogadores = await _context.EstatisticasJogador
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
                .ToListAsync();

            var isCasaPorJogador = escInicial
                .Where(e => e.JogadorId.HasValue)
                .ToDictionary(e => e.JogadorId!.Value, e => e.IsTimeCasa);
            bool EhCasaJogador(Jogador j) =>
                isCasaPorJogador.TryGetValue(j.Id, out var isCasa) ? isCasa : (j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId);

            var metricasJogador = new (string label, Func<EstatisticaJogador, int> valor)[]
            {
                ("Chutes", e => e.FinalizacoesTotal),
                ("Chutes a gol", e => e.FinalizacoesNoGol),
                ("Duelos disputados", e => e.DuelosTotal),
                ("Duelos ganhos", e => e.DuelosVencidos),
                ("Passes", e => e.PassesTotal),
                ("Passes-chave", e => e.PassesChave),
                ("Defesas", e => e.Defesas),
                ("Faltas cometidas", e => e.FaltasCometidas),
                ("Faltas sofridas", e => e.FaltasSofridas),
            };

            object? MelhorJogadorDaMetrica(IEnumerable<EstatisticaJogador> lista, Func<EstatisticaJogador, int> valor)
            {
                var melhor = lista.OrderByDescending(valor).FirstOrDefault();
                if (melhor == null || valor(melhor) <= 0) return null;
                return new
                {
                    nome = melhor.Jogador.Nome,
                    foto = string.IsNullOrEmpty(melhor.Jogador.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = melhor.Jogador.FotoUrl }),
                    valor = valor(melhor)
                };
            }

            var estatJogadoresCasa = estatisticasJogadores.Where(e => EhCasaJogador(e.Jogador)).ToList();
            var estatJogadoresVis = estatisticasJogadores.Where(e => !EhCasaJogador(e.Jogador)).ToList();

            var estatisticasJogador = metricasJogador
                .Select(m => new
                {
                    label = m.label,
                    casa = MelhorJogadorDaMetrica(estatJogadoresCasa, m.valor),
                    vis = MelhorJogadorDaMetrica(estatJogadoresVis, m.valor),
                })
                .Where(x => x.casa != null || x.vis != null)
                .ToList();

            // ── Resumo textual automático ──────────────────────────────────────
            var totalGols = await _context.Gols.CountAsync(g => g.JogoId == id && !g.Contra);
            var cartoesAmarelos = await _context.Cartoes.CountAsync(c => c.JogoId == id && c.Tipo == "Amarelo");
            var cartoesVermelhos = await _context.Cartoes.CountAsync(c => c.JogoId == id && c.Tipo == "Vermelho");

            var resumo = new List<string>();
            if (jogo.PlacarCasa.HasValue && jogo.PlacarVisitante.HasValue)
            {
                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                    resumo.Add($"{jogo.TimeCasa?.Nome} venceu {jogo.TimeVisitante?.Nome} por {jogo.PlacarCasa} a {jogo.PlacarVisitante}.");
                else if (jogo.PlacarVisitante > jogo.PlacarCasa)
                    resumo.Add($"{jogo.TimeVisitante?.Nome} venceu {jogo.TimeCasa?.Nome} por {jogo.PlacarVisitante} a {jogo.PlacarCasa}.");
                else
                    resumo.Add($"Empate entre {jogo.TimeCasa?.Nome} e {jogo.TimeVisitante?.Nome} em {jogo.PlacarCasa} a {jogo.PlacarVisitante}.");
            }
            if (totalGols > 0 || cartoesAmarelos > 0 || cartoesVermelhos > 0)
            {
                var partes = new List<string> { $"{totalGols} gol{(totalGols != 1 ? "s" : "")}" };
                if (cartoesAmarelos > 0) partes.Add($"{cartoesAmarelos} cartão(ões) amarelo(s)");
                if (cartoesVermelhos > 0) partes.Add($"{cartoesVermelhos} cartão(ões) vermelho(s)");
                resumo.Add(string.Join(", ", partes) + " na partida.");
            }
            return Json(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVisitante = jogo.PlacarVisitante,
                penaltisCasa = jogo.PenaltisCasa,
                penaltisVisitante = jogo.PenaltisVisitante,
                competicao = jogo.Competicao?.Nome,
                rodada = jogo.Rodada > 0 ? jogo.Rodada : (int?)null,
                data = DateHelper.FormatarData(jogo.Data, "dd/MM/yyyy · HH:mm"),
                casa = new { nome = jogo.TimeCasa?.Nome, escudo = string.IsNullOrEmpty(jogo.TimeCasa?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = jogo.TimeCasa.EscudoUrl }) },
                visitante = new { nome = jogo.TimeVisitante?.Nome, escudo = string.IsNullOrEmpty(jogo.TimeVisitante?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = jogo.TimeVisitante.EscudoUrl }) },
                mediaCasa,
                mediaVisitante,
                formaCasa,
                formaVisitante,
                resumo,
                lineup,
                campo,
                observacoes,
                estatisticas,
                estatisticasJogador
            });
        }

        // Resumo de um time para o modal Pré-jogo, restrito à mesma competição/temporada do jogo.
        private async Task<object> MontarResumoPreJogoAsync(Jogo jogo, int timeId, Time? time)
        {
            // Jogos finalizados do time na mesma competição/temporada (mais recentes primeiro)
            var jogosTime = await _context.Jogos
                .Where(j => j.CompeticaoId == jogo.CompeticaoId
                         && j.Temporada == jogo.Temporada
                         && j.Id != jogo.Id
                         && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                         && j.PlacarCasa != null && j.PlacarVisitante != null)
                .OrderByDescending(j => j.Data)
                .Select(j => new { j.TimeCasaId, j.PlacarCasa, j.PlacarVisitante })
                .ToListAsync();

            int v = 0, e = 0, d = 0, golsPro = 0, golsContra = 0;
            var form = new List<string>();
            foreach (var j in jogosTime)
            {
                bool ehCasa = j.TimeCasaId == timeId;
                int golsTime = (ehCasa ? j.PlacarCasa : j.PlacarVisitante) ?? 0;
                int golsOpp = (ehCasa ? j.PlacarVisitante : j.PlacarCasa) ?? 0;
                golsPro += golsTime;
                golsContra += golsOpp;

                string r = golsTime > golsOpp ? "V" : golsTime == golsOpp ? "E" : "D";
                if (r == "V") v++; else if (r == "E") e++; else d++;
                if (form.Count < 5) form.Add(r);
            }
            int total = v + e + d;
            int aproveitamento = total > 0 ? (int)Math.Round((v * 3 + e) * 100.0 / (total * 3)) : 0;

            // Artilheiros do time na competição/temporada
            var artilheiros = await _context.Gols
                .Where(g => g.Jogo.CompeticaoId == jogo.CompeticaoId
                         && g.Jogo.Temporada == jogo.Temporada
                         && !g.Contra
                         && (g.Jogador.TimeId == timeId || g.Jogador.SelecaoId == timeId))
                .GroupBy(g => new { g.JogadorId, g.Jogador.Nome, g.Jogador.FotoUrl })
                .Select(grp => new { grp.Key.JogadorId, grp.Key.Nome, grp.Key.FotoUrl, Gols = grp.Count() })
                .OrderByDescending(x => x.Gols)
                .Take(5)
                .ToListAsync();

            // Assistências do time na competição/temporada (mapa jogador → total)
            var assistsLista = await _context.Assistencias
                .Where(a => a.Jogo.CompeticaoId == jogo.CompeticaoId
                         && a.Jogo.Temporada == jogo.Temporada
                         && (a.Jogador.TimeId == timeId || a.Jogador.SelecaoId == timeId))
                .GroupBy(a => new { a.JogadorId, a.Jogador.Nome, a.Jogador.FotoUrl })
                .Select(grp => new { grp.Key.JogadorId, grp.Key.Nome, grp.Key.FotoUrl, Assists = grp.Count() })
                .OrderByDescending(x => x.Assists)
                .ToListAsync();
            var assistsMap = assistsLista.ToDictionary(x => x.JogadorId, x => x.Assists);

            // Destaques: artilheiros + até 2 maiores assistentes que ainda não apareceram
            var idsArtilheiros = artilheiros.Select(a => a.JogadorId).ToHashSet();
            var destaques = artilheiros
                .Select(a => new
                {
                    nome = a.Nome,
                    foto = string.IsNullOrEmpty(a.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = a.FotoUrl }),
                    gols = a.Gols,
                    assists = assistsMap.TryGetValue(a.JogadorId, out var asi) ? asi : 0
                })
                .Concat(assistsLista
                    .Where(a => !idsArtilheiros.Contains(a.JogadorId))
                    .Take(2)
                    .Select(a => new
                    {
                        nome = a.Nome,
                        foto = string.IsNullOrEmpty(a.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = a.FotoUrl }),
                        gols = 0,
                        assists = a.Assists
                    }))
                .ToList();

            // Observações automáticas a partir dos números
            var observacoes = new List<string>();
            if (total == 0)
            {
                observacoes.Add("Sem jogos finalizados nesta competição/temporada.");
            }
            else
            {
                observacoes.Add($"{v}V · {e}E · {d}D em {total} jogo(s) — {aproveitamento}% de aproveitamento.");
                int saldo = golsPro - golsContra;
                observacoes.Add($"Gols: {golsPro} marcados, {golsContra} sofridos (saldo {(saldo >= 0 ? "+" : "")}{saldo}).");

                // Sequência atual (a partir do jogo mais recente)
                if (form.Count > 0)
                {
                    string atual = form[0];
                    int seq = 0;
                    foreach (var f in form) { if (f == atual) seq++; else break; }
                    if (seq > 1)
                    {
                        string plural = atual == "V" ? "vitórias" : atual == "E" ? "empates" : "derrotas";
                        observacoes.Add($"Sequência de {seq} {plural}.");
                    }
                }

                if (destaques.Count > 0 && destaques[0].gols > 0)
                {
                    var art = destaques[0];
                    observacoes.Add($"Destaque: {art.nome} ({art.gols} gol{(art.gols > 1 ? "s" : "")}).");
                }
            }

            return new
            {
                nome = time?.Nome,
                escudo = string.IsNullOrEmpty(time?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = time!.EscudoUrl }),
                vitorias = v,
                empates = e,
                derrotas = d,
                jogos = total,
                golsPro,
                golsContra,
                aproveitamento,
                form,
                destaques,
                observacoes
            };
        }
    }
}
