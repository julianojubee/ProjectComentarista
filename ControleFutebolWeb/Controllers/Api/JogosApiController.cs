using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/jogos")]
    public class JogosApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;

        public JogosApiController(FutebolContext context)
        {
            _context = context;
        }

        // GET api/v1/jogos?competicaoId=&page=&pageSize=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<JogoResumoDto>>> Listar(int? competicaoId, int page = 1, int pageSize = 50)
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

            var jogos = await query
                .OrderByDescending(j => j.Data)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(jogos.Select(ParaResumoDto));
        }

        // GET api/v1/jogos/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<JogoDetalheDto>> Detalhe(int id)
        {
            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            return Ok(new JogoDetalheDto
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
                PenaltisVisitante = jogo.PenaltisVisitante
            });
        }

        private JogoResumoDto ParaResumoDto(Jogo j) => new()
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
            Status = j.Status
        };
    }
}
