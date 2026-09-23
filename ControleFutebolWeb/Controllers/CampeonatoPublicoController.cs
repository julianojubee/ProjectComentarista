using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Controllers
{
    // Link público e SOMENTE LEITURA de um campeonato próprio (/c/{token}) — é por
    // ele que os amigos acompanham a tabela sem ter conta. Isento do
    // AssinaturaFilter, como o /analise/{token}: quem abre o link não é o dono.
    [AllowAnonymous]
    public class CampeonatoPublicoController : Controller
    {
        private readonly CampeonatoService _servico;

        public CampeonatoPublicoController(CampeonatoService servico)
        {
            _servico = servico;
        }

        [HttpGet("/c/{token}")]
        public async Task<IActionResult> Index(string token, string? aba)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return NotFound();

            var campeonato = await _servico.CarregarPublicoAsync(token);
            if (campeonato == null) return NotFound();

            return View(new CampeonatoDetalhesViewModel
            {
                Campeonato = campeonato,
                Aba = aba is "partidas" or "artilharia" ? aba : "tabela",
                Painel = CampeonatoPainelAdapter.MontarPainel(campeonato),
                Participantes = campeonato.Participantes.ToDictionary(p => p.Id),
                Artilharia = EstatisticasCampeonatoHelper.PorAtleta(campeonato.Partidas.SelectMany(p => p.Eventos))
            });
        }
    }
}
