using ControleFutebolWeb.Controllers;
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
        private static readonly TimeSpan IntervaloEntreJogadores = TimeSpan.FromSeconds(3);

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
                s.UltimaAtividade = "Sincronizando competições via api-football...";
            });

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var api = scope.ServiceProvider.GetRequiredService<ApiFootballService>();

            await SincronizarCompeticoesApiFootball(context, api, ct);

            // Atualiza jogadores com IdApi definido e ainda não atualizados
            var jogadores = await context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .Where(j => j.IdApi != null && j.IdApi > 0 && !j.Atualizado)
                .OrderBy(j => j.Id)
                .ToListAsync(ct);

            if (!jogadores.Any())
            {
                _logger.LogInformation("[AtualizarJogadores] Nenhum jogador pendente encontrado.");
                _monitor.Atualizar(Chave, s =>
                {
                    s.CiclosCompletos++;
                    s.UltimaAtividade = "Ciclo concluído — nenhum jogador pendente.";
                });
                return;
            }

            _logger.LogInformation("[AtualizarJogadores] Iniciando ciclo: {Total} jogadores pendentes.", jogadores.Count);
            _monitor.Atualizar(Chave, s =>
                s.UltimaAtividade = $"Atualizando {jogadores.Count} jogadores pendentes...");

            int atualizados = 0, falhas = 0;

            foreach (var jogador in jogadores)
            {
                if (ct.IsCancellationRequested) break;

                _monitor.Atualizar(Chave, s =>
                    s.UltimaAtividade = $"Processando: {jogador.Nome}");

                try
                {
                    _logger.LogInformation("[AtualizarJogadores] Verificando: {Nome}", jogador.Nome);

                    var info = await api.BuscarInfoJogadorAsync(jogador.IdApi!.Value, ct);

                    if (info == null)
                    {
                        _logger.LogWarning("[AtualizarJogadores] Sem dados para: {Nome}", jogador.Nome);
                        falhas++;
                    }
                    else
                    {
                        bool alterado = false;

                        if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900 &&
                            info.DataNascimento.Value.Date != jogador.DataNascimento?.Date)
                        {
                            jogador.DataNascimento = DateTime.SpecifyKind(
                                info.DataNascimento.Value, DateTimeKind.Unspecified);
                            alterado = true;
                        }

                        if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                        {
                            var nac = await ResolverOuCriarNacionalidade(context, info.Nacionalidade, ct);
                            if (nac != null && jogador.NacionalidadeId != nac.Id)
                            {
                                jogador.NacionalidadeId = nac.Id;
                                alterado = true;
                            }
                        }

                        if (!string.IsNullOrEmpty(info.FotoUrl) && info.FotoUrl != jogador.FotoUrl)
                        {
                            jogador.FotoUrl = info.FotoUrl;
                            alterado = true;
                        }

                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.UtcNow;

                        await context.SaveChangesAsync(ct);
                        if (alterado) atualizados++;

                        _logger.LogInformation("[AtualizarJogadores] {Status}: {Nome}",
                            alterado ? "Atualizado" : "Sem alteração", jogador.Nome);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro ao processar: {Nome}", jogador.Nome);
                    falhas++;
                }

                await Task.Delay(IntervaloEntreJogadores, ct);
            }

            _logger.LogInformation("[AtualizarJogadores] Ciclo concluído. Atualizados: {Ok} | Falhas: {Fail}",
                atualizados, falhas);

            _monitor.Atualizar(Chave, s =>
            {
                s.CiclosCompletos++;
                s.JogadoresAtualizados += atualizados;
                s.Falhas += falhas;
                s.UltimaAtividade =
                    $"Ciclo #{s.CiclosCompletos} concluído — {atualizados} atualizados, {falhas} falhas.";
            });
        }

        private async Task SincronizarCompeticoesApiFootball(
            FutebolContext context,
            ApiFootballService api,
            CancellationToken ct)
        {
            var competicoes = await context.Competicoes
                .Where(c => !string.IsNullOrWhiteSpace(c.linktransfermarket) &&
                            c.linktransfermarket.StartsWith("apifoot:"))
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            if (!competicoes.Any())
            {
                _logger.LogInformation("[AtualizarJogadores] Nenhuma competição com link apifoot: encontrada.");
                return;
            }

            foreach (var competicao in competicoes)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("[AtualizarJogadores] Sincronizando: {Nome}", competicao.Nome);
                _monitor.Atualizar(Chave, s =>
                    s.UltimaAtividade = $"Sincronizando: {competicao.Nome}...");

                try
                {
                    var (jogos, times, erros, _) = await api.SincronizarCompeticaoAsync(context, competicao, ct);
                    _logger.LogInformation(
                        "[AtualizarJogadores] {Nome}: {J} jogos, {T} times, {E} erros.",
                        competicao.Nome, jogos, times, erros);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro ao sincronizar {Nome}.", competicao.Nome);
                }

                // Pausa entre competições para não esgotar as requisições da API
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        private static async Task<Nacionalidade?> ResolverOuCriarNacionalidade(
            FutebolContext context, string nomeRaw, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeRaw)) return null;

            var nomeCanonical = AdminController.ResolverNomeCanonical(nomeRaw) ?? nomeRaw.Trim();

            var nac = await context.Nacionalidades
                .FirstOrDefaultAsync(n => n.Nome.ToLower() == nomeCanonical.ToLower(), ct);

            if (nac == null)
                nac = await context.Nacionalidades
                    .FirstOrDefaultAsync(n => n.Nome.ToLower() == nomeRaw.Trim().ToLower(), ct);

            if (nac == null)
            {
                nac = new Nacionalidade { Nome = nomeCanonical };
                context.Nacionalidades.Add(nac);
                await context.SaveChangesAsync(ct);
            }

            return nac;
        }
    }
}
