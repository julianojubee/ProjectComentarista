using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Observações categorizadas por tag (mandante/visitante/competição/jogador/marco)
    /// da tela /Jogos/Analisar, incluindo as menções a jogadores escritas com "@Nome"
    /// dentro do texto.
    ///
    /// Rota "Jogos/[action]/{id?}" preserva as URLs originais, escritas
    /// literalmente em analisar.js.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosObservacoesController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosObservacoesController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private static readonly string[] TiposObservacaoTagValidos = { "MANDANTE", "VISITANTE", "COMPETICAO", "JOGADOR", "MARCO" };

        public class ObservacaoTagRequest
        {
            public int JogoId { get; set; }
            public string Tipo { get; set; } = "";
            public int? JogadorId { get; set; }
            public string Texto { get; set; } = "";
        }

        // Reconhece menções "@Nome" no texto livre da observação, comparando com os
        // jogadores escalados no jogo (o mesmo universo oferecido no dropdown de @).
        // Compara os nomes mais longos primeiro para não confundir "@João" com "@João Silva".
        private static List<int> ExtrairMencoes(string texto, IEnumerable<Jogador> disponiveis)
        {
            var encontrados = new List<int>();
            if (string.IsNullOrWhiteSpace(texto)) return encontrados;

            foreach (var jogador in disponiveis
                         .Where(j => !string.IsNullOrWhiteSpace(j.NomeExibicao))
                         .OrderByDescending(j => j.NomeExibicao.Length))
            {
                var alvo = "@" + jogador.NomeExibicao;
                if (texto.Contains(alvo, StringComparison.OrdinalIgnoreCase) && !encontrados.Contains(jogador.Id))
                    encontrados.Add(jogador.Id);
            }

            return encontrados;
        }

        private async Task SincronizarMencoesAsync(int observacaoId, int jogoId, string texto)
        {
            var mencoesAntigas = _context.ObservacoesJogoTagMencoes.Where(m => m.ObservacaoJogoTagId == observacaoId);
            _context.ObservacoesJogoTagMencoes.RemoveRange(mencoesAntigas);

            var jogadoresDoJogo = await _context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.JogadorId != null)
                .Select(e => e.Jogador!)
                .Distinct()
                .ToListAsync();

            foreach (var jogadorId in ExtrairMencoes(texto, jogadoresDoJogo))
            {
                _context.ObservacoesJogoTagMencoes.Add(new ObservacaoJogoTagMencao
                {
                    ObservacaoJogoTagId = observacaoId,
                    JogadorId = jogadorId
                });
            }

            await _context.SaveChangesAsync();
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarObservacaoTag([FromBody] ObservacaoTagRequest req)
        {
            if (req == null || req.JogoId <= 0 || !TiposObservacaoTagValidos.Contains(req.Tipo) || string.IsNullOrWhiteSpace(req.Texto))
                return BadRequest("Dados inválidos.");
            if (req.Tipo == "JOGADOR" && (req.JogadorId is null || req.JogadorId <= 0))
                return BadRequest("Selecione o jogador.");

            var usuarioId = _userManager.GetUserId(User)!;

            var ordemMax = await _context.ObservacoesJogoTag
                .Where(o => o.JogoId == req.JogoId && o.UsuarioId == usuarioId)
                .Select(o => (int?)o.Ordem).MaxAsync() ?? 0;

            var obs = new ObservacaoJogoTag
            {
                JogoId = req.JogoId,
                UsuarioId = usuarioId,
                Tipo = req.Tipo,
                JogadorId = req.Tipo == "JOGADOR" ? req.JogadorId : null,
                Texto = req.Texto.Trim(),
                Ordem = ordemMax + 1
            };
            _context.ObservacoesJogoTag.Add(obs);
            await _context.SaveChangesAsync();

            await SincronizarMencoesAsync(obs.Id, obs.JogoId, obs.Texto);

            string? jogadorNome = null;
            if (obs.JogadorId.HasValue)
            {
                var jogadorObs = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == obs.JogadorId);
                jogadorNome = jogadorObs?.NomeExibicao;
            }

            return Ok(new { obs.Id, obs.Tipo, obs.JogadorId, jogadorNome, obs.Texto });
        }

        public class EditarObservacaoTagRequest
        {
            public int Id { get; set; }
            public string Texto { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> EditarObservacaoTag([FromBody] EditarObservacaoTagRequest req)
        {
            if (req == null || req.Id <= 0 || string.IsNullOrWhiteSpace(req.Texto))
                return BadRequest("Dados inválidos.");

            var usuarioId = _userManager.GetUserId(User)!;
            var obs = await _context.ObservacoesJogoTag
                .FirstOrDefaultAsync(o => o.Id == req.Id && o.UsuarioId == usuarioId);
            if (obs == null) return NotFound();

            obs.Texto = req.Texto.Trim();
            await _context.SaveChangesAsync();

            await SincronizarMencoesAsync(obs.Id, obs.JogoId, obs.Texto);

            return Ok(new { sucesso = true });
        }

        public class RemoverObservacaoTagRequest { public int Id { get; set; } }

        [HttpPost]
        public async Task<IActionResult> RemoverObservacaoTag([FromBody] RemoverObservacaoTagRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            // Delete atômico: com carregar-e-remover, um clique duplo fazia a 2ª
            // requisição deletar uma linha já apagada e o EF estourava
            // DbUpdateConcurrencyException (500 em produção, 11/07/2026).
            var removidos = await _context.ObservacoesJogoTag
                .Where(o => o.Id == req.Id && o.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            if (removidos == 0) return NotFound();
            return Ok(new { sucesso = true });
        }
    }
}
