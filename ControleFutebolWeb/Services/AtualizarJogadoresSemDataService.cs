using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Services
{
    public class AtualizarJogadoresSemDataService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarJogadoresSemDataService> _logger;

        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromHours(6);
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

                _logger.LogInformation("[AtualizarJogadores] Próximo ciclo em {Horas}h.",
                    IntervaloEntreCiclos.TotalHours);
                await Task.Delay(IntervaloEntreCiclos, stoppingToken);
            }

            _logger.LogInformation("[AtualizarJogadores] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var ogol = scope.ServiceProvider.GetRequiredService<OgolService>();

            await SincronizarCompeticoesTimesJogos(context, ogol, ct);
            await SincronizarEventosJogos(context, ogol, ct);

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

            _logger.LogInformation("[AtualizarJogadores] Iniciando ciclo: {Total} jogadores pendentes.", jogadores.Count);

            int atualizados = 0, falhas = 0;

            foreach (var jogador in jogadores)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("[AtualizarJogadores] Verificando: {Nome}", jogador.Nome);

                    var info = await ogol.BuscarJogadorPorLink(jogador.linktransfermarket);

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
                            var nac = await context.Nacionalidades
                                .FirstOrDefaultAsync(n => n.Nome.ToLower() == info.Nacionalidade.ToLower(), ct);
                            if (nac == null)
                            {
                                nac = new Nacionalidade { Nome = info.Nacionalidade };
                                context.Nacionalidades.Add(nac);
                                await context.SaveChangesAsync(ct);
                            }
                            if (jogador.NacionalidadeId != nac.Id)
                            {
                                jogador.NacionalidadeId = nac.Id;
                                alterado = true;
                            }
                        }

                        var fotoUrl = await ogol.BuscarFotoJogador(jogador);
                        if (!string.IsNullOrEmpty(fotoUrl) && fotoUrl != jogador.FotoUrl)
                        {
                            jogador.FotoUrl = fotoUrl;
                            alterado = true;
                        }

                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

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
        }

        private async Task SincronizarCompeticoesTimesJogos(
            FutebolContext context,
            OgolService ogol,
            CancellationToken ct)
        {
            await context.TransfermarktSincronizacaoLogs.ExecuteDeleteAsync(ct);
            _logger.LogInformation("[OgolSync] Logs anteriores removidos.");

            var cicloId = Guid.NewGuid();
            RegistrarLog(context, cicloId, "Ciclo", "Iniciado", detalhes: "Sincronização Ogol iniciada.");

            var competicoes = await context.Competicoes
                .Where(c => !string.IsNullOrWhiteSpace(c.linktransfermarket))
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            foreach (var competicao in competicoes)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("[OgolSync] Competição: {Nome}", competicao.Nome);
                RegistrarLog(context, cicloId, "Competicao", "Verificando", competicaoNome: competicao.Nome);

                var link = competicao.linktransfermarket!;
                var jogosWeb = await ogol.BuscarJogosCompeticaoPorLink(link, ct);

                _logger.LogInformation("[OgolSync] {Total} jogos encontrados para {Nome}.",
                    jogosWeb.Count, competicao.Nome);

                foreach (var jogoWeb in jogosWeb)
                {
                    var timeCasa = await ResolverOuCriarTime(context, ogol,
                        jogoWeb.NomeTimeCasa, jogoWeb.LinkTimeCasa, jogoWeb.EscudoTimeCasa, cicloId, ct);
                    var timeVisitante = await ResolverOuCriarTime(context, ogol,
                        jogoWeb.NomeTimeVisitante, jogoWeb.LinkTimeVisitante, jogoWeb.EscudoTimeVisitante, cicloId, ct);

                    await ogol.IncluirOuAtualizarJogo(
                        context, competicao, jogoWeb, timeCasa, timeVisitante, cicloId, ct);
                    await context.SaveChangesAsync(ct);

                    // Para jogos futuros (sem placar), importa elenco via página do time
                    if (!jogoWeb.PlacarCasa.HasValue)
                    {
                        await ImportarElencoSeNecessario(context, ogol,
                            timeCasa, jogoWeb.LinkTimeCasaComEdicao, cicloId, ct);
                        await ImportarElencoSeNecessario(context, ogol,
                            timeVisitante, jogoWeb.LinkTimeVisitanteComEdicao, cicloId, ct);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }

            RegistrarLog(context, cicloId, "Ciclo", "Concluido", detalhes: "Sincronização Ogol concluída.");
            await context.SaveChangesAsync(ct);
        }

        private async Task<Time> ResolverOuCriarTime(
            FutebolContext context,
            OgolService ogol,
            string nome,
            string? linkOgol,
            string? escudoUrl,
            Guid cicloId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nome))
                nome = $"Time_{Guid.NewGuid()}";

            var urlNorm = !string.IsNullOrWhiteSpace(linkOgol)
                ? ogol.NormalizarLink(linkOgol)
                : null;

            if (!string.IsNullOrWhiteSpace(urlNorm))
            {
                var porUrl = await context.Times
                    .FirstOrDefaultAsync(t => t.linktransfermarket == urlNorm, ct);
                if (porUrl != null)
                {
                    if (string.IsNullOrWhiteSpace(porUrl.EscudoUrl) &&
                        !string.IsNullOrWhiteSpace(escudoUrl))
                        porUrl.EscudoUrl = escudoUrl;
                    return porUrl;
                }
            }

            var porNome = await context.Times.FirstOrDefaultAsync(t => t.Nome == nome, ct);
            if (porNome != null)
            {
                if (string.IsNullOrWhiteSpace(porNome.linktransfermarket) &&
                    !string.IsNullOrWhiteSpace(urlNorm))
                    porNome.linktransfermarket = urlNorm;
                if (string.IsNullOrWhiteSpace(porNome.EscudoUrl) &&
                    !string.IsNullOrWhiteSpace(escudoUrl))
                    porNome.EscudoUrl = escudoUrl;
                return porNome;
            }

            var novoTime = new Time
            {
                Nome = nome,
                Cidade = "Importado",
                IdApi = 0,
                EscudoUrl = escudoUrl ?? "",
                CorPrincipal = "#000000",
                CorSecundaria = "#FFFFFF",
                linktransfermarket = urlNorm,
                FormacaoPadraoId = await ObterFormacaoPadraoId(context, ct)
            };

            await context.Times.AddAsync(novoTime, ct);
            await context.SaveChangesAsync(ct);

            RegistrarLog(context, cicloId, "Time", "Criado",
                timeNome: novoTime.Nome, detalhes: "Criado via Ogol");

            return novoTime;
        }

        private async Task ImportarElencoSeNecessario(
            FutebolContext context,
            OgolService ogol,
            Time time,
            string? linkComEdicao,
            Guid cicloId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(linkComEdicao)) return;

            // Só importa se o time ainda não tem jogadores
            var temJogadores = await context.Jogadores
                .AnyAsync(j => j.TimeId == time.Id, ct);
            if (temJogadores) return;

            _logger.LogInformation("[OgolElenco] Importando elenco de {Time} via {Link}",
                time.Nome, linkComEdicao);

            var elenco = await ogol.BuscarElencoTimePorLink(linkComEdicao, ct);
            if (elenco.Count == 0) return;

            var formacaoId = await ObterFormacaoPadraoId(context, ct);

            foreach (var j in elenco)
            {
                if (string.IsNullOrWhiteSpace(j.Nome)) continue;

                // Dedup por link ogol
                var linkNorm = !string.IsNullOrWhiteSpace(j.JogadorLink)
                    ? ogol.NormalizarLink(j.JogadorLink) : null;

                Jogador? jogador = null;
                if (!string.IsNullOrWhiteSpace(linkNorm))
                    jogador = await context.Jogadores
                        .FirstOrDefaultAsync(x => x.linktransfermarket == linkNorm, ct);

                if (jogador == null)
                    jogador = await context.Jogadores
                        .FirstOrDefaultAsync(x => x.Nome == j.Nome && x.TimeId == time.Id, ct);

                if (jogador != null)
                {
                    // Atualiza campos vazios
                    if (string.IsNullOrWhiteSpace(jogador.linktransfermarket) && !string.IsNullOrWhiteSpace(linkNorm))
                        jogador.linktransfermarket = linkNorm;
                    if (string.IsNullOrWhiteSpace(jogador.FotoUrl) && !string.IsNullOrWhiteSpace(j.FotoUrl))
                        jogador.FotoUrl = j.FotoUrl;
                    continue;
                }

                var novoJogador = new Jogador
                {
                    Nome = j.Nome,
                    TimeId = time.Id,
                    Posicao = j.Posicao,
                    NumeroCamisa = j.Numero,
                    linktransfermarket = linkNorm,
                    FotoUrl = j.FotoUrl,
                    IdApi = j.IdExterno.HasValue ? (int)j.IdExterno.Value : 0,
                    DataNascimento = null,
                    DtInc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    Atualizado = false
                };

                context.Jogadores.Add(novoJogador);
            }

            await context.SaveChangesAsync(ct);

            RegistrarLog(context, cicloId, "Time", "ElencoImportado",
                timeNome: time.Nome,
                detalhes: $"{elenco.Count} jogadores importados do elenco");
        }

        private async Task SincronizarEventosJogos(
            FutebolContext context,
            OgolService ogol,
            CancellationToken ct)
        {
            var jogos = await context.Jogos
                .Where(j => j.Status == "Finalizado" && !string.IsNullOrEmpty(j.LinkDetalhes))
                .OrderByDescending(j => j.Data)
                .Take(200)
                .ToListAsync(ct);

            foreach (var jogo in jogos)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var (gols, assistencias, cartoes) = await ogol.ImportarEventosPorLinkAsync(
                        context, jogo, jogo.LinkDetalhes!, ct);

                    _logger.LogInformation(
                        "[Eventos] Jogo {Id}: {G} gols, {A} assists, {C} cartões",
                        jogo.Id, gols, assistencias, cartoes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Eventos] Erro ao sincronizar jogo {Id}", jogo.Id);
                }

                await Task.Delay(1500, ct);
            }
        }

        private static void RegistrarLog(
            FutebolContext context,
            Guid cicloId,
            string tipo,
            string acao,
            string? competicaoNome = null,
            string? timeNome = null,
            string? jogoDescricao = null,
            string? detalhes = null)
        {
            context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId = cicloId,
                Data = DateTime.UtcNow,
                Tipo = tipo,
                Acao = acao,
                CompeticaoNome = competicaoNome,
                TimeNome = timeNome,
                JogoDescricao = jogoDescricao,
                Detalhes = detalhes
            });
        }

        private static async Task<int> ObterFormacaoPadraoId(FutebolContext context, CancellationToken ct)
        {
            var formacao = await context.Formacoes.OrderBy(f => f.Id).FirstOrDefaultAsync(ct);
            return formacao?.Id ?? 1;
        }
    }
}
