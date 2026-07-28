using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Mapa de calor de posicionamento em /Jogos/Analisar: pontos das escalações
    /// salvas (inicial, final e cada fase tática) mais o destino das setas.
    ///
    /// Rota "Jogos/[action]/{id?}" preserva as URLs originais, escritas
    /// literalmente em analisar.js.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosMapaCalorController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosMapaCalorController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ── Mapa de Calor ───────────────────────────────────────────────────
        // Pontos (PosicaoX/Y, em % do campo) de todas as escalações salvas pelo
        // usuário atual nesse jogo — INICIAL, FINAL e cada fase tática intermediária
        // contam como uma "foto" a mais, dando o histórico de posicionamento usado
        // pelo mapa de calor. Filtra por jogador OU por lado (casa/visitante).
        // Some-se o destino de cada seta de movimentação (EscalacaoSeta.X/Y) —
        // a origem da seta já é a própria Escalacao (não duplica), só o destino
        // é um ponto novo, dando mais densidade sem depender só das fases salvas.
        // Peso menor (mais fraco/amarelado no mapa): a seta indica um lugar por
        // onde o jogador passou/se movimentou, não a posição principal dele.
        public async Task<IActionResult> MapaCalor(int id, int? jogadorId, int? timeId)
        {
            if (jogadorId == null && timeId == null) return BadRequest();

            var usuarioId = _userManager.GetUserId(User);

            // Titular == true: reservas que ficaram no banco têm PosicaoX/Y fixo em
            // 0/0 (canto do campo, não é uma posição de jogo real) — incluí-las
            // gerava um ponto de calor falso no canto superior esquerdo.
            var query = _context.Escalacoes.AsNoTracking()
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.JogadorId != null && e.Titular);

            if (jogadorId.HasValue)
            {
                query = query.Where(e => e.JogadorId == jogadorId.Value);
            }
            else
            {
                var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
                if (jogo == null) return NotFound();
                var isCasa = timeId!.Value == jogo.TimeCasaId;
                query = query.Where(e => e.IsTimeCasa == isCasa);
            }

            var pontos = await query
                .Select(e => new { x = e.PosicaoX, y = e.PosicaoY, peso = 1.0 })
                .ToListAsync();

            var destinosSetas = await query
                .SelectMany(e => e.Setas)
                .Select(s => new { x = s.X, y = s.Y, peso = 0.45 })
                .ToListAsync();

            pontos.AddRange(destinosSetas);

            return Json(pontos);
        }

        // Jogadores elegíveis pro seletor do mapa de calor: só quem realmente entrou
        // em campo (titulares da escalação INICIAL do usuário + quem entrou como
        // substituição, segundo os eventos importados) — reservas que ficaram no
        // banco o jogo inteiro não aparecem.
        public async Task<IActionResult> MapaCalorJogadores(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            var titulares = await _context.Escalacoes.AsNoTracking()
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && e.Titular && e.JogadorId != null)
                .ToListAsync();

            var subsEntraram = await _context.Substituicoes.AsNoTracking()
                .Include(s => s.JogadorEntrou)
                .Where(s => s.JogoId == id && s.JogadorEntrouId != null && s.JogadorEntrouId != s.JogadorSaiuId)
                .ToListAsync();

            List<object> MontarLado(bool isTimeCasa) =>
                titulares.Where(e => e.IsTimeCasa == isTimeCasa)
                    .Select(e => new { jogadorId = e.Jogador!.Id, nome = e.Jogador!.Nome })
                    .Concat(subsEntraram.Where(s => s.IsTimeCasa == isTimeCasa && s.JogadorEntrou != null)
                        .Select(s => new { jogadorId = s.JogadorEntrou!.Id, nome = s.JogadorEntrou!.Nome }))
                    .GroupBy(x => x.jogadorId)
                    .Select(g => g.First())
                    .OrderBy(x => x.nome)
                    .Cast<object>()
                    .ToList();

            return Json(new { casa = MontarLado(true), visitante = MontarLado(false) });
        }
    }
}
