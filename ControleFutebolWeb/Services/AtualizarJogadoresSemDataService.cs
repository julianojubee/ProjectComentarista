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

            // Busca jogadores com data inválida: MinValue ou -infinity do Postgres
            var dataLimite = new DateTime(1900, 1, 1);

            var jogadoresSemData = await context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .Where(j => j.DataNascimento <= dataLimite)
                .OrderBy(j => j.Id)
                .ToListAsync(ct);

            if (!jogadoresSemData.Any())
            {
                _logger.LogInformation("[AtualizarJogadores] Nenhum jogador sem data encontrado.");
                return;
            }

            _logger.LogInformation(
                "[AtualizarJogadores] Iniciando ciclo: {Total} jogadores sem data.",
                jogadoresSemData.Count);

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogador in jogadoresSemData)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var nomeClube = jogador.Time?.Nome;

                    _logger.LogInformation(
                        "[AtualizarJogadores] Buscando: {Nome} (Clube: {Clube})",
                        jogador.Nome, nomeClube);

                    var info = await transfermarkt.BuscarJogador(jogador.Nome, nomeClube);

                    if (info == null)
                    {
                        _logger.LogWarning(
                            "[AtualizarJogadores] Não encontrado no Transfermarkt: {Nome}",
                            jogador.Nome);
                        falhas++;
                    }
                    else
                    {
                        bool alterado = false;

                        // Atualiza data de nascimento
                        if (info.DataNascimento.HasValue &&
                            info.DataNascimento.Value > dataLimite)
                        {
                            jogador.DataNascimento = DateTime.SpecifyKind(
                                info.DataNascimento.Value, DateTimeKind.Unspecified);
                            alterado = true;
                        }

                        // Atualiza nacionalidade se vier do Transfermarkt e jogador não tiver
                        if (!string.IsNullOrWhiteSpace(info.Nacionalidade) &&
                            jogador.NacionalidadeId == null)
                        {
                            var nacionalidade = await context.Nacionalidades
                                .FirstOrDefaultAsync(n =>
                                    n.Nome.ToLower() == info.Nacionalidade.ToLower(), ct);

                            if (nacionalidade == null)
                            {
                                nacionalidade = new Nacionalidade { Nome = info.Nacionalidade };
                                context.Nacionalidades.Add(nacionalidade);
                                await context.SaveChangesAsync(ct);
                                _logger.LogInformation(
                                    "[AtualizarJogadores] Nova nacionalidade criada: {Nac}",
                                    info.Nacionalidade);
                            }

                            jogador.NacionalidadeId = nacionalidade.Id;
                            alterado = true;
                        }

                        if (alterado)
                        {
                            await context.SaveChangesAsync(ct);
                            atualizados++;
                            _logger.LogInformation(
                                "[AtualizarJogadores] ✅ Atualizado: {Nome} | Nasc: {Data} | Nac: {Nac}",
                                jogador.Nome,
                                jogador.DataNascimento.ToString("dd/MM/yyyy"),
                                info.Nacionalidade);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[AtualizarJogadores] ⚠️ Encontrado mas sem dados novos: {Nome}",
                                jogador.Nome);
                            falhas++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[AtualizarJogadores] Erro ao processar jogador {Nome}", jogador.Nome);
                    falhas++;
                }

                // Pausa entre jogadores para respeitar o rate limit do Transfermarkt
                await Task.Delay(IntervaloEntreJogadores, ct);
            }

            _logger.LogInformation(
                "[AtualizarJogadores] Ciclo concluído. Atualizados: {Ok} | Falhas: {Fail}",
                atualizados, falhas);
        }
    }
}
