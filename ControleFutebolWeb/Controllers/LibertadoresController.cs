using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class LibertadoresController : Controller
    {
        private readonly FutebolContext _context;

        public LibertadoresController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? temporada = null)
        {
            // Mesma montagem da Sul-Americana (CopaContinentalPainelBuilder)
            var (vm, estatisticas) = CopaContinentalPainelBuilder.Montar(_context, 2, temporada);
            ViewBag.EstatisticasTimes = estatisticas;
            return View(vm);
        }
    }
}
