using ControleFutebolWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Mantém os jogos em andamento atualizados sozinho: a cada 15 minutos faz, para
    /// cada jogo ao vivo, o mesmo que o botão "Reimportar dados" da tela de análise —
    /// escalação, gols, cartões, substituições e estatísticas saem da api-football na
    /// mesma visita, e de lambuja o placar corrente é gravado em
    /// Jogo.PlacarParcialCasa/Visitante para a tela Jogos/Hoje mostrar o resultado
    /// enquanto a partida rola.
    ///
    /// O parcial NÃO vai para PlacarCasa/PlacarVisitante: esses dois são o que o resto
    /// do sistema lê como "jogo realizado" (ver comentário em Jogo.PlacarParcialCasa).
    /// Quem grava o placar de verdade continua sendo só a reimportação, e só quando a
    /// API diz que a partida terminou.
    ///
    /// Diferente do botão, aqui não há usuário logado: o ciclo mexe apenas nos dados
    /// compartilhados (Escalacao.UsuarioId == null). A escalação pessoal de ninguém é
    /// apagada nem recriada — quem quiser recarregar a dele continua clicando no botão.
    /// </summary>
    public class AtualizarJogosAoVivoService : BackgroundService
    {
        public const string Chave = "AtualizarJogosAoVivo";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarJogosAoVivoService> _logger;
        private readonly ServicoMonitor _monitor;

        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromMinutes(15);

        // Janela em que o jogo é considerado "em andamento". Começa no apito inicial e
        // vai além dos 2h30 que a tela Jogos/Hoje usa para pintar o card de vermelho: é
        // aqui que o placar final precisa ser capturado, e prorrogação + pênaltis +
        // atraso de publicação da API passam folgadamente dos 2h30.
        private static readonly TimeSpan DuracaoMaxima = TimeSpan.FromHours(4);

        public AtualizarJogosAoVivoService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarJogosAoVivoService> logger,
            ServicoMonitor monitor)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _monitor = monitor;
            _monitor.Registrar(Chave,
                "Atualizar Jogos ao Vivo",
                "A cada 15 min reimporta os dados dos jogos em andamento (escalação, gols, cartões, "
                + "estatísticas) e grava o placar parcial mostrado em Jogos/Hoje.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Aguardando;
                s.IniciadoEm = DateTime.Now;
                s.UltimaAtividade = "Aguardando 30s para iniciar...";
            });

            _logger.LogInformation("[JogosAoVivo] Serviço iniciado.");
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
                    _logger.LogError(ex, "[JogosAoVivo] Erro inesperado no ciclo.");
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
            _logger.LogInformation("[JogosAoVivo] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            _monitor.Atualizar(Chave, s =>
            {
                s.Estado = EstadoServico.Rodando;
                s.UltimoCicloEm = DateTime.Now;
                s.UltimaAtividade = "Procurando jogos em andamento...";
            });

            var ids = await JogosEmAndamentoAsync(ct);

            if (ids.Count == 0)
            {
                _logger.LogInformation("[JogosAoVivo] Nenhum jogo em andamento agora.");
                _monitor.Atualizar(Chave, s =>
                {
                    s.CiclosCompletos++;
                    s.UltimaAtividade = "Ciclo concluído — nenhum jogo em andamento.";
                });
                return;
            }

            _monitor.Atualizar(Chave, s =>
                s.UltimaAtividade = $"Atualizando {ids.Count} jogo(s) em andamento...");

            var atualizados = 0;
            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (await AtualizarJogoAsync(id, ct)) atualizados++;
                }
                catch (Exception ex)
                {
                    // Um jogo problemático não pode derrubar os outros do mesmo ciclo.
                    _logger.LogError(ex, "[JogosAoVivo] Erro ao atualizar o jogo {Id}.", id);
                }
            }

            _logger.LogInformation(
                "[JogosAoVivo] Ciclo concluído: {A} de {T} jogo(s) atualizado(s).", atualizados, ids.Count);

            _monitor.Atualizar(Chave, s =>
            {
                s.CiclosCompletos++;
                s.UltimaAtividade = $"Ciclo concluído — {atualizados} de {ids.Count} jogo(s) atualizado(s).";
            });
        }

        /// <summary>
        /// Ids dos jogos que já começaram, ainda não têm placar final e estão dentro da
        /// janela de duração. Jogo.Data fica em UTC no banco, então a conta é feita toda
        /// em UTC — converter para Brasília aqui só abriria espaço para errar o fuso.
        /// </summary>
        private async Task<List<int>> JogosEmAndamentoAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<FutebolContext>();

            var agoraUtc = DateTime.UtcNow;
            var maisAntigo = agoraUtc - DuracaoMaxima;

            return await ctx.Jogos
                .AsNoTracking()
                .Where(j => j.PlacarCasa == null && j.PlacarVisitante == null
                         && j.Data != null && j.Data <= agoraUtc && j.Data >= maisAntigo)
                .OrderBy(j => j.Data)
                .Select(j => j.Id)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Um jogo: reimporta pela api-football e, quando ela não tem a partida, tenta os
        /// lances na fonte alternativa. Devolve se alguma fonte respondeu.
        /// </summary>
        private async Task<bool> AtualizarJogoAsync(int jogoId, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var apiFootball = scope.ServiceProvider.GetRequiredService<ApiFootballService>();

            var (ok, msg) = await apiFootball.ForcarReimportarEscalacaoAsync(ctx, jogoId, ct);
            _logger.LogInformation("[JogosAoVivo] Jogo {Id}: {Ok} — {Msg}", jogoId, ok, msg);

            if (ok) return true;

            // A api-football não publica esta partida (competição feminina da FIFA e
            // mata-mata sul-americano são os casos de sempre). Os lances ainda podem vir
            // da fonte alternativa — o placar parcial não, porque só a api-football
            // devolve o gol a gol num número pronto.
            return await TentarLancesNaFonteAlternativaAsync(scope, ctx, jogoId, ct);
        }

        private async Task<bool> TentarLancesNaFonteAlternativaAsync(
            IServiceScope scope, FutebolContext ctx, int jogoId, CancellationToken ct)
        {
            var link = await ctx.Jogos.AsNoTracking()
                .Where(j => j.Id == jogoId).Select(j => j.LinkDetalhes).FirstOrDefaultAsync(ct);

            if (FifaService.RefDaPartida(link) != null)
            {
                var fifa = scope.ServiceProvider.GetRequiredService<FifaEventosService>();
                var r = await fifa.ImportarAsync(ctx, jogoId, ct);
                _logger.LogInformation("[JogosAoVivo] Jogo {Id} › FIFA lances: {Ok} — {Msg}",
                    jogoId, r.Ok, r.Mensagem);
                return r.Ok;
            }

            // ESPN só quando a competição tem slug mapeado; sem ele a chamada gastaria
            // tempo para devolver "competição sem cobertura".
            var espn = scope.ServiceProvider.GetRequiredService<EspnEstatisticasService>();
            var idApiLiga = await ctx.Jogos.AsNoTracking()
                .Where(j => j.Id == jogoId).Select(j => j.Competicao!.IdApi).FirstOrDefaultAsync(ct);

            if (espn.SlugDaLiga(idApiLiga) == null) return false;

            var eventos = scope.ServiceProvider.GetRequiredService<EspnEventosService>();
            var rEv = await eventos.ImportarAsync(ctx, jogoId, ct);
            _logger.LogInformation("[JogosAoVivo] Jogo {Id} › ESPN lances: {Ok} — {Msg}",
                jogoId, rEv.Ok, rEv.Mensagem);
            return rEv.Ok;
        }
    }
}
