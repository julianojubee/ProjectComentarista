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
    /// O placar de verdade só é gravado quando uma fonte diz que a partida terminou.
    ///
    /// Depois da api-football, cada jogo é conferido contra o FotMob
    /// (ComplementoFotMobService): gol, cartão, substituição, estatística ou placar que
    /// o FotMob tenha a mais substitui o que a api-football trouxe. É o que cobre a
    /// partida em que ela fica parada — escalação e estatísticas chegando, mas status
    /// "NS" e nenhum evento até depois do apito final.
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
                + "estatísticas), completa com o FotMob o que a api-football não trouxe e grava o "
                + "placar parcial mostrado em Jogos/Hoje.");
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
        /// Um jogo: reimporta pela api-football (e, quando ela não tem a partida, tenta
        /// os lances na fonte alternativa) e em seguida confere o resultado contra o
        /// FotMob, que completa o que tiver a mais. Devolve se alguma fonte respondeu.
        ///
        /// Reimportação e conferência vão numa transação só: o que a api-football
        /// trouxe só é gravado de fato depois de comparado com o FotMob. Sem isso a tela
        /// de análise poderia pegar, entre um passo e outro, o jogo com os dados
        /// incompletos da api-football. O FotMob é baixado ANTES de abrir a transação,
        /// para a chamada HTTP não ficar segurando a transação aberta.
        /// </summary>
        private async Task<bool> AtualizarJogoAsync(int jogoId, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var apiFootball = scope.ServiceProvider.GetRequiredService<ApiFootballService>();
            var complemento = scope.ServiceProvider.GetRequiredService<ComplementoFotMobService>();

            using var fotmob = await BuscarFotMobAsync(complemento, ctx, jogoId, ct);
            var temFotMob = fotmob?.Documento != null;

            var ok = false;
            var transacao = await ctx.Database.BeginTransactionAsync(ct);
            try
            {
                try
                {
                    var (okApi, msg) = await apiFootball.ForcarReimportarEscalacaoAsync(ctx, jogoId, ct);
                    _logger.LogInformation("[JogosAoVivo] Jogo {Id}: {Ok} — {Msg}", jogoId, okApi, msg);
                    ok = okApi;

                    // A api-football não publica esta partida (competição feminina da FIFA
                    // e mata-mata sul-americano são os casos de sempre). Os lances ainda
                    // podem vir da fonte alternativa.
                    if (!ok) ok = await TentarLancesNaFonteAlternativaAsync(scope, ctx, jogoId, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && temFotMob)
                {
                    // api-football fora do ar ou cota estourada: é justamente quando o
                    // FotMob mais faz falta. Desfaz o que a reimportação deixou pela
                    // metade e segue só com ele. Sem FotMob o erro sobe, como antes.
                    _logger.LogError(ex, "[JogosAoVivo] Reimportação do jogo {Id} falhou; seguindo só com o FotMob.", jogoId);
                    await transacao.RollbackAsync(ct);
                    await transacao.DisposeAsync();
                    ctx.ChangeTracker.Clear();
                    transacao = await ctx.Database.BeginTransactionAsync(ct);
                }

                // Escalação que a api-football não trouxe: FotMob, depois ESPN. Vem antes
                // da conferência porque é contra os escalados que as estatísticas por
                // jogador (e os lances de homônimos) são casados.
                var completouEscalacao = await PreencherEscalacaoFaltandoAsync(scope, ctx, jogoId, ct);

                var completou = temFotMob && await ConferirComFotMobAsync(complemento, ctx, jogoId, fotmob!, ct);
                completou |= completouEscalacao;

                await transacao.CommitAsync(ct);
                return ok || completou;
            }
            finally
            {
                await transacao.DisposeAsync();
            }
        }

        /// <summary>
        /// Só a escalação compartilhada (sem usuário). Jogo da FIFA fica de fora: a
        /// competição não tem liga na api-football, e sem ela nem FotMob nem ESPN sabem
        /// onde procurar.
        /// </summary>
        private async Task<bool> PreencherEscalacaoFaltandoAsync(
            IServiceScope scope, FutebolContext ctx, int jogoId, CancellationToken ct)
        {
            try
            {
                var link = await ctx.Jogos.AsNoTracking()
                    .Where(j => j.Id == jogoId).Select(j => j.LinkDetalhes).FirstOrDefaultAsync(ct);
                if (FifaService.RefDaPartida(link) != null) return false;

                var alternativa = scope.ServiceProvider.GetRequiredService<EscalacaoAlternativaService>();
                var preenchidos = await alternativa.PreencherLadosFaltandoAsync(ctx, jogoId, usuarioId: null, ct);

                foreach (var p in preenchidos)
                    _logger.LogInformation("[JogosAoVivo] Jogo {Id} › escalação {Fonte}: {Msg}", jogoId, p.Fonte, p.Mensagem);

                return preenchidos.Count > 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Mesmo tratamento da conferência: complemento que falha não derruba o
                // que a api-football trouxe.
                _logger.LogError(ex, "[JogosAoVivo] Erro ao buscar a escalação alternativa do jogo {Id}.", jogoId);
                ctx.ChangeTracker.Clear();
                return false;
            }
        }

        private async Task<bool> ConferirComFotMobAsync(
            ComplementoFotMobService complemento, FutebolContext ctx, int jogoId,
            ComplementoFotMobService.PartidaAberta fotmob, CancellationToken ct)
        {
            try
            {
                var r = await complemento.AplicarAsync(ctx, jogoId, fotmob, ct);
                _logger.LogInformation("[JogosAoVivo] Jogo {Id} › FotMob: {Msg}", jogoId, r.Mensagem);
                return r.Alterou;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A conferência é complemento: se ela falhar, o que a api-football trouxe
                // continua valendo e é gravado mesmo assim. O contexto é limpo porque a
                // falha pode ter deixado trocas pela metade nele.
                _logger.LogError(ex, "[JogosAoVivo] Erro ao comparar o jogo {Id} com o FotMob.", jogoId);
                ctx.ChangeTracker.Clear();
                return false;
            }
        }

        private async Task<ComplementoFotMobService.PartidaAberta?> BuscarFotMobAsync(
            ComplementoFotMobService complemento, FutebolContext ctx, int jogoId, CancellationToken ct)
        {
            try
            {
                var aberta = await complemento.BuscarAsync(ctx, jogoId, ct);
                if (aberta.Documento == null && !aberta.SemCobertura)
                    _logger.LogInformation("[JogosAoVivo] Jogo {Id} › FotMob: {Msg}", jogoId, aberta.Falha);
                return aberta;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[JogosAoVivo] Falha ao buscar o jogo {Id} no FotMob.", jogoId);
                return null;
            }
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
