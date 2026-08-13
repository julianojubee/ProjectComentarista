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

        public JogosPreJogoController(FutebolContext context, ILogger<JogosPreJogoController> logger,
            ApiFootballService transfermarkt, UserManager<ApplicationUser> userManager,
            PainelJogoService paineis)
        {
            _context = context;
            _logger = logger;
            _transfermarkt = transfermarkt;
            _userManager = userManager;
            _paineis = paineis;
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
            var usuarioId = _userManager.GetUserId(User);
            var dados = await _paineis.PosJogoAsync(id, usuarioId, ImagemUrl);
            return dados == null ? NotFound() : Json(dados);
        }
    }
}
