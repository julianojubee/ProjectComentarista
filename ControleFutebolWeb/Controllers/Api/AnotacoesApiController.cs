using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    // Anotações de time são por usuário (mesma regra da tela web /AnotacoesTime).
    [Route("api/v1/anotacoes")]
    public class AnotacoesApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnotacoesApiController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET api/v1/anotacoes?timeId=1&q=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnotacaoTimeDto>>> Listar(int timeId, string? q)
        {
            var uid = _userManager.GetUserId(User);

            var query = _context.AnotacoesTime
                .AsNoTracking()
                .Include(a => a.Time)
                .Where(a => a.TimeId == timeId && a.UsuarioId == uid)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Titulo.ToLower().Contains(q.ToLower()) ||
                    a.Conteudo.ToLower().Contains(q.ToLower()) ||
                    (a.Categoria != null && a.Categoria.ToLower().Contains(q.ToLower())));

            var anotacoes = await query.OrderByDescending(a => a.DtInc).ToListAsync();
            return Ok(anotacoes.Select(ParaDto));
        }

        // GET api/v1/anotacoes/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<AnotacaoTimeDto>> Detalhe(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTime
                .AsNoTracking()
                .Include(a => a.Time)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);

            if (anotacao == null) return NotFound();
            return Ok(ParaDto(anotacao));
        }

        // POST api/v1/anotacoes
        [HttpPost]
        public async Task<ActionResult<AnotacaoTimeDto>> Criar([FromBody] AnotacaoTimeInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Titulo) || string.IsNullOrWhiteSpace(input.Conteudo))
                return BadRequest("Título e conteúdo são obrigatórios.");

            var time = await _context.Times.FindAsync(input.TimeId);
            if (time == null) return NotFound("Time não encontrado.");

            var anotacao = new AnotacaoTime
            {
                TimeId = input.TimeId,
                Titulo = input.Titulo,
                Conteudo = input.Conteudo,
                Categoria = input.Categoria,
                DtInc = DateTime.UtcNow,
                UsuarioId = _userManager.GetUserId(User)
            };

            _context.AnotacoesTime.Add(anotacao);
            await _context.SaveChangesAsync();

            anotacao.Time = time;
            return CreatedAtAction(nameof(Detalhe), new { id = anotacao.Id }, ParaDto(anotacao));
        }

        // PUT api/v1/anotacoes/5
        [HttpPut("{id:int}")]
        public async Task<ActionResult<AnotacaoTimeDto>> Atualizar(int id, [FromBody] AnotacaoTimeInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Titulo) || string.IsNullOrWhiteSpace(input.Conteudo))
                return BadRequest("Título e conteúdo são obrigatórios.");

            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTime
                .Include(a => a.Time)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            anotacao.Titulo = input.Titulo;
            anotacao.Conteudo = input.Conteudo;
            anotacao.Categoria = input.Categoria;
            anotacao.DtAlt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ParaDto(anotacao));
        }

        // DELETE api/v1/anotacoes/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Excluir(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTime.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            _context.AnotacoesTime.Remove(anotacao);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static AnotacaoTimeDto ParaDto(AnotacaoTime a) => new()
        {
            Id = a.Id,
            TimeId = a.TimeId,
            TimeNome = a.Time?.Nome,
            Titulo = a.Titulo,
            Conteudo = a.Conteudo,
            Categoria = a.Categoria,
            DtInc = a.DtInc,
            DtAlt = a.DtAlt
        };
    }
}
