using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Eventos de uma partida em /Jogos/Analisar: gols, cartões, placar e a marcação
    /// de "analisado". Tudo por AJAX a partir de analisar.js.
    ///
    /// Rota "Jogos/[action]/{id?}" preserva as URLs originais (/Jogos/RegistrarGol,
    /// /Jogos/RemoverGol/5 ...), escritas literalmente no JS.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosEventosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosEventosController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Retorna gols e cartões de um jogo para popular a timeline.
        /// GET /Jogos/BuscarEventos?jogoId=X
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarEventos(int jogoId)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId);

            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var gols = await _context.Gols
                .Include(g => g.Jogador)
                .Where(g => g.JogoId == jogoId)
                .OrderBy(g => g.Minuto)
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .Include(a => a.Jogador)
                .Where(a => a.JogoId == jogoId)
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .Include(c => c.Jogador)
                .Where(c => c.JogoId == jogoId)
                .OrderBy(c => c.Minuto)
                .ToListAsync();

            var substituicoes = await _context.Substituicoes
                .Include(s => s.JogadorEntrou)
                .Include(s => s.JogadorSaiu)
                .Where(s => s.JogoId == jogoId)
                .OrderBy(s => s.Minuto)
                .ToListAsync();

            var penaltisPerdidos = await _context.PenaltisPerdidos
                .Include(p => p.Jogador)
                .Where(p => p.JogoId == jogoId)
                .OrderBy(p => p.Minuto)
                .ToListAsync();

            var penaltisDisputa = await _context.PenaltisDisputa
                .Include(p => p.Jogador)
                .Where(p => p.JogoId == jogoId)
                .OrderBy(p => p.Ordem)
                .ToListAsync();

            var resultado = new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante,
                penaltisCasa = jogo.PenaltisCasa,
                penaltisVis = jogo.PenaltisVisitante,

                gols = gols.Select(g => new
                {
                    id = g.Id,
                    minuto = g.Minuto,
                    nomeJogador = g.Jogador?.Nome,
                    nomeAssistencia = assistencias
                        .Where(a => a.Minuto == g.Minuto && !g.Contra &&
                                    a.Jogador != null &&
                                    (a.Jogador.TimeId == g.Jogador!.TimeId ||
                                     a.Jogador.SelecaoId == g.Jogador!.TimeId ||
                                     a.Jogador.TimeId == g.Jogador!.SelecaoId ||
                                     (a.Jogador.SelecaoId != null && a.Jogador.SelecaoId == g.Jogador!.SelecaoId)))
                        .Select(a => a.Jogador!.Nome)
                        .FirstOrDefault(),
                    contra = g.Contra,
                    timeCasaId = g.Contra
                        ? ((g.Jogador?.TimeId == jogo.TimeCasaId || g.Jogador?.SelecaoId == jogo.TimeCasaId)
                            ? null : (int?)jogo.TimeCasaId)
                        : ((g.Jogador?.TimeId == jogo.TimeCasaId || g.Jogador?.SelecaoId == jogo.TimeCasaId)
                            ? (int?)jogo.TimeCasaId : null)
                }),

                cartoes = cartoes.Select(c => new
                {
                    id = c.Id,
                    minuto = c.Minuto,
                    tipo = c.Tipo,
                    nomeJogador = c.Jogador?.Nome,
                    timeCasaId = c.Jogador?.TimeId == jogo.TimeCasaId || c.Jogador?.SelecaoId == jogo.TimeCasaId
                        ? (int?)jogo.TimeCasaId : null
                }),

                substituicoes = substituicoes.Select(s => new
                {
                    id = s.Id,
                    minuto = s.Minuto,
                    nomeEntrou = s.JogadorEntrou?.Nome,
                    nomeSaiu = s.JogadorSaiu?.Nome,
                    timeCasaId = s.IsTimeCasa ? (int?)jogo.TimeCasaId : null
                }),

                penaltisPerdidos = penaltisPerdidos.Select(p => new
                {
                    id = p.Id,
                    minuto = p.Minuto,
                    nomeJogador = p.Jogador?.Nome,
                    timeCasaId = p.IsTimeCasa ? (int?)jogo.TimeCasaId : null
                }),

                penaltisDisputa = penaltisDisputa.Select(p => new
                {
                    id = p.Id,
                    ordem = p.Ordem,
                    nomeJogador = p.Jogador?.Nome,
                    convertido = p.Convertido,
                    isCasa = p.IsTimeCasa
                })
            };

            return Ok(resultado);
        }

        public class RegistrarGolRequest
        {
            public int JogoId { get; set; }
            public int JogadorId { get; set; }
            public int? AssistenciaJogadorId { get; set; }
            public int Minuto { get; set; }
            public int Acrescimo { get; set; }
            public bool Contra { get; set; }
            public bool IsTimeCasa { get; set; }
        }

        /// <summary>
        /// Registra um gol manualmente.
        /// POST /Jogos/RegistrarGol
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarGol([FromBody] RegistrarGolRequest req)
        {
            if (req.JogadorId <= 0) return BadRequest(new { erro = "Jogador inválido." });

            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var gol = new Gol
            {
                JogoId = req.JogoId,
                JogadorId = req.JogadorId,
                Minuto = req.Minuto,
                Contra = req.Contra
            };
            _context.Gols.Add(gol);

            if (req.AssistenciaJogadorId.HasValue && req.AssistenciaJogadorId > 0 && !req.Contra)
            {
                _context.Assistencias.Add(new Assistencia
                {
                    JogoId = req.JogoId,
                    JogadorId = req.AssistenciaJogadorId.Value,
                    Minuto = req.Minuto
                });
            }

            // Recalcula placar contando os gols no banco + o novo
            if (!req.Contra)
            {
                if (req.IsTimeCasa)
                    jogo.PlacarCasa = (jogo.PlacarCasa ?? 0) + 1;
                else
                    jogo.PlacarVisitante = (jogo.PlacarVisitante ?? 0) + 1;
            }
            else // gol contra: ponto vai para o adversário
            {
                if (req.IsTimeCasa)
                    jogo.PlacarVisitante = (jogo.PlacarVisitante ?? 0) + 1;
                else
                    jogo.PlacarCasa = (jogo.PlacarCasa ?? 0) + 1;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = gol.Id,
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        /// <summary>
        /// Remove um gol e recalcula o placar.
        /// DELETE /Jogos/RemoverGol?id=X
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoverGol(int id)
        {
            var gol = await _context.Gols
                .Include(g => g.Jogador)
                .Include(g => g.Jogo)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gol == null) return NotFound(new { erro = "Gol não encontrado." });

            var jogo = gol.Jogo;

            if (!gol.Contra)
            {
                bool isCasa = gol.Jogador?.TimeId == jogo.TimeCasaId;
                if (isCasa)
                    jogo.PlacarCasa = Math.Max(0, (jogo.PlacarCasa ?? 1) - 1);
                else
                    jogo.PlacarVisitante = Math.Max(0, (jogo.PlacarVisitante ?? 1) - 1);
            }
            else
            {
                bool isCasa = gol.Jogador?.TimeId == jogo.TimeCasaId;
                if (isCasa)
                    jogo.PlacarVisitante = Math.Max(0, (jogo.PlacarVisitante ?? 1) - 1);
                else
                    jogo.PlacarCasa = Math.Max(0, (jogo.PlacarCasa ?? 1) - 1);
            }

            _context.Gols.Remove(gol);

            // Remove assistência vinculada ao mesmo minuto (se existir)
            if (!gol.Contra && gol.Jogador != null)
            {
                var assist = await _context.Assistencias
                    .Include(a => a.Jogador)
                    .FirstOrDefaultAsync(a => a.JogoId == gol.JogoId && a.Minuto == gol.Minuto
                                           && a.Jogador != null && a.Jogador.TimeId == gol.Jogador.TimeId);
                if (assist != null)
                    _context.Assistencias.Remove(assist);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        public class RegistrarCartaoRequest
        {
            public int JogoId { get; set; }
            public int JogadorId { get; set; }
            public int Minuto { get; set; }
            public int Acrescimo { get; set; }
            public string Tipo { get; set; } = "Amarelo"; // "Amarelo" | "Vermelho"
            public bool IsTimeCasa { get; set; }
        }
        /// <summary>
        /// Registra um cartão manualmente.
        /// POST /Jogos/RegistrarCartao
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarCartao([FromBody] RegistrarCartaoRequest req)
        {
            if (req.JogadorId <= 0) return BadRequest(new { erro = "Jogador inválido." });

            var cartao = new Cartao
            {
                JogoId = req.JogoId,
                JogadorId = req.JogadorId,
                Minuto = req.Minuto,
                Tipo = req.Tipo
            };
            _context.Cartoes.Add(cartao);
            await _context.SaveChangesAsync();

            return Ok(new { id = cartao.Id });
        }

        /// <summary>
        /// Remove um cartão.
        /// DELETE /Jogos/RemoverCartao?id=X
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoverCartao(int id)
        {
            var cartao = await _context.Cartoes.FindAsync(id);
            if (cartao == null) return NotFound(new { erro = "Cartão não encontrado." });

            _context.Cartoes.Remove(cartao);
            await _context.SaveChangesAsync();

            return Ok(new { removido = true });
        }

        public class AtualizarPlacarRequest
        {
            public int JogoId { get; set; }
            public int PlacarCasa { get; set; }
            public int PlacarVis { get; set; }
        }
        /// <summary>
        /// Atualiza o placar manualmente (clique nos números do placar).
        /// POST /Jogos/AtualizarPlacar
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AtualizarPlacar([FromBody] AtualizarPlacarRequest req)
        {
            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            jogo.PlacarCasa = req.PlacarCasa;
            jogo.PlacarVisitante = req.PlacarVis;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        public class MarcarAnalisadoRequest
        {
            public int JogoId { get; set; }
            public int Analisado { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> MarcarAnalisado([FromBody] MarcarAnalisadoRequest req)
        {
            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var usuarioId = _userManager.GetUserId(User)!;
            var registro = await _context.JogosAnalisadosUsuario
                .FirstOrDefaultAsync(j => j.JogoId == req.JogoId && j.UsuarioId == usuarioId);

            // NUNCA remove a linha: ela também guarda as Observacoes da escalação
            // final (ver SalvarEscalacao). Remover apagava as observações do usuário
            // ao desmarcar "analisado" — aqui só alternamos o flag Analisado.
            if (registro == null)
                _context.JogosAnalisadosUsuario.Add(new JogoAnalisadoUsuario
                {
                    JogoId = req.JogoId,
                    UsuarioId = usuarioId,
                    Analisado = req.Analisado == 1
                });
            else
                registro.Analisado = req.Analisado == 1;

            await _context.SaveChangesAsync();
            return Ok(new { analisado = req.Analisado });
        }
    }
}
