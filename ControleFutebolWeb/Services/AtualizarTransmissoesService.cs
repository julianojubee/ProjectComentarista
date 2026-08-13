namespace ControleFutebolWeb.Services
{
    // Roda em ciclo curto (cobre a virada do dia automaticamente, já que cada ciclo
    // sempre consulta "hoje") pois o futnatv.net costuma anunciar/confirmar canais
    // ao longo do próprio dia, não só à meia-noite. A lógica de busca/matching/log
    // fica em TransmissaoJogoService, reaproveitada também em cadastros pontuais
    // de jogo (ver JogosController.Create).
    public class AtualizarTransmissoesService : BackgroundService
    {
        public const string Chave = "AtualizarTransmissoes";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarTransmissoesService> _logger;
        private readonly ServicoMonitor _monitor;

        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromHours(3);

        public AtualizarTransmissoesService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarTransmissoesService> logger,
            ServicoMonitor monitor)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _monitor = monitor;
            _monitor.Registrar(Chave,
                "Atualizar Transmissões",
                "Busca no futnatv.net a plataforma de transmissão dos jogos do dia e grava em Jogo.TransmissaoTv.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Aguardando;
                s.IniciadoEm = DateTime.Now;
                s.UltimaAtividade = "Aguardando 20s para iniciar...";
            });

            _logger.LogInformation("[AtualizarTransmissoes] Serviço iniciado.");
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutarCiclo(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarTransmissoes] Erro inesperado no ciclo.");
                    _monitor.Atualizar(Chave, s => s.UltimaAtividade = $"Erro: {ex.Message}");
                }

                if (stoppingToken.IsCancellationRequested) break;

                var proximo = DateTime.Now.Add(IntervaloEntreCiclos);
                _monitor.Atualizar(Chave, s =>
                {
                    s.Estado = EstadoServico.Aguardando;
                    s.ProximoCicloEm = proximo;
                    s.UltimaAtividade = $"Aguardando próximo ciclo em {proximo:HH:mm}.";
                });

                await Task.Delay(IntervaloEntreCiclos, stoppingToken);
            }

            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Parado;
                s.ProximoCicloEm = null;
            });
            _logger.LogInformation("[AtualizarTransmissoes] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Rodando;
                s.UltimoCicloEm = DateTime.Now;
                s.UltimaAtividade = "Buscando transmissões dos jogos de hoje e dos próximos dias...";
            });

            using var scope = _serviceProvider.CreateScope();
            var transmissao = scope.ServiceProvider.GetRequiredService<TransmissaoJogoService>();

            var atualizados = 0;
            foreach (var diaBrasil in TransmissaoJogoService.DiasDaJanela())
                atualizados += await transmissao.AtualizarTransmissoesDoDiaAsync(diaBrasil, ct);

            _logger.LogInformation(
                "[AtualizarTransmissoes] Ciclo concluído: {A} jogo(s) atualizado(s) na janela.", atualizados);

            _monitor.Atualizar(Chave, s =>
            {
                s.CiclosCompletos++;
                s.UltimaAtividade = $"Ciclo concluído — {atualizados} transmissão(ões) atualizada(s).";
            });
        }
    }
}
