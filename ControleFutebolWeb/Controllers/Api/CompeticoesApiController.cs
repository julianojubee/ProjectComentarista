using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/competicoes")]
    public class CompeticoesApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;

        public CompeticoesApiController(FutebolContext context)
        {
            _context = context;
        }

        // GET api/v1/competicoes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CompeticaoDto>>> Listar()
        {
            var competicoes = await _context.Competicoes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();
            return Ok(competicoes.Select(ParaDto));
        }

        // GET api/v1/competicoes/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CompeticaoDto>> Detalhe(int id)
        {
            var competicao = await _context.Competicoes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (competicao == null) return NotFound();

            return Ok(ParaDto(competicao));
        }

        private static CompeticaoDto ParaDto(Competicao c) => new()
        {
            Id = c.Id,
            Nome = c.Nome,
            Regiao = c.Regiao,
            Tipo = c.Tipo,
            EhSelecaoNacional = c.EhSelecaoNacional,
            TopTier = c.TopTier,
            LogoUrl = c.LogoUrl
        };
    }
}
