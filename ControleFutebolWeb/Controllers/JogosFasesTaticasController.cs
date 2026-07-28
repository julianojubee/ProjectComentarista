using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Timeline de fases táticas de /Jogos/Analisar (fotos intermediárias da
    /// escalação) e as setas de movimentação desenhadas sobre o campo.
    ///
    /// Rota "Jogos/[action]/{id?}" preserva as URLs originais, escritas
    /// literalmente em analisar.js.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosFasesTaticasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosFasesTaticasController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public class FaseTaticaSlot
        {
            public int? JogadorId { get; set; }
            public string? Posicao { get; set; }
            public double PosicaoX { get; set; }
            public double PosicaoY { get; set; }
            public bool IsTimeCasa { get; set; }
            public List<SetaSlot> Setas { get; set; } = new();
        }

        public class SetaSlot
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class SalvarFaseTaticaRequest
        {
            public int JogoId { get; set; }
            public int Minuto { get; set; }
            public string? Nome { get; set; }
            public List<FaseTaticaSlot> Titulares { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> SalvarFaseTatica([FromBody] SalvarFaseTaticaRequest req)
        {
            if (req == null || req.JogoId <= 0) return BadRequest("Dados inválidos.");
            var usuarioId = _userManager.GetUserId(User)!;

            var ordemMax = await _context.FasesTaticas
                .Where(f => f.JogoId == req.JogoId && f.UsuarioId == usuarioId)
                .Select(f => (int?)f.Ordem).MaxAsync() ?? 0;

            var chave = "FASE_" + Guid.NewGuid().ToString("N").Substring(0, 12);

            _context.FasesTaticas.Add(new FaseTatica
            {
                JogoId = req.JogoId,
                UsuarioId = usuarioId,
                Chave = chave,
                Ordem = ordemMax + 1,
                MinutoInicio = Math.Max(0, req.Minuto),
                Nome = string.IsNullOrWhiteSpace(req.Nome) ? null : req.Nome.Trim()
            });

            foreach (var s in req.Titulares.Where(s => s.JogadorId > 0))
            {
                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = req.JogoId,
                    JogadorId = s.JogadorId,
                    Posicao = s.Posicao,
                    PosicaoX = s.PosicaoX,
                    PosicaoY = s.PosicaoY,
                    IsTimeCasa = s.IsTimeCasa,
                    Titular = true,
                    FaseEscalacao = chave,
                    UsuarioId = usuarioId,
                    // Congela as setas de movimentação atuais junto com a fase
                    Setas = s.Setas
                        .Select(t => new EscalacaoSeta { X = Math.Clamp(t.X, 0, 100), Y = Math.Clamp(t.Y, 0, 100) })
                        .ToList()
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { chave });
        }

        public class ExcluirFaseTaticaRequest { public int JogoId { get; set; } public string Chave { get; set; } = ""; }

        [HttpPost]
        public async Task<IActionResult> ExcluirFaseTatica([FromBody] ExcluirFaseTaticaRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var fase = await _context.FasesTaticas
                .FirstOrDefaultAsync(f => f.JogoId == req.JogoId && f.UsuarioId == usuarioId && f.Chave == req.Chave);
            if (fase == null) return NotFound();

            var escs = await _context.Escalacoes
                .Where(e => e.JogoId == req.JogoId && e.UsuarioId == usuarioId && e.FaseEscalacao == req.Chave)
                .ToListAsync();
            _context.Escalacoes.RemoveRange(escs);
            _context.FasesTaticas.Remove(fase);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }

        // ── Setas de movimentação no campinho tático ────────────────────────

        public class AdicionarSetaRequest
        {
            public int EscalacaoId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarSeta([FromBody] AdicionarSetaRequest req)
        {
            if (req == null || req.EscalacaoId <= 0) return BadRequest("Dados inválidos.");
            var usuarioId = _userManager.GetUserId(User)!;

            var escalacao = await _context.Escalacoes
                .FirstOrDefaultAsync(e => e.Id == req.EscalacaoId
                                       && (e.UsuarioId == usuarioId || e.UsuarioId == null));
            if (escalacao == null) return NotFound();
            if (escalacao.JogadorId == null) return BadRequest("Slot sem jogador.");

            var seta = new EscalacaoSeta
            {
                EscalacaoId = escalacao.Id,
                X = Math.Clamp(req.X, 0, 100),
                Y = Math.Clamp(req.Y, 0, 100)
            };
            _context.SetasEscalacao.Add(seta);
            await _context.SaveChangesAsync();

            return Ok(new { seta.Id, seta.EscalacaoId, seta.X, seta.Y });
        }

        public class RemoverSetaRequest { public int Id { get; set; } }

        [HttpPost]
        public async Task<IActionResult> RemoverSeta([FromBody] RemoverSetaRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var seta = await _context.SetasEscalacao
                .FirstOrDefaultAsync(s => s.Id == req.Id
                                       && (s.Escalacao.UsuarioId == usuarioId || s.Escalacao.UsuarioId == null));
            if (seta == null) return NotFound();

            _context.SetasEscalacao.Remove(seta);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }
    }
}
