using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    public class AtualizarJogadoresSemDataService : BackgroundService
    {
        public const string Chave = "AtualizarJogadores";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarJogadoresSemDataService> _logger;
        private readonly ServicoMonitor _monitor;

        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromHours(6);

        private CancellationTokenSource _ctsPausa = new();
        private CancellationToken _stoppingToken;

        public AtualizarJogadoresSemDataService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarJogadoresSemDataService> logger,
            ServicoMonitor monitor)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _monitor = monitor;
            _monitor.Registrar(Chave,
                "Atualizar Jogadores",
                "Busca foto, data de nascimento e nacionalidade dos jogadores via api-football; sincroniza jogos das competições com link apifoot:.");
        }

        public void Parar()
        {
            _ctsPausa.Cancel();
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Parado;
                s.UltimaAtividade = "Parado manualmente.";
            });
        }

        public void Reiniciar()
        {
            _ctsPausa.Cancel();
            _ctsPausa = new CancellationTokenSource();
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Rodando;
                s.UltimaAtividade = "Reiniciado manualmente.";
                s.IniciadoEm = DateTime.Now;
                s.ProximoCicloEm = null;
            });
            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _ctsPausa.Token, _stoppingToken);
            _ = Task.Run(() => LoopPrincipal(linked.Token));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Aguardando;
                s.IniciadoEm = DateTime.Now;
                s.UltimaAtividade = "Aguardando 30s para iniciar...";
            });

            _logger.LogInformation("[AtualizarJogadores] Serviço iniciado.");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _ctsPausa.Token, stoppingToken);
            await LoopPrincipal(linked.Token);
        }

        private async Task LoopPrincipal(CancellationToken ct)
        {
            _monitor.Atualizar(Chave, s => s.Estado = EstadoServico.Rodando);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ExecutarCiclo(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro inesperado no ciclo.");
                    _monitor.Atualizar(Chave, s => s.UltimaAtividade = $"Erro: {ex.Message}");
                }

                if (ct.IsCancellationRequested) break;

                var proximo = DateTime.Now.Add(IntervaloEntreCiclos);
                _monitor.Atualizar(Chave, s =>
                {
                    s.Estado = EstadoServico.Aguardando;
                    s.ProximoCicloEm = proximo;
                    s.UltimaAtividade = $"Aguardando próximo ciclo em {proximo:HH:mm}.";
                });

                _logger.LogInformation("[AtualizarJogadores] Próximo ciclo em {Horas}h.",
                    IntervaloEntreCiclos.TotalHours);
                await Task.Delay(IntervaloEntreCiclos, ct);
            }

            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Parado;
                s.ProximoCicloEm = null;
            });
            _logger.LogInformation("[AtualizarJogadores] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Rodando;
                s.UltimoCicloEm = DateTime.Now;
                s.UltimaAtividade = "Verificando jogos agendados sem placar...";
            });

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var api = scope.ServiceProvider.GetRequiredService<ApiFootballService>();

            try
            {
                var (atualizados, erros) = await api.AtualizarJogosAgendadosAsync(context, ct: ct);
                _logger.LogInformation(
                    "[AtualizarJogadores] Ciclo concluído: {A} jogos atualizados, {E} erros.", atualizados, erros);

                _monitor.Atualizar(Chave, s =>
                {
                    s.CiclosCompletos++;
                    s.UltimaAtividade = $"Ciclo concluído — {atualizados} jogo(s) atualizado(s), {erros} erro(s).";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AtualizarJogadores] Erro no ciclo de atualização.");
                _monitor.Atualizar(Chave, s => s.UltimaAtividade = $"Erro: {ex.Message}");
            }
        }

    }
}
