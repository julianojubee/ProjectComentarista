using System;
using System.Threading.Tasks;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ControleFutebolWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransfermarktController : ControllerBase
    {
        private readonly TransfermarktService _transfermarktService;
        private readonly ILogger<TransfermarktController> _logger;

        public TransfermarktController(TransfermarktService transfermarktService, ILogger<TransfermarktController> logger)
        {
            _transfermarktService = transfermarktService;
            _logger = logger;
        }

        /// <summary>
        /// Dispara a sincronização da Copa Sul-Americana.
        /// - HTTP POST api/transfermarkt/update-copa-sulamericana?temporada=2025
        /// - Query: temporada (int) — padrão 2025
        /// - Query: runSync (bool) — se true aguarda conclusão (bloqueante); padrão false (fire-and-forget, retorna 202 Accepted).
        /// </summary>
        //[HttpPost("update-copa-sulamericana")]
        //public async Task<IActionResult> UpdateCopaSulAmericana([FromQuery] int temporada = 2025, [FromQuery] bool runSync = false)
        //{
        //    if (runSync)
        //    {
        //        try
        //        {
        //            await _transfermarktService.AtualizarCopaSulAmericana(temporada);
        //            return Ok(new { message = "Completed", temporada });
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Erro ao executar AtualizarCopaSulAmericana sincronamente.");
        //            return StatusCode(500, new { error = "Execution failed", detail = ex.Message });
        //        }
        //    }
        //    else
        //    {
        //        // Fire-and-forget: log exceptions inside task
        //        _ = Task.Run(async () =>
        //        {
        //            try
        //            {
        //                await _transfermarktService.AtualizarCopaSulAmericana(temporada);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Erro ao executar AtualizarCopaSulAmericana (background).");
        //            }
        //        });

        //        return Accepted(new { message = "Started", temporada });
        //    }
        //}
    }
}