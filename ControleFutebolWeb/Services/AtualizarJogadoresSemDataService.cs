using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

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

        private readonly HttpClient _httpClient;

        public AtualizarJogadoresSemDataService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarJogadoresSemDataService> logger,
            IHttpClientFactory httpClientFactory)  // ← adicionar
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient(); // ← adicionar
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9");
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

            await SincronizarCompeticoesTimesJogosEElencos(context, transfermarkt, ct);
            await transfermarkt.CorrigirDatasJogos(context, transfermarkt, ct);
            await SincronizarEventosJogos(context, transfermarkt, ct);

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
                            info.DataNascimento.Value.Date != jogador.DataNascimento?.Date)
                        {

                            jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
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
                        jogador.DtAlt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

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

        private async Task SincronizarCompeticoesTimesJogosEElencos(
            FutebolContext context,
            TransfermarktService transfermarkt,
            CancellationToken ct)
        {
            // Limpa logs do ciclo anterior antes de iniciar o novo
            await context.TransfermarktSincronizacaoLogs.ExecuteDeleteAsync(ct);
            _logger.LogInformation("[TransfermarktSync] Logs anteriores removidos.");

            var cicloId = Guid.NewGuid();
            RegistrarLog(context, cicloId, "Ciclo", "Iniciado", detalhes: "Sincronização Transfermarkt iniciada.");

            var competicoes = await context.Competicoes
                .Where(c => !string.IsNullOrWhiteSpace(c.linktransfermarket))
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            foreach (var competicao in competicoes)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("[TransfermarktSync] Verificando competição: {Nome}", competicao.Nome);
                RegistrarLog(context, cicloId, "Competicao", "Verificando", competicaoNome: competicao.Nome);

                // ── Detecta o tipo de scraping pelo link ─────────────────────────
                var link = competicao.linktransfermarket!;
                List<TransfermarktJogoInfo> jogosWeb;

                bool isCupStyle = link.Contains("pokalwettbewerb") ||
                   link.Contains("copa") ||
                   competicao.Tipo == "MATA_MATA";

                bool isLeagueStyle = link.Contains("/wettbewerb/") ||
                                     link.Contains("gesamtspielplan") ||
                                     competicao.Tipo == "PONTOS_CORRIDOS";

                if (isCupStyle)
                {
                    jogosWeb = await transfermarkt.BuscarJogosCopaPorLink(link, ct);
                }
                else if (isLeagueStyle)
                {
                    jogosWeb = await transfermarkt.BuscarJogosLigaPorLink(link, ct);
                }
                else
                {
                    // fallback
                    jogosWeb = await transfermarkt.BuscarJogosCompeticaoPorLink(link, ct);
                }

                _logger.LogInformation("[TransfermarktSync] {Total} jogos encontrados para {Nome}.",
                    jogosWeb.Count, competicao.Nome);

                RegistrarLog(context, cicloId, "Competicao", "JogosEncontrados",
                    competicaoNome: competicao.Nome,
                    detalhes: $"{jogosWeb.Count} jogo(s) encontrado(s) no Transfermarkt.");

                foreach (var jogoWeb in jogosWeb)
                {
                    var timeCasa = await ResolverOuCriarTime(context, transfermarkt,
                        jogoWeb.NomeTimeCasa, jogoWeb.LinkTimeCasa, cicloId, ct);
                    var timeVisitante = await ResolverOuCriarTime(context, transfermarkt,
                        jogoWeb.NomeTimeVisitante, jogoWeb.LinkTimeVisitante, cicloId, ct);

                    await transfermarkt.IncluirOuAtualizarJogo(context, competicao, jogoWeb,
                        timeCasa, timeVisitante, cicloId,transfermarkt, ct);
                    await context.SaveChangesAsync(ct);

                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }

            var times = await context.Times
                .Where(t => !string.IsNullOrWhiteSpace(t.linktransfermarket))
                .OrderBy(t => t.Id)
                .ToListAsync(ct);

            foreach (var time in times)
            {
                if (ct.IsCancellationRequested) break;

                await SincronizarElencoTime(context, transfermarkt, time, cicloId, ct);
                await context.SaveChangesAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }

            RegistrarLog(context, cicloId, "Ciclo", "Concluido", detalhes: "Sincronização Transfermarkt concluída.");
            await context.SaveChangesAsync(ct);
        }

        private async Task<Time> ResolverOuCriarTime(
        FutebolContext context,
        TransfermarktService transfermarkt,
        string nome,
        string? linkTransfermarkt,
        Guid cicloId,
        CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nome))
                nome = $"Time_{Guid.NewGuid()}";

            var urlNormalizada = !string.IsNullOrWhiteSpace(linkTransfermarkt)
                ? transfermarkt.NormalizarLinkTransfermarkt(linkTransfermarkt)
                : null;

            // 1. Por URL exata
            if (!string.IsNullOrWhiteSpace(urlNormalizada))
            {
                var porUrl = await context.Times
                    .FirstOrDefaultAsync(t => t.linktransfermarket == urlNormalizada, ct);
                if (porUrl != null)
                {
                    _logger.LogInformation("[ResolverTime] Por URL: {Nome}", porUrl.Nome);
                    return porUrl;
                }
            }

            // 2. Por nome exato
            var porNome = await context.Times
                .FirstOrDefaultAsync(t => t.Nome == nome, ct);
            if (porNome != null)
            {
                // Aproveita para salvar o link se ainda não tiver
                if (string.IsNullOrWhiteSpace(porNome.linktransfermarket)
                    && !string.IsNullOrWhiteSpace(urlNormalizada))
                {
                    porNome.linktransfermarket = urlNormalizada;
                }
                _logger.LogInformation("[ResolverTime] Por nome exato: {Nome}", porNome.Nome);
                return porNome;
            }

            // 5. Cria novo
            var novoTime = new Time
            {
                Nome = nome,
                Cidade = "Importado",
                IdApi = 0,
                EscudoUrl = "",
                CorPrincipal = "#000000",
                CorSecundaria = "#FFFFFF",
                linktransfermarket = urlNormalizada,
                FormacaoPadraoId = await ObterFormacaoPadraoId(context, ct)
            };

            await context.Times.AddAsync(novoTime, ct);
            // Busca escudo e atualiza antes de salvar
            var escudoUrl = await transfermarkt.BuscarEscudoTimePorLink(linkTransfermarkt);
            if (!string.IsNullOrEmpty(escudoUrl))
            {
                novoTime.EscudoUrl = escudoUrl;
                _logger.LogInformation("[ResolverTime] Escudo atualizado para {Nome}: {Url}", novoTime.Nome, escudoUrl);
            }

            await context.SaveChangesAsync(ct);

            RegistrarLog(context, cicloId, "Time", "Criado",
                timeNome: novoTime.Nome,
                detalhes: $"Criado via Transfermarkt");

            _logger.LogInformation("[ResolverTime] Criado: {Nome}", novoTime.Nome);
            return novoTime;
        }


        // ── Resolução de jogador no banco ─────────────────────────────────────────
        private async Task<Jogador?> ResolverJogadorAsync(
            FutebolContext context,
            string nomeTM,
            long? idExterno,
            int timeId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeTM)) return null;

            if (idExterno.HasValue)
            {
                var porId = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.IdApi == idExterno && j.TimeId == timeId, ct);
                if (porId != null) return porId;
            }

            var porNome = await context.Jogadores
                .FirstOrDefaultAsync(j => j.Nome == nomeTM && j.TimeId == timeId, ct);
            if (porNome != null) return porNome;

            var porILike = await context.Jogadores
                .FirstOrDefaultAsync(j => j.TimeId == timeId &&
                    EF.Functions.ILike(j.Nome, nomeTM), ct);
            if (porILike != null) return porILike;

            var tokens = nomeTM.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sobrenome = tokens.LastOrDefault(t => t.Length > 3);
            if (sobrenome != null)
            {
                var porSobrenome = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.TimeId == timeId &&
                        EF.Functions.ILike(j.Nome, $"%{sobrenome}%"), ct);
                if (porSobrenome != null) return porSobrenome;
            }

            var nomeNorm = NormalizarTexto(nomeTM);
            var candidatos = await context.Jogadores
                .Where(j => j.TimeId == timeId)
                .ToListAsync(ct);

            return candidatos.FirstOrDefault(j =>
            {
                var jNorm = NormalizarTexto(j.Nome);
                return jNorm == nomeNorm
                    || jNorm.Contains(nomeNorm)
                    || nomeNorm.Contains(jNorm);
            });
        }

        private async Task SincronizarElencoTime(
        FutebolContext context,
        TransfermarktService transfermarkt,
        Time time,
        Guid cicloId,
        CancellationToken ct)
        {
            var elencoWeb = await transfermarkt.BuscarElencoTimePorLink(
                time.linktransfermarket!, ct);
            if (!elencoWeb.Any()) return;

            var jogadoresBanco = await context.Jogadores
                .Where(j => j.TimeId == time.Id)
                .ToListAsync(ct);

            foreach (var jogadorWeb in elencoWeb)
            {
                if (string.IsNullOrWhiteSpace(jogadorWeb.NomeCompleto)) continue;

                var nomeNorm = NormalizarTexto(jogadorWeb.NomeCompleto);

                // Busca por nome exato ou normalizado
                var jogador = jogadoresBanco.FirstOrDefault(j =>
                    j.Nome == jogadorWeb.NomeCompleto ||
                    NormalizarTexto(j.Nome) == nomeNorm ||
                    NormalizarTexto(j.Nome).Contains(nomeNorm) ||
                    nomeNorm.Contains(NormalizarTexto(j.Nome)));

                if (jogador != null)
                {
                    // Atualiza link se não tiver
                    if (string.IsNullOrWhiteSpace(jogador.linktransfermarket)
                        && !string.IsNullOrWhiteSpace(jogadorWeb.LinkPerfil))
                    {
                        jogador.linktransfermarket = jogadorWeb.LinkPerfil;
                        jogador.DtAlt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    }
                    continue;
                }

                // Cria novo jogador
                jogador = new Jogador
                {
                    Nome = jogadorWeb.NomeCompleto,
                    Posicao = jogadorWeb.Posicao ?? "Meio",
                    TimeId = time.Id,
                    NumeroCamisa = jogadorWeb.NumeroCamisa,
                    linktransfermarket = jogadorWeb.LinkPerfil,
                    DataNascimento = null,
                    DtInc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    Atualizado = false
                };

                context.Jogadores.Add(jogador);
                jogadoresBanco.Add(jogador);

                _logger.LogInformation("[SincElenco] Jogador incluído: {Nome} ({Time})",
                    jogador.Nome, time.Nome);

                RegistrarLog(context, cicloId, "Elenco", "JogadorCriado",
                    timeNome: time.Nome,
                    detalhes: $"Incluído: {jogador.Nome}");
            }
        }

        private async Task SincronizarEventosJogos(
        FutebolContext context,
        TransfermarktService transfermarkt,
        CancellationToken ct)
        {
            var jogos = await context.Jogos
                .Where(j => j.Status == "Finalizado" && !string.IsNullOrEmpty(j.LinkDetalhes))
                .OrderByDescending(j => j.Data)
                .Take(200) // limita para não sobrecarregar
                .ToListAsync(ct);

            foreach (var jogo in jogos)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("[Eventos] Buscando detalhes do jogo {Id}: {Casa} x {Vis}",
                        jogo.Id, jogo.TimeCasa.Nome, jogo.TimeVisitante.Nome);

                    // 🔹 Usa o novo método que resolve jogadores por link
                    var (gols, assistencias, cartoes) = await transfermarkt.ImportarEventosPorLinkJogadorAsync(
                        context, jogo, jogo.LinkDetalhes!, ct);

                    _logger.LogInformation(
                        "[Eventos] Jogo {Id} atualizado: {G} gols, {A} assistências, {C} cartões",
                        jogo.Id, gols, assistencias, cartoes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Eventos] Erro ao sincronizar jogo {Id}", jogo.Id);
                }

                await Task.Delay(1500, ct); // pausa para não ser bloqueado
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

        private static string MontarDescricaoJogo(Time timeCasa, Time timeVisitante, TransfermarktJogoInfo jogoWeb)
        {
            var placar = jogoWeb.PlacarCasa.HasValue
                ? $" {jogoWeb.PlacarCasa} x {jogoWeb.PlacarVisitante}"
                : string.Empty;

            var data = jogoWeb.Data.HasValue
                ? $" em {jogoWeb.Data.Value:dd/MM/yyyy}"
                : string.Empty;

            return $"{timeCasa.Nome}{placar} {timeVisitante.Nome}{data}";
        }

        private static async Task<int> ObterFormacaoPadraoId(FutebolContext context, CancellationToken ct)
        {
            var formacao = await context.Formacoes.OrderBy(f => f.Id).FirstOrDefaultAsync(ct);
            return formacao?.Id ?? 1;
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var s = texto.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("õ", "o").Replace("ê", "e").Replace("â", "a")
                .Replace("ô", "o").Replace("ç", "c").Replace("ñ", "n");

            return Regex.Replace(s, @"\b(cr|fc|sc|ec|ac|se|cf|cd|club|clube|futebol|football|de|do|da|dos|das)\b|\s+", "");
        }
    }
}
