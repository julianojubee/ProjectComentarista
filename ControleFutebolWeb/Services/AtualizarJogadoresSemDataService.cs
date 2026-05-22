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
            await CorrigirDatasJogos(context, transfermarkt, ct);
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
                            info.DataNascimento.Value.Date != jogador.DataNascimento.Date)
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

        private async Task CorrigirDatasJogos(FutebolContext context, TransfermarktService transfermarkt, CancellationToken ct)
        {
            var jogos = await context.Jogos
                .Where(j => !string.IsNullOrEmpty(j.LinkDetalhes))
                .ToListAsync(ct);

            foreach (var jogo in jogos)
            {
                try
                {
                    var novaData = await transfermarkt.BuscarDataJogoPorLink(jogo.LinkDetalhes, ct);
                    if (novaData.HasValue && (!jogo.Data.HasValue || jogo.Data.Value.Date != novaData.Value.Date))
                    {
                        jogo.Data = DateTime.SpecifyKind(novaData.Value, DateTimeKind.Utc);
                        _logger.LogInformation("[CorrigirDatas] Atualizado jogo {Casa} x {Vis} para {Data}",
                            jogo.TimeCasa.Nome, jogo.TimeVisitante.Nome, jogo.Data?.ToString("dd/MM/yyyy HH:mm"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CorrigirDatas] Erro ao atualizar data do jogo {Id}", jogo.Id);
                }
            }

            await context.SaveChangesAsync(ct);
        }

        private async Task<List<TransfermarktJogoInfo>> BuscarJogosCopaPorLink(
        string linkCompeticao, CancellationToken ct)
        {
            var jogos = new List<TransfermarktJogoInfo>();

            // Copa do Brasil no TM usa URLs tipo:
            // /copa-do-brasil/spieltag/pokalwettbewerb/BRC/saison_id/2025/spieltag/1
            // Extrai código e temporada do link
            var matchCodigo = Regex.Match(linkCompeticao,
                @"pokalwettbewerb/([^/]+)", RegexOptions.IgnoreCase);
            var matchSaison = Regex.Match(linkCompeticao,
                @"saison_id/(\d+)", RegexOptions.IgnoreCase);

            if (!matchCodigo.Success)
            {
                _logger.LogWarning("[CopaScraping] Não foi possível extrair código da competição: {Link}",
                    linkCompeticao);
                return jogos;
            }

            var codigo = matchCodigo.Groups[1].Value;

            // Tenta saison do link, senão usa ano atual - 1
            var saison = matchSaison.Success
                ? matchSaison.Groups[1].Value
                : (DateTime.UtcNow.Year - 1).ToString();

            _logger.LogInformation("[CopaScraping] Código={Codigo} Saison={Saison}", codigo, saison);

            // Tenta rodadas de 1 a 10 (Copa do Brasil tem até 7 fases)
            int rodadasSemJogos = 0;
            for (int rodada = 1; rodada <= 10 && rodadasSemJogos < 3; rodada++)
            {
                if (ct.IsCancellationRequested) break;

                var url = $"https://www.transfermarkt.com.br/-/spieltag/" +
                          $"pokalwettbewerb/{codigo}/saison_id/{saison}/spieltag/{rodada}";

                _logger.LogInformation("[CopaScraping] Buscando rodada {R}: {U}", rodada, url);

                string html;
                try
                {
                    var response = await _httpClient.GetAsync(url, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        rodadasSemJogos++;
                        continue;
                    }
                    html = await response.Content.ReadAsStringAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[CopaScraping] Erro rodada {R}: {M}", rodada, ex.Message);
                    rodadasSemJogos++;
                    continue;
                }

                if (!html.Contains("/spielbericht/") && !html.Contains("/begegnung_detail/"))
                {
                    _logger.LogInformation("[CopaScraping] Rodada {R}: sem jogos.", rodada);
                    rodadasSemJogos++;
                    continue;
                }

                var jogosRodada = ExtrairJogosDaRodada(html, rodada);
                if (jogosRodada.Any())
                {
                    jogos.AddRange(jogosRodada);
                    rodadasSemJogos = 0;
                    _logger.LogInformation("[CopaScraping] Rodada {R}: {N} jogos.", rodada, jogosRodada.Count);
                }
                else
                {
                    rodadasSemJogos++;
                }

                await Task.Delay(1500, ct);
            }

            return jogos;
        }

        private List<TransfermarktJogoInfo> ExtrairJogosDaRodada(string html, int rodada)
        {
            var jogos = new List<TransfermarktJogoInfo>();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var linksJogo = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/spielbericht/') or contains(@href,'/begegnung_detail/')]");

            if (linksJogo == null) return jogos;

            var processados = new HashSet<string>();

            foreach (var linkJogo in linksJogo)
            {
                var href = linkJogo.GetAttributeValue("href", "");
                if (processados.Contains(href)) continue;
                processados.Add(href);

                if (!href.StartsWith("http"))
                    href = "https://www.transfermarkt.com.br" + href;

                // Placar no texto do link
                var scoreText = HtmlAgilityPack.HtmlEntity.DeEntitize(linkJogo.InnerText.Trim());
                var scoreMatch = Regex.Match(scoreText, @"(\d+)\s*[:\-]\s*(\d+)");
                int? pc = null, pv = null;
                if (scoreMatch.Success)
                {
                    pc = int.Parse(scoreMatch.Groups[1].Value);
                    pv = int.Parse(scoreMatch.Groups[2].Value);
                }

                // Sobe na DOM para encontrar bloco com os times
                var bloco = linkJogo.ParentNode;
                for (int i = 0; i < 6; i++)
                {
                    if (bloco == null || bloco.Name == "tr") break;
                    bloco = bloco.ParentNode;
                }
                if (bloco == null) continue;

                var linksTime = bloco.SelectNodes(
                    ".//a[contains(@href,'/verein/') and not(contains(@href,'/spielbericht/'))]");
                if (linksTime == null || linksTime.Count < 2) continue;

                var nomes = new List<string>();
                var links = new List<string>();
                foreach (var lt in linksTime)
                {
                    var nome = lt.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(nome))
                        nome = HtmlAgilityPack.HtmlEntity.DeEntitize(lt.InnerText.Trim());
                    var lnk = lt.GetAttributeValue("href", "");
                    if (!string.IsNullOrWhiteSpace(nome) && !nomes.Contains(nome))
                    {
                        nomes.Add(nome);
                        links.Add(lnk.StartsWith("http") ? lnk
                            : "https://www.transfermarkt.com.br" + lnk);
                    }
                    if (nomes.Count == 2) break;
                }

                if (nomes.Count < 2) continue;

                // Data
                DateTime? data = null;
                var tds = bloco.SelectNodes(".//td");
                if (tds != null)
                {
                    foreach (var td in tds)
                    {
                        var dm = Regex.Match(td.InnerText.Trim(), @"\d{2}[./]\d{2}[./]\d{2,4}");
                        if (dm.Success)
                        {
                            data = ParseData(dm.Value);
                            break;
                        }
                    }
                }

                jogos.Add(new TransfermarktJogoInfo
                {
                    NomeTimeCasa = nomes[0],
                    NomeTimeVisitante = nomes[1],
                    LinkTimeCasa = links[0],
                    LinkTimeVisitante = links[1],
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = data,
                    Rodada = rodada,
                    LinkDetalhes = href
                });
            }

            return jogos;
        }

        private static DateTime? ParseData(string texto)
        {
            var m = Regex.Match(texto.Trim(), @"\d{2}[./]\d{2}[./]\d{2,4}");
            if (!m.Success) return null;

            string[] fmts = { "dd/MM/yy", "dd/MM/yyyy", "dd.MM.yyyy", "dd.MM.yy" };
            foreach (var f in fmts)
            {
                if (DateTime.TryParseExact(m.Value, f,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                {
                    // 🔹 Corrige anos de dois dígitos
                    if (dt.Year < 100)
                        dt = new DateTime(2000 + dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);

                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }
            return null;
        }

        private async Task SincronizarCompeticoesTimesJogosEElencos(
            FutebolContext context,
            TransfermarktService transfermarkt,
            CancellationToken ct)
        {
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
                    jogosWeb = await BuscarJogosCopaPorLink(link, ct);
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

                    await IncluirOuAtualizarJogo(context, competicao, jogoWeb,
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


        private async Task IncluirOuAtualizarJogo(
        FutebolContext context,
        Competicao competicao,
        TransfermarktJogoInfo jogoWeb,
        Time timeCasa,
        Time timeVisitante,
        Guid cicloId,
        TransfermarktService transfermarkt, // ← agora recebe o serviço
        CancellationToken ct)
        {
            // ── Resolve data ─────────────────────────────────────────────────────
            DateTime? dataJogo = jogoWeb.Data;

            // Se não veio data do calendário, busca na página de detalhe
            if (!dataJogo.HasValue && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                _logger.LogInformation(
                    "[IncluirJogo] Data não encontrada no calendário, buscando no detalhe: {Link}",
                    jogoWeb.LinkDetalhes);

                dataJogo = await transfermarkt.BuscarDataJogoPorLink(jogoWeb.LinkDetalhes, ct);
            }

            // Fallback: se ainda sem data, usa null (não salva como hoje)
            // Não usar DateTime.UtcNow como fallback

            var jogosBanco = await context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id &&
                            j.TimeCasaId == timeCasa.Id &&
                            j.TimeVisitanteId == timeVisitante.Id)
                .ToListAsync(ct);

            var jogo = dataJogo.HasValue
                ? jogosBanco.FirstOrDefault(j =>
                    j.Data.HasValue &&
                    Math.Abs((j.Data.Value.Date - dataJogo.Value.Date).TotalDays) <= 2)
                : jogosBanco.FirstOrDefault();

            var formacaoCasa = await transfermarkt.ObterOuCriarFormacao(context, jogoWeb.FormacaoCasa, ct);
            var formacaoVisitante = await transfermarkt.ObterOuCriarFormacao(context, jogoWeb.FormacaoVisitante, ct);

            if (jogo == null)
            {
                jogo = new Jogo
                {
                    CompeticaoId = competicao.Id,
                    TimeCasa = timeCasa,
                    TimeVisitante = timeVisitante,
                    Data = dataJogo,
                    Rodada = jogoWeb.Rodada,
                    PlacarCasa = jogoWeb.PlacarCasa,
                    PlacarVisitante = jogoWeb.PlacarVisitante,
                    Grupo = jogoWeb.Grupo,
                    Status = jogoWeb.PlacarCasa.HasValue ? "Finalizado" : "Agendado",
                    Atualizado = jogoWeb.PlacarCasa.HasValue ? 1 : 0,
                    FormacaoCasaId = formacaoCasa.Id,
                    FormacaoVisitanteId = formacaoVisitante.Id,
                    LinkDetalhes = jogoWeb.LinkDetalhes // ← salva o link do jogo
                };

                context.Jogos.Add(jogo);
                transfermarkt.AdicionarEscalacaoComPosicoes(context, jogo, formacaoCasa, true);
                transfermarkt.AdicionarEscalacaoComPosicoes(context, jogo, formacaoVisitante, false);

                _logger.LogInformation(
                    "[IncluirJogo] Incluído: {Casa} x {Vis} em {Data}",
                    timeCasa.Nome, timeVisitante.Nome,
                    dataJogo?.ToString("dd/MM/yyyy HH:mm") ?? "sem data");
                return;
            }

            // Atualiza placar e data se necessário
            bool alterado = false;

            if (jogoWeb.PlacarCasa.HasValue &&
                (jogo.PlacarCasa != jogoWeb.PlacarCasa ||
                 jogo.PlacarVisitante != jogoWeb.PlacarVisitante))
            {
                jogo.PlacarCasa = jogoWeb.PlacarCasa;
                jogo.PlacarVisitante = jogoWeb.PlacarVisitante;
                jogo.Status = "Finalizado";
                jogo.Atualizado = 1;
                alterado = true;
            }

            // Corrige data se estava errada (null ou muito diferente)
            if (dataJogo.HasValue &&
                (!jogo.Data.HasValue ||
                 Math.Abs((jogo.Data.Value.Date - dataJogo.Value.Date).TotalDays) > 2))
            {
                jogo.Data = DateTime.SpecifyKind(dataJogo.Value, DateTimeKind.Utc);
                alterado = true;
            }

            if (alterado)
                _logger.LogInformation(
                    "[IncluirJogo] Atualizado: {Casa} x {Vis}",
                    timeCasa.Nome, timeVisitante.Nome);
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
                    DataNascimento = DateTime.MinValue,
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
                .Where(j => !string.IsNullOrEmpty(j.LinkDetalhes))
                .ToListAsync(ct);

            foreach (var jogo in jogos)
            {
                if (ct.IsCancellationRequested) break;

                var eventos = await transfermarkt.BuscarEventosJogo(jogo.LinkDetalhes, ct);

                foreach (var ev in eventos)
                {
                    Jogador jogador = null;

                    // 🔹 Para gols e cartões, usamos JogadorNome/JogadorLink
                    if (ev.Tipo == "Gol" || ev.Tipo.StartsWith("Cartao"))
                    {
                        jogador = await context.Jogadores
                            .FirstOrDefaultAsync(j =>
                                (!string.IsNullOrEmpty(ev.JogadorLink) && j.linktransfermarket != null && j.linktransfermarket.Contains(ev.JogadorLink)) ||
                                j.Nome == ev.JogadorNome, ct);

                        if (jogador == null) continue;
                    }

                    // 🔹 Para assistências, usamos AssistenteNome/AssistenteLink
                    if (ev.Tipo == "Assistencia")
                    {
                        jogador = await context.Jogadores
                            .FirstOrDefaultAsync(j =>
                                (!string.IsNullOrEmpty(ev.AssistenteLink) && j.linktransfermarket != null && j.linktransfermarket.Contains(ev.AssistenteLink)) ||
                                j.Nome == ev.AssistenteNome, ct);

                        if (jogador == null) continue;
                    }

                    // 🔹 Salva no banco
                    if (ev.Tipo == "Gol")
                    {
                        context.Gols.Add(new Gol
                        {
                            JogoId = jogo.Id,
                            JogadorId = jogador.Id,
                            Minuto = ev.Minuto,
                            Contra = ev.Contra
                        });
                    }
                    else if (ev.Tipo == "Assistencia")
                    {
                        context.Assistencias.Add(new Assistencia
                        {
                            JogoId = jogo.Id,
                            JogadorId = jogador.Id,
                            Minuto = ev.Minuto
                        });
                    }
                    else if (ev.Tipo.StartsWith("Cartao"))
                    {
                        context.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id,
                            JogadorId = jogador.Id,
                            Minuto = ev.Minuto,
                            Tipo = ev.Detalhe
                        });
                    }
                }

                await context.SaveChangesAsync(ct);
                await Task.Delay(1000, ct); // evita bloqueio do site
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
