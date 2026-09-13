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
        private readonly PainelJogoService _paineis;
        private readonly TooltipJogadorService _tooltip;

        public JogosPreJogoController(FutebolContext context, ILogger<JogosPreJogoController> logger,
            ApiFootballService transfermarkt, UserManager<ApplicationUser> userManager,
            PainelJogoService paineis, TooltipJogadorService tooltip)
        {
            _context = context;
            _logger = logger;
            _transfermarkt = transfermarkt;
            _userManager = userManager;
            _paineis = paineis;
            _tooltip = tooltip;
        }

        // URL do proxy de mídia para o PainelJogoService (que não tem IUrlHelper).
        private string? ImagemUrl(string url) => Url.Action("Imagem", "MediaProxy", new { url });

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
            var dados = await _paineis.PreJogoAsync(id, ImagemUrl);
            return dados == null ? NotFound() : Json(dados);
        }

        // GET: Jogos/Indisponiveis/5 — aba Indisponíveis do modal Pré-jogo.
        //
        // Lê só o banco: a busca na api-football acontece no "Reimportar dados", junto
        // com a escalação (ver ApiFootballService.AtualizarIndisponiveisAsync). Abrir o
        // modal não gasta chamada da API, e por isso pode ser aberto quantas vezes o
        // analista quiser durante a preparação do jogo.
        [HttpGet]
        public async Task<IActionResult> Indisponiveis(int id)
        {
            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var lista = await _context.JogosIndisponiveis
                .AsNoTracking()
                .Include(i => i.Jogador)
                .Where(i => i.JogoId == id)
                .ToListAsync();

            object Lado(int timeId, Time? time) => new
            {
                time = time?.Nome,
                escudo = string.IsNullOrWhiteSpace(time?.EscudoUrl) ? null : ImagemUrl(time!.EscudoUrl!),
                jogadores = lista
                    .Where(i => i.TimeId == timeId)
                    // Quem está fora antes de quem é dúvida, e dentro de cada grupo por
                    // nome: a primeira coisa que o analista procura é o desfalque certo.
                    .OrderBy(i => IndisponibilidadeHelper.EhDuvida(i.Tipo))
                    .ThenBy(i => i.Jogador != null ? i.Jogador.Nome : i.Nome)
                    .Select(i => new
                    {
                        jogadorId = i.JogadorId,
                        // Vinculado ao cadastro: vale o nosso nome, o mesmo que aparece
                        // no campinho do Analisar. Sem vínculo, o nome da fonte.
                        nome = i.Jogador != null ? i.Jogador.Nome : i.Nome,
                        foto = string.IsNullOrWhiteSpace(i.FotoUrl) ? null : ImagemUrl(i.FotoUrl!),
                        duvida = IndisponibilidadeHelper.EhDuvida(i.Tipo),
                        situacao = IndisponibilidadeHelper.Tipo(i.Tipo),
                        categoria = IndisponibilidadeHelper.Categoria(i.Motivo),
                        motivo = IndisponibilidadeHelper.Motivo(i.Motivo),
                    })
                    .ToList()
            };

            return Json(new
            {
                casa = Lado(jogo.TimeCasaId, jogo.TimeCasa),
                visitante = Lado(jogo.TimeVisitanteId, jogo.TimeVisitante),
                atualizadoEm = lista.Count > 0
                    ? lista.Max(i => i.AtualizadoEm).ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : null,
            });
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

            // Média, gols e assistências de cada jogador na competição/temporada DESTE
            // jogo, para o card do match-up mostrar como o cara vem no campeonato.
            var jogadorIds = new[] { t1, t2 }
                .Where(t => t != null)
                .SelectMany(t => t!.Escalacao.Select(e => e.Jogador.Id).Concat(t.Elenco.Select(j => j.Id)))
                .Distinct()
                .ToList();
            var desempenhos = await DesempenhosNaCompeticaoAsync(jogo, jogadorIds, uid);

            var inv = CultureInfo.InvariantCulture;

            DesempenhoNaCompeticao Desempenho(int jogadorId) =>
                desempenhos.TryGetValue(jogadorId, out var d) ? d : DesempenhoNaCompeticao.Vazio;
            string? Foto(string? fotoUrl) => string.IsNullOrEmpty(fotoUrl) ? null : ImagemUrl(fotoUrl);

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
                    foto = Foto(e.Jogador.FotoUrl),
                    media = Desempenho(e.Jogador.Id).Media,
                    gols = Desempenho(e.Jogador.Id).Gols,
                    assistencias = Desempenho(e.Jogador.Id).Assistencias,
                    x = Math.Round(e.PosicaoX, 2),
                    y = Math.Round(e.PosicaoY, 2),
                }),
                elenco = t.Elenco.Select(j => new
                {
                    id = j.Id,
                    numero = j.NumeroCamisa?.ToString() ?? "",
                    nome = j.Nome,
                    sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                    foto = Foto(j.FotoUrl),
                    media = Desempenho(j.Id).Media,
                    gols = Desempenho(j.Id).Gols,
                    assistencias = Desempenho(j.Id).Assistencias,
                }),
            };

            // Ficha e números do ℹ para quem aparece no campo e no banco: o mapa
            // da tela de análise não cobre o elenco inteiro que o match-up traz.
            var tooltip = await _tooltip.MontarPacoteAsync(
                jogadorIds, jogo, uid ?? string.Empty, url => ImagemUrl(url!));

            return Json(new
            {
                casa = Map(t1),
                visitante = Map(t2),
                nomeCasa = jogo.TimeCasa?.Nome,
                nomeVisitante = jogo.TimeVisitante?.Nome,
                tooltip,
            });
        }

        /// <summary>Números do jogador na competição/temporada do jogo em análise.</summary>
        private sealed record DesempenhoNaCompeticao(double? Media, int Gols, int Assistencias)
        {
            public static readonly DesempenhoNaCompeticao Vazio = new(null, 0, 0);
        }

        // Nota média, gols e assistências de cada jogador na mesma competição/temporada
        // do jogo. A média sai pela régua do resto do sistema: nota manual do usuário
        // quando existe, senão a automática do motor escolhido em /CriteriosNota sobre
        // a estatística importada (ver PerfilJogadorService/EquipeDaRodada).
        private async Task<Dictionary<int, DesempenhoNaCompeticao>> DesempenhosNaCompeticaoAsync(
            Jogo jogo, IReadOnlyCollection<int> jogadorIds, string? usuarioId)
        {
            var vazio = new Dictionary<int, DesempenhoNaCompeticao>();
            if (jogadorIds.Count == 0) return vazio;

            // Só jogos já terminados: o jogo em análise (e os futuros) não têm nota.
            var jogoIds = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == jogo.CompeticaoId
                         && j.Temporada == jogo.Temporada
                         && j.Id != jogo.Id
                         && j.PlacarCasa != null && j.PlacarVisitante != null)
                .Select(j => j.Id)
                .ToListAsync();

            if (jogoIds.Count == 0) return vazio;

            var notas = await _context.Notas.AsNoTracking()
                // Detalhes: o piso de merecimento conta os chips verdes x vermelhos.
                .Include(n => n.Detalhes)
                .Where(n => jogoIds.Contains(n.JogoId) && n.UsuarioId == usuarioId
                         && jogadorIds.Contains(n.JogadorId))
                .ToListAsync();

            // Minutos > 0 exclui o relacionado que não entrou: ele receberia a nota
            // base e puxaria a média de quem jogou pra baixo.
            var estatisticas = await _context.EstatisticasJogador.AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId) && e.Minutos > 0
                         && jogadorIds.Contains(e.JogadorId))
                .ToListAsync();

            // Gols (sem contra) e assistências nos mesmos jogos.
            var gols = await _context.Gols.AsNoTracking()
                .Where(g => jogoIds.Contains(g.JogoId) && !g.Contra && jogadorIds.Contains(g.JogadorId))
                .GroupBy(g => g.JogadorId)
                .Select(grp => new { JogadorId = grp.Key, Total = grp.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            var assistencias = await _context.Assistencias.AsNoTracking()
                .Where(a => jogoIds.Contains(a.JogoId) && jogadorIds.Contains(a.JogadorId))
                .GroupBy(a => a.JogadorId)
                .Select(grp => new { JogadorId = grp.Key, Total = grp.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            if (notas.Count == 0 && estatisticas.Count == 0 && gols.Count == 0 && assistencias.Count == 0)
                return vazio;

            var criterios = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync());

            var lados = await LadoJogadorHelper.CarregarAsync(_context, jogoIds, usuarioId);
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, jogoIds, usuarioId, lados);
            var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                _context, jogoIds, usuarioId, criterios, lados, contextos);

            var porJogador = new Dictionary<int, List<double>>();
            void Somar(int jogadorId, double nota)
            {
                if (!porJogador.TryGetValue(jogadorId, out var lista))
                    porJogador[jogadorId] = lista = new List<double>();
                lista.Add(nota);
            }

            var comNotaManual = new HashSet<(int JogadorId, int JogoId)>();
            foreach (var n in notas)
            {
                comNotaManual.Add((n.JogadorId, n.JogoId));
                Somar(n.JogadorId, n.NotaManual.HasValue
                    ? Math.Round(Math.Max(0, Math.Min(10, n.NotaManual.Value)), 2)
                    : CriteriosNotaHelper.NotaFinal(n.Valor, criterios,
                        ContextoNotaHelper.De(contextos, n.JogadorId, n.JogoId) with
                        {
                            Acoes = CriteriosNotaHelper.ContarAcoes(n.Detalhes)
                        }));
            }

            foreach (var e in estatisticas)
            {
                if (comNotaManual.Contains((e.JogadorId, e.JogoId))) continue;
                Somar(e.JogadorId, calculadora.De(e).Nota);
            }

            return porJogador.Keys.Concat(gols.Keys).Concat(assistencias.Keys).Distinct()
                .ToDictionary(id => id, id => new DesempenhoNaCompeticao(
                    porJogador.TryGetValue(id, out var notasDoJogador) && notasDoJogador.Count > 0
                        ? Math.Round(notasDoJogador.Average(), 1)
                        : (double?)null,
                    gols.TryGetValue(id, out var g) ? g : 0,
                    assistencias.TryGetValue(id, out var a) ? a : 0));
        }

        // GET: Jogos/PosJogo/5 — resumo pós-jogo (placar, notas dos jogadores,
        // observações digitadas na partida e estatísticas), no estilo do modal Pré-jogo.
        [HttpGet]
        public async Task<IActionResult> PosJogo(int id)
        {
            var usuarioId = _userManager.GetUserId(User);
            var dados = await _paineis.PosJogoAsync(id, usuarioId, ImagemUrl);
            return dados == null ? NotFound() : Json(dados);
        }
    }
}
