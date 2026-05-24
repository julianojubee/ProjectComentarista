//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Configuration;
//using ControleFutebolWeb.Data;

//namespace ControleFutebolWeb.Services
//{
//    /// <summary>
//    /// HostedService que roda periodicamente a atualização da Copa Sul-Americana via oGol.
//    /// Comportamento: espera 30s após start, executa e repete a cada 6 horas.
//    /// </summary>
//    public class AtualizarCopaSulAmericanaService : BackgroundService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<AtualizarCopaSulAmericanaService> _logger;
//        private readonly IConfiguration _config;

//        // defaults
//        private static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(6);
//        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

//        public AtualizarCopaSulAmericanaService(
//            IServiceProvider serviceProvider,
//            ILogger<AtualizarCopaSulAmericanaService> logger,
//            IConfiguration config)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//            _config = config;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("[AtualizarCopaSulAmericana] Serviço iniciado. Aguardando {s}s antes do primeiro ciclo.", InitialDelay.TotalSeconds);
//            await Task.Delay(InitialDelay, stoppingToken);

//            var intervalHours = _config.GetValue<double?>("SulAmericana:IntervaloHoras") ?? DefaultInterval.TotalHours;
//            var interval = TimeSpan.FromHours(intervalHours);

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    await ExecutarUmCiclo(stoppingToken);
//                }
//                catch (OperationCanceledException) { break; }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "[AtualizarCopaSulAmericana] Erro inesperado no ciclo.");
//                }

//                _logger.LogInformation("[AtualizarCopaSulAmericana] Próximo ciclo em {Horas}h.", interval.TotalHours);
//                await Task.Delay(interval, stoppingToken);
//            }

//            _logger.LogInformation("[AtualizarCopaSulAmericana] Serviço encerrado.");
//        }

//        private async Task ExecutarUmCiclo(CancellationToken ct)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
//            var svc = scope.ServiceProvider.GetRequiredService<TransfermarktSulAmericanaService>();

//            // Lê configuração
//            var competicaoId = _config.GetValue<int?>("SulAmericana:CompeticaoId");
//            var ano = _config.GetValue<int?>("SulAmericana:Ano") ?? DateTime.UtcNow.Year;
//            var importarEscalacoes = _config.GetValue<bool?>("SulAmericana:ImportarEscalacoes") ?? true;

//            if (!competicaoId.HasValue || competicaoId.Value == 0)
//            {
//                _logger.LogWarning("[AtualizarCopaSulAmericana] SulAmericana:CompeticaoId não configurado. Pulando ciclo.");
//                return;
//            }

//            _logger.LogInformation("[AtualizarCopaSulAmericana] Iniciando sincronização: CompeticaoId={Id} Ano={Ano} ImportarEscalacoes={Importar}", competicaoId, ano, importarEscalacoes);

//            var resultado = await svc.SincronizarAsync(context, competicaoId.Value, ano, importarEscalacoes, ct);

//            _logger.LogInformation("[AtualizarCopaSulAmericana] Ciclo concluído: {Resultado}", resultado?.ToString() ?? "null");
//            // garante persistência das alterações realizadas pelo serviço
//            await context.SaveChangesAsync(ct);
//        }
//    }
//}