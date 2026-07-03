using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/times")]
    public class TimesApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;

        public TimesApiController(FutebolContext context)
        {
            _context = context;
        }

        // GET api/v1/times?nome=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TimeResumoDto>>> Listar(string? nome)
        {
            var query = _context.Times.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(nome))
                query = query.Where(t => t.Nome.ToLower().Contains(nome.ToLower()));

            var times = await query.OrderBy(t => t.Nome).ToListAsync();

            return Ok(times.Select(ParaResumoDto));
        }

        // GET api/v1/times/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TimeDetalheDto>> Detalhe(int id)
        {
            var time = await _context.Times.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (time == null) return NotFound();

            return Ok(new TimeDetalheDto
            {
                Id = time.Id,
                Nome = time.Nome,
                Cidade = time.Cidade,
                EscudoUrl = Url.FotoSrcAbsoluto(Request, time.EscudoUrl),
                EhSelecao = time.EhSelecao,
                BackgroundUrl = Url.FotoSrcAbsoluto(Request, time.BackgroundUrl),
                CorPrincipal = time.CorPrincipal,
                CorSecundaria = time.CorSecundaria,
                CamisaUrl = Url.FotoSrcAbsoluto(Request, time.CamisaUrl),
                CamisaVisitanteUrl = Url.FotoSrcAbsoluto(Request, time.CamisaVisitanteUrl),
                LinkTransfermarket = time.LinkTransfermarket
            });
        }

        private TimeResumoDto ParaResumoDto(Time t) => new()
        {
            Id = t.Id,
            Nome = t.Nome,
            Cidade = t.Cidade,
            EscudoUrl = Url.FotoSrcAbsoluto(Request, t.EscudoUrl),
            EhSelecao = t.EhSelecao
        };
    }
}
