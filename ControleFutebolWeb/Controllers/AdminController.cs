using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(FutebolContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: /Admin/NormalizarNacionalidades
        // Mescla nacionalidades duplicadas (ex.: "Brazil" + "Brasil") traduzindo tudo
        // para o nome canônico em português via CountryHelper. Não destrutiva:
        // nacionalidades fora do mapa são mantidas como estão (apenas deduplicadas
        // por igualdade case-insensitive); nenhum jogador/treinador perde o vínculo.
        [HttpPost]
        public async Task<IActionResult> NormalizarNacionalidades()
        {
            var todas = await _context.Nacionalidades.ToListAsync();

            var grupos = todas.GroupBy(
                n => CountryHelper.Traduzir(n.Nome.Trim()),
                StringComparer.OrdinalIgnoreCase);

            int renomeadas = 0, mescladas = 0, jogadoresMovidos = 0, treinadoresMovidos = 0;

            foreach (var grupo in grupos)
            {
                var lista = grupo.OrderBy(n => n.Id).ToList();
                var canonical = lista[0];
                var nomeCanonico = CountryHelper.Traduzir(canonical.Nome.Trim());

                if (canonical.Nome != nomeCanonico)
                {
                    canonical.Nome = nomeCanonico;
                    renomeadas++;
                }

                foreach (var duplicada in lista.Skip(1))
                {
                    jogadoresMovidos += await _context.Jogadores
                        .Where(j => j.NacionalidadeId == duplicada.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.NacionalidadeId, canonical.Id));

                    treinadoresMovidos += await _context.Treinadores
                        .Where(t => t.NacionalidadeId == duplicada.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.NacionalidadeId, canonical.Id));

                    _context.Nacionalidades.Remove(duplicada);
                    mescladas++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Admin] NormalizarNacionalidades: {R} renomeadas, {M} mescladas, {J} jogadores e {T} treinadores atualizados.",
                renomeadas, mescladas, jogadoresMovidos, treinadoresMovidos);

            TempData["Sucesso"] =
                $"Nacionalidades normalizadas: {renomeadas} renomeada(s), {mescladas} duplicata(s) mesclada(s), " +
                $"{jogadoresMovidos} jogador(es) e {treinadoresMovidos} treinador(es) atualizados.";
            return RedirectToAction("Index", "Servicos");
        }
    }
}
