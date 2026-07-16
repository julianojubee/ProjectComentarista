using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/jogos")]
    public class JogosApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosApiController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET api/v1/jogos?competicaoId=&data=yyyy-MM-dd&page=&pageSize=
        // data: filtra os jogos daquele dia no fuso do Brasil (mesma conversão do
        // /Jogos/Hoje da web — jogos ficam em UTC no banco).
        [HttpGet]
        public async Task<ActionResult<IEnumerable<JogoResumoDto>>> Listar(
            int? competicaoId, DateTime? data, int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .AsQueryable();

            if (competicaoId.HasValue)
                query = query.Where(j => j.CompeticaoId == competicaoId.Value);

            if (data.HasValue)
            {
                var fusoBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                var diaBrasil = data.Value.Date;
                var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(diaBrasil, DateTimeKind.Unspecified), fusoBrasil);
                var fimUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(diaBrasil.AddDays(1), DateTimeKind.Unspecified), fusoBrasil);
                query = query.Where(j => j.Data >= inicioUtc && j.Data < fimUtc);
            }

            // Dia específico: ordem cronológica (como /Jogos/Hoje). Sem filtro: mais recentes primeiro.
            query = data.HasValue
                ? query.OrderBy(j => j.Data)
                : query.OrderByDescending(j => j.Data);

            var jogos = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var analisadosIds = await JogosAnalisadosDoUsuarioAsync(jogos.Select(j => j.Id));

            return Ok(jogos.Select(j => ParaResumoDto(j, analisadosIds.Contains(j.Id))));
        }

        // GET api/v1/jogos/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<JogoDetalheDto>> Detalhe(int id)
        {
            var uid = _userManager.GetUserId(User);

            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var analisadosIds = await JogosAnalisadosDoUsuarioAsync(new[] { jogo.Id });

            var dto = new JogoDetalheDto
            {
                Id = jogo.Id,
                Data = jogo.Data,
                CompeticaoId = jogo.CompeticaoId,
                CompeticaoNome = jogo.Competicao?.Nome,
                TimeCasaId = jogo.TimeCasaId,
                TimeCasaNome = jogo.TimeCasa?.Nome ?? string.Empty,
                TimeCasaEscudoUrl = Url.FotoSrcAbsoluto(Request, jogo.TimeCasa?.EscudoUrl),
                TimeVisitanteId = jogo.TimeVisitanteId,
                TimeVisitanteNome = jogo.TimeVisitante?.Nome ?? string.Empty,
                TimeVisitanteEscudoUrl = Url.FotoSrcAbsoluto(Request, jogo.TimeVisitante?.EscudoUrl),
                PlacarCasa = jogo.PlacarCasa,
                PlacarVisitante = jogo.PlacarVisitante,
                Status = jogo.Status,
                Estadio = jogo.Estadio,
                Arbitro = jogo.Arbitro,
                PenaltisCasa = jogo.PenaltisCasa,
                PenaltisVisitante = jogo.PenaltisVisitante,
                AnalisadoPorMim = analisadosIds.Contains(jogo.Id),
            };

            // ── Escalações ────────────────────────────────────────────────
            // Mesma regra das telas web: escalações do próprio usuário (com os
            // ajustes dele) têm prioridade sobre as compartilhadas (UsuarioId null);
            // entre as do usuário, prefere a fase INICIAL. Um jogador aparece uma
            // única vez por lado.
            var escalacoes = await _context.Escalacoes
                .AsNoTracking()
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && e.JogadorId.HasValue
                         && (e.UsuarioId == uid || e.UsuarioId == null))
                .ToListAsync();

            var escalacaoPorJogadorLado = escalacoes
                .GroupBy(e => (e.JogadorId!.Value, e.IsTimeCasa))
                .Select(g => g
                    .OrderBy(e => e.UsuarioId == uid ? 0 : 1)
                    .ThenBy(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null ? 0 : 1)
                    .First())
                .ToList();

            List<EscalacaoJogadorDto> MontarLado(bool casa) => escalacaoPorJogadorLado
                .Where(e => e.IsTimeCasa == casa)
                .OrderByDescending(e => e.Titular)
                .ThenBy(e => e.Jogador?.Nome)
                .Select(e => new EscalacaoJogadorDto
                {
                    JogadorId = e.JogadorId!.Value,
                    Nome = e.Jogador?.Nome ?? string.Empty,
                    Posicao = e.Posicao,
                    Titular = e.Titular,
                    FotoUrl = Url.FotoSrcAbsoluto(Request, e.Jogador?.FotoUrl)
                })
                .ToList();

            dto.EscalacaoCasa = MontarLado(casa: true);
            dto.EscalacaoVisitante = MontarLado(casa: false);

            // ── Eventos ───────────────────────────────────────────────────
            dto.Gols = await _context.Gols
                .AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => g.JogoId == id)
                .OrderBy(g => g.Minuto)
                .Select(g => new GolJogoDto
                {
                    JogadorId = g.JogadorId,
                    JogadorNome = g.Jogador != null ? g.Jogador.Nome : string.Empty,
                    Minuto = g.Minuto,
                    Contra = g.Contra
                })
                .ToListAsync();

            dto.Cartoes = await _context.Cartoes
                .AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => c.JogoId == id)
                .OrderBy(c => c.Minuto)
                .Select(c => new CartaoJogoDto
                {
                    JogadorId = c.JogadorId,
                    JogadorNome = c.Jogador != null ? c.Jogador.Nome : string.Empty,
                    Minuto = c.Minuto,
                    Tipo = c.Tipo
                })
                .ToListAsync();

            dto.EstatisticasTimes = ExtrairEstatisticasTimes(jogo);

            return Ok(dto);
        }

        // EstatisticasJson (api-football): [{ "TimeId": <idApi>, "Stats": { chave: valor } }, ...]
        // Junta os dois lados numa lista de linhas casa x visitante, preservando a
        // ordem das chaves do time da casa.
        private static List<EstatisticaTimeJogoDto> ExtrairEstatisticasTimes(Jogo jogo)
        {
            var resultado = new List<EstatisticaTimeJogoDto>();
            if (string.IsNullOrEmpty(jogo.EstatisticasJson)) return resultado;

            try
            {
                var casa = new Dictionary<string, string?>();
                var visitante = new Dictionary<string, string?>();
                var ordem = new List<string>();

                using var doc = System.Text.Json.JsonDocument.Parse(jogo.EstatisticasJson);
                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (!entry.TryGetProperty("TimeId", out var tidEl)) continue;
                    int apiId = tidEl.GetInt32();

                    Dictionary<string, string?>? destino = null;
                    if (jogo.TimeCasa?.IdApi == apiId) destino = casa;
                    else if (jogo.TimeVisitante?.IdApi == apiId) destino = visitante;
                    if (destino == null) continue;

                    if (!entry.TryGetProperty("Stats", out var stats)) continue;
                    foreach (var stat in stats.EnumerateObject())
                    {
                        var valor = stat.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                            ? null : stat.Value.GetString();
                        destino[stat.Name] = valor;
                        if (!ordem.Contains(stat.Name)) ordem.Add(stat.Name);
                    }
                }

                foreach (var chave in ordem)
                {
                    resultado.Add(new EstatisticaTimeJogoDto
                    {
                        Nome = chave,
                        ValorCasa = casa.GetValueOrDefault(chave),
                        ValorVisitante = visitante.GetValueOrDefault(chave)
                    });
                }
            }
            catch { /* JSON malformado: devolve o que tiver */ }

            return resultado;
        }

        private async Task<HashSet<int>> JogosAnalisadosDoUsuarioAsync(IEnumerable<int> jogoIds)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return new HashSet<int>();

            var ids = jogoIds.ToList();
            return await _context.JogosAnalisadosUsuario
                .Where(j => j.UsuarioId == uid && j.Analisado && ids.Contains(j.JogoId))
                .Select(j => j.JogoId)
                .ToHashSetAsync();
        }

        private JogoResumoDto ParaResumoDto(Jogo j, bool analisadoPorMim) => new()
        {
            Id = j.Id,
            Data = j.Data,
            CompeticaoId = j.CompeticaoId,
            CompeticaoNome = j.Competicao?.Nome,
            TimeCasaId = j.TimeCasaId,
            TimeCasaNome = j.TimeCasa?.Nome ?? string.Empty,
            TimeCasaEscudoUrl = Url.FotoSrcAbsoluto(Request, j.TimeCasa?.EscudoUrl),
            TimeVisitanteId = j.TimeVisitanteId,
            TimeVisitanteNome = j.TimeVisitante?.Nome ?? string.Empty,
            TimeVisitanteEscudoUrl = Url.FotoSrcAbsoluto(Request, j.TimeVisitante?.EscudoUrl),
            PlacarCasa = j.PlacarCasa,
            PlacarVisitante = j.PlacarVisitante,
            Status = j.Status,
            AnalisadoPorMim = analisadoPorMim
        };
    }
}
