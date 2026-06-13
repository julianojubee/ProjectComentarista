using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Controllers
{
    public class ServicosController : Controller
    {
        private readonly ServicoMonitor _monitor;
        private readonly AtualizarJogadoresSemDataService _atualizarJogadores;

        public ServicosController(
            ServicoMonitor monitor,
            AtualizarJogadoresSemDataService atualizarJogadores)
        {
            _monitor = monitor;
            _atualizarJogadores = atualizarJogadores;
        }

        public IActionResult Index()
        {
            return View(_monitor.ObterTodos());
        }

        // Retorna JSON para polling da página (atualiza status sem recarregar)
        [HttpGet]
        public IActionResult Status()
        {
            var lista = _monitor.ObterTodos().Select(s => new
            {
                s.Nome,
                Estado = s.Estado.ToString(),
                IniciadoEm = s.IniciadoEm?.ToString("dd/MM HH:mm"),
                UltimoCicloEm = s.UltimoCicloEm?.ToString("dd/MM HH:mm:ss"),
                ProximoCicloEm = s.ProximoCicloEm?.ToString("dd/MM HH:mm"),
                s.UltimaAtividade,
                s.CiclosCompletos,
                s.JogadoresAtualizados,
                s.Falhas
            });
            return Json(lista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Parar(string chave)
        {
            if (chave == AtualizarJogadoresSemDataService.Chave)
                _atualizarJogadores.Parar();

            TempData["Sucesso"] = "Serviço pausado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reiniciar(string chave)
        {
            if (chave == AtualizarJogadoresSemDataService.Chave)
                _atualizarJogadores.Reiniciar();

            TempData["Sucesso"] = "Serviço reiniciado.";
            return RedirectToAction(nameof(Index));
        }
    }
}
