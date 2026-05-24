//using ControleFutebolWeb.Data;
//using ControleFutebolWeb.Models;
//using ControleFutebolWeb.Services;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Options;
//using System;
//using System.Threading;
//using System.Threading.Tasks;


//public class AtualizacaoJogosService : BackgroundService
//{
//    private readonly IServiceProvider _serviceProvider;
//    private readonly ApiFootballDataService _apiService;
//    private readonly CompeticoesApiOptions _options;

//    public AtualizacaoJogosService(
//        IServiceProvider serviceProvider,
//        ApiFootballDataService apiService,
//        IOptions<CompeticoesApiOptions> options)
//    {
//        _serviceProvider = serviceProvider;
//        _apiService = apiService;
//        _options = options.Value;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        while (!stoppingToken.IsCancellationRequested)
//        {
//            using (var scope = _serviceProvider.CreateScope())
//            {
//                var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();

//                try
//                {
//                    foreach (var codigo in _options.Codigos)
//                    {
//                        var jogosApi = await _apiService.GetMatchesAsync(codigo);


//                        foreach (var jogoApi in jogosApi)
//                        {
//                            var jogoDb = await context.Jogos
//                                .FirstOrDefaultAsync(j => j.PartidaApiId == jogoApi.Id, stoppingToken);

//                            if (jogoDb != null)
//                            {
//                                jogoDb.PlacarCasa = jogoApi.Score.FullTime.Home;
//                                jogoDb.PlacarVisitante = jogoApi.Score.FullTime.Away;
//                                jogoDb.Data = jogoApi.UtcDate;
//                                jogoDb.Rodada = jogoApi.Matchday ?? jogoDb.Rodada;

//                                context.Jogos.Update(jogoDb);

//                                Console.WriteLine(
//                                    $"[Atualização {codigo}] {jogoApi.HomeTeam.Name} {jogoDb.PlacarCasa} x {jogoDb.PlacarVisitante} {jogoApi.AwayTeam.Name}"
//                                );
//                            }

//                        }
//                    }

//                    await context.SaveChangesAsync(stoppingToken);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[Erro] Falha ao atualizar jogos: {ex.Message}");
//                }
//            }


//            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
//        }
//    }
//}
