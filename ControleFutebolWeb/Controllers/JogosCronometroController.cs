using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Cronômetro da partida em /Jogos/Analisar — estado por usuário, consultado e
    /// comandado por AJAX.
    ///
    /// A rota é declarada explicitamente como "Jogos/[action]" para que as URLs
    /// continuem sendo /Jogos/CronometroEstado e /Jogos/CronometroAcao mesmo com as
    /// actions fora do JogosController: são chamadas por URL literal no
    /// wwwroot/js/analisar.js, que não sabe (nem precisa saber) em qual controller
    /// elas moram.
    /// </summary>
    [Route("Jogos/[action]")]
    public class JogosCronometroController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosCronometroController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private static int CronometroSegundos(CronometroPartida c)
        {
            var s = c.SegundosAcumulados;
            if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                s += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
            return Math.Max(0, s);
        }

        [HttpGet]
        public async Task<IActionResult> CronometroEstado(int jogoId)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var c = await _context.CronometrosPartida
                .FirstOrDefaultAsync(x => x.JogoId == jogoId && x.UsuarioId == usuarioId);
            if (c == null) return Ok(new { estado = "PARADO", segundos = 0 });
            return Ok(new { estado = c.Estado, segundos = CronometroSegundos(c) });
        }

        public class CronometroAcaoRequest { public int JogoId { get; set; } public string Acao { get; set; } = ""; }

        [HttpPost]
        public async Task<IActionResult> CronometroAcao([FromBody] CronometroAcaoRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var c = await _context.CronometrosPartida
                .FirstOrDefaultAsync(x => x.JogoId == req.JogoId && x.UsuarioId == usuarioId);
            if (c == null)
            {
                c = new CronometroPartida { JogoId = req.JogoId, UsuarioId = usuarioId, Estado = "PARADO" };
                _context.CronometrosPartida.Add(c);
            }

            switch ((req.Acao ?? "").ToLowerInvariant())
            {
                case "iniciar":
                    if (c.Estado != "RODANDO") { c.InicioUtc = DateTime.UtcNow; c.Estado = "RODANDO"; }
                    break;
                case "parar":
                    if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                        c.SegundosAcumulados += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
                    c.InicioUtc = null;
                    c.Estado = "PARADO";
                    break;
                case "finalizar":
                    if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                        c.SegundosAcumulados += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
                    c.InicioUtc = null;
                    c.Estado = "FINALIZADO";
                    break;
                case "zerar":
                    c.SegundosAcumulados = 0; c.InicioUtc = null; c.Estado = "PARADO";
                    break;
            }

            await _context.SaveChangesAsync();
            return Ok(new { estado = c.Estado, segundos = CronometroSegundos(c) });
        }
    }
}
