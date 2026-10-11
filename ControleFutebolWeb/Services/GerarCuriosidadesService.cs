using ControleFutebolWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Gera as curiosidades dos jogos encerrados e grava nas anotações de jogador e
    /// de clube de TODOS os usuários, sem depender de alguém abrir o pós-jogo.
    ///
    /// Não fica pendurado nos pontos que finalizam o jogo (api-football, FotMob,
    /// FIFA...): são vários, e a estatística por jogador costuma chegar depois do
    /// placar — gerar no instante do "Finalizado" perderia os recordes individuais.
    /// Em vez disso, a cada ciclo olha os jogos encerrados dos últimos dias e
    /// reprocessa os que mudaram (placar, estatísticas, gols ou usuários novos).
    /// Reprocessar é seguro: cada curiosidade entra uma vez por usuário
    /// (CuriosidadeRegistrada).
    /// </summary>
    public class GerarCuriosidadesService : BackgroundService
    {
        public const string Chave = "GerarCuriosidades";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GerarCuriosidadesService> _logger;
        private readonly ServicoMonitor _monitor;

        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromMinutes(30);

        // Quanto tempo depois do jogo ainda vale reprocessar: cobre a estatística
        // que chega horas depois (complemento do FotMob, ESPN, FIFA).
        private static readonly TimeSpan Janela = TimeSpan.FromDays(3);

        // "Impressão digital" do que já foi processado por jogo. Em memória de
        // propósito: depois de um restart o serviço reprocessa a janela uma vez, o
        // que não duplica nada e dispensa coluna nova em Jogo.
        private readonly Dictionary<int, string> _processados = new();

        private CancellationTokenSource _ctsPausa = new();
        private CancellationToken _stoppingToken;

        public GerarCuriosidadesService(
            IServiceProvider serviceProvider,
            ILogger<GerarCuriosidadesService> logger,
            ServicoMonitor monitor)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _monitor = monitor;
            _monitor.Registrar(Chave,
                "Gerar Curiosidades",
                "Gera as curiosidades dos jogos encerrados nos últimos 3 dias e grava nas anotações de jogador e clube de todos os usuários.");
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
            var linked = CancellationTokenSource.CreateLinkedTokenSource(_ctsPausa.Token, _stoppingToken);
            _ = Task.Run(() => LoopPrincipal(linked.Token));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Aguardando;
                s.IniciadoEm = DateTime.Now;
                s.UltimaAtividade = "Aguardando 60s para iniciar...";
            });

            _logger.LogInformation("[GerarCuriosidades] Serviço iniciado.");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(_ctsPausa.Token, stoppingToken);
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
                    _logger.LogError(ex, "[GerarCuriosidades] Erro inesperado no ciclo.");
                    _monitor.Atualizar(Chave, s => s.UltimaAtividade = $"Erro: {ex.Message}");
                }

                if (ct.IsCancellationRequested) break;

                var proximo = DateTime.Now.Add(IntervaloEntreCiclos);
                _monitor.Atualizar(Chave, s =>
                {
                    s.Estado = EstadoServico.Aguardando;
                    s.ProximoCicloEm = proximo;
                });
                await Task.Delay(IntervaloEntreCiclos, ct);
            }

            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Parado;
                s.ProximoCicloEm = null;
            });
            _logger.LogInformation("[GerarCuriosidades] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Rodando;
                s.UltimoCicloEm = DateTime.Now;
                s.UltimaAtividade = "Procurando jogos encerrados...";
            });

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var paineis = scope.ServiceProvider.GetRequiredService<PainelJogoService>();

            var usuarios = await context.Users.AsNoTracking().Select(u => u.Id).ToListAsync(ct);

            var agora = DateTime.UtcNow;
            var desde = agora - Janela;
            var limiteSemStatus = agora.AddHours(-3);
            var candidatos = await context.Jogos
                .AsNoTracking()
                .Where(j => j.Data != null && j.Data >= desde && j.Data <= agora
                         && j.PlacarCasa != null && j.PlacarVisitante != null
                         && (j.Status == "Finalizado" || j.Data < limiteSemStatus))
                .Select(j => new { j.Id, j.PlacarCasa, j.PlacarVisitante })
                .ToListAsync(ct);

            var ids = candidatos.Select(j => j.Id).ToList();
            var estatisticas = await context.EstatisticasJogador.AsNoTracking()
                .Where(e => ids.Contains(e.JogoId))
                .GroupBy(e => e.JogoId)
                .Select(g => new { g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Qtd, ct);
            var gols = await context.Gols.AsNoTracking()
                .Where(g => ids.Contains(g.JogoId))
                .GroupBy(g => g.JogoId)
                .Select(g => new { g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Qtd, ct);

            int processados = 0, anotacoes = 0;
            foreach (var j in candidatos)
            {
                ct.ThrowIfCancellationRequested();

                // Usuário novo também muda a impressão: ele recebe as curiosidades da janela.
                var impressao = $"{j.PlacarCasa}x{j.PlacarVisitante}|{estatisticas.GetValueOrDefault(j.Id)}|{gols.GetValueOrDefault(j.Id)}|{usuarios.Count}";
                if (_processados.TryGetValue(j.Id, out var anterior) && anterior == impressao) continue;

                try
                {
                    var (criadas, conflito) = await paineis.ProcessarCuriosidadesAsync(j.Id, usuarios);
                    anotacoes += criadas;
                    processados++;
                    // Com conflito, tenta de novo no próximo ciclo.
                    if (!conflito) _processados[j.Id] = impressao;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "[GerarCuriosidades] Erro no jogo {JogoId}.", j.Id);
                }
            }

            // Jogo que saiu da janela não volta mais: não precisa ficar na memória.
            var naJanela = ids.ToHashSet();
            foreach (var id in _processados.Keys.Where(id => !naJanela.Contains(id)).ToList())
                _processados.Remove(id);

            _logger.LogInformation("[GerarCuriosidades] Ciclo concluído: {P} jogo(s) processado(s), {A} anotação(ões) criada(s).",
                processados, anotacoes);
            _monitor.Atualizar(Chave, s =>
            {
                s.CiclosCompletos++;
                s.UltimaAtividade = $"Ciclo concluído — {processados} jogo(s) processado(s), {anotacoes} anotação(ões) criada(s).";
            });
        }
    }
}
