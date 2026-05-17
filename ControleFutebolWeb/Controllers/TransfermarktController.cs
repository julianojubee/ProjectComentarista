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

    }
}