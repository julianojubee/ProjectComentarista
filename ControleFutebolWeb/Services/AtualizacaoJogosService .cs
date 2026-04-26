using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Services;

public class AtualizacaoJogosService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ApiFootballDataService _apiService;

    public AtualizacaoJogosService(IServiceProvider serviceProvider, ApiFootballDataService apiService)
    {
        _serviceProvider = serviceProvider;
        _apiService = apiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();

                try
                {
                    var jogosApi = await _apiService.GetMatchesAsync("BSA"); // código da competição (Brasileirão)

                    foreach (var jogoApi in jogosApi)
                    {
                        var jogoDb = await context.Jogos
                            .FirstOrDefaultAsync(j => j.PartidaApiId == jogoApi.Id, stoppingToken);

                        if (jogoDb != null)
                        {
                            // Atualiza placares e data
                            jogoDb.PlacarCasa = jogoApi.Score.FullTime.Home;
                            jogoDb.PlacarVisitante = jogoApi.Score.FullTime.Away;
                            jogoDb.Data = jogoApi.UtcDate;
                            jogoDb.Rodada = jogoApi.Matchday;

                            context.Jogos.Update(jogoDb);

                            Console.WriteLine(
                                $"[Atualização] Rodada {jogoDb.Rodada}: " +
                                $"{jogoApi.HomeTeam.Name} {jogoDb.PlacarCasa} x {jogoDb.PlacarVisitante} {jogoApi.AwayTeam.Name}"
                            );
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Erro] Falha ao atualizar jogos: {ex.Message}");
                }
            }

            // Aguarda 15 minutos antes da próxima execução
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}