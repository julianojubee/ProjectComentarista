using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Serviço em background que busca periodicamente jogadores com data de nascimento
    /// inválida (-infinity / MinValue) e tenta atualizar via Transfermarkt.
    /// </summary>
    public class AtualizarJogadoresSemDataService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarJogadoresSemDataService> _logger;

        // Intervalo entre cada ciclo completo (padrão: 6 horas)
        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromHours(6);

        // Intervalo entre cada jogador dentro do ciclo (evita bloqueio do Transfermarkt)
        private static readonly TimeSpan IntervaloEntreJogadores = TimeSpan.FromSeconds(3);

        public AtualizarJogadoresSemDataService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarJogadoresSemDataService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[AtualizarJogadores] Serviço iniciado.");

            // Aguarda 30 segundos após o start da aplicação antes do primeiro ciclo
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

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
                    _logger.LogError(ex, "[AtualizarJogadores] Erro inesperado no ciclo.");
                }

                _logger.LogInformation(
                    "[AtualizarJogadores] Próximo ciclo em {Horas}h.",
                    IntervaloEntreCiclos.TotalHours);

                await Task.Delay(IntervaloEntreCiclos, stoppingToken);
            }

            _logger.LogInformation("[AtualizarJogadores] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var transfermarkt = scope.ServiceProvider.GetRequiredService<TransfermarktService>();

            var jogadores = await context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .Where(j => !string.IsNullOrEmpty(j.linktransfermarket) && !j.Atualizado)
                .OrderBy(j => j.Id)
                .ToListAsync(ct);

            if (!jogadores.Any())
            {
                _logger.LogInformation("[AtualizarJogadores] Nenhum jogador pendente encontrado.");
                return;
            }

            _logger.LogInformation("[AtualizarJogadores] Iniciando ciclo: {Total} jogadores com link Transfermarkt.", jogadores.Count);

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogador in jogadores)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("[AtualizarJogadores] Verificando jogador: {Nome}", jogador.Nome);

                    var info = await transfermarkt.BuscarJogadorPorLink(jogador.linktransfermarket);

                    if (info == null)
                    {
                        _logger.LogWarning("[AtualizarJogadores] Não foi possível obter dados do Transfermarkt: {Nome}", jogador.Nome);
                        falhas++;
                    }
                    else
                    {
                        bool alterado = false;

                        // Atualiza data de nascimento
                        if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900 &&
                            info.DataNascimento.Value.Date != jogador.DataNascimento.Date)
                        {
                            jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Utc);
                            alterado = true;
                        }

                        // Atualiza nacionalidade
                        if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                        {
                            var nacionalidade = await context.Nacionalidades
                                .FirstOrDefaultAsync(n => n.Nome.ToLower() == info.Nacionalidade.ToLower(), ct);

                            if (nacionalidade == null)
                            {
                                nacionalidade = new Nacionalidade { Nome = info.Nacionalidade };
                                context.Nacionalidades.Add(nacionalidade);
                                await context.SaveChangesAsync(ct);
                                _logger.LogInformation("[AtualizarJogadores] Nova nacionalidade criada: {Nac}", info.Nacionalidade);
                            }

                            if (jogador.NacionalidadeId != nacionalidade.Id)
                            {
                                jogador.NacionalidadeId = nacionalidade.Id;
                                alterado = true;
                            }
                        }

                        // Atualiza foto
                        var fotoUrl = await transfermarkt.BuscarFotoJogador(jogador);
                        if (!string.IsNullOrEmpty(fotoUrl) && fotoUrl != jogador.FotoUrl)
                        {
                            jogador.FotoUrl = fotoUrl;
                            alterado = true;
                        }

                        // Marca como atualizado
                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.UtcNow;

                        if (alterado)
                        {
                            await context.SaveChangesAsync(ct);
                            atualizados++;
                            _logger.LogInformation("[AtualizarJogadores] ✅ Atualizado: {Nome}", jogador.Nome);
                        }
                        else
                        {
                            await context.SaveChangesAsync(ct); // mesmo sem alteração, marca Atualizado=true
                            _logger.LogInformation("[AtualizarJogadores] ⚠️ Nenhuma alteração necessária: {Nome}", jogador.Nome);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro ao processar jogador {Nome}", jogador.Nome);
                    falhas++;
                }

                await Task.Delay(IntervaloEntreJogadores, ct);
            }

            _logger.LogInformation("[AtualizarJogadores] Ciclo concluído. Atualizados: {Ok} | Falhas: {Fail}", atualizados, falhas);
        }

    }
}
