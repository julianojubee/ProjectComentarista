using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace ControleFutebolWeb.Services
{
    public class TransfermarktPlayerInfo
    {
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? NomeCompleto { get; set; }
        public string? Clube { get; set; }
        public string? Posicao { get; set; }
        public int? NumeroCamisa { get; set; }
        public string? LinkPerfil { get; set; }
    }

    public class TransfermarktEventoInfo
    {
        public string Tipo { get; set; } = string.Empty; // "Gol", "Assistencia", "Cartao"

        public int JogadorId { get; set; }   // jogador envolvido
        public int Minuto { get; set; }      // minuto do evento

        // 🔹 Detalhes adicionais
        public bool Contra { get; set; }     // se foi gol contra
        public int? AssistenteId { get; set; } // jogador que deu a assistência (quando houver)
        public string? Detalhe { get; set; } // tipo de cartão ("Amarelo", "Vermelho")
    }


    public class TransfermarktJogoInfo
    {
        public string NomeTimeCasa { get; set; } = string.Empty;
        public string NomeTimeVisitante { get; set; } = string.Empty;
        public string? LinkTimeCasa { get; set; }
        public string? LinkTimeVisitante { get; set; }
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public DateTime? Data { get; set; }
        public int Rodada { get; set; }
        public string? Grupo { get; set; }
        public string? LinkDetalhes { get; set; }

        // 🔹 Novas propriedades
        public string? FormacaoCasa { get; set; }
        public string? FormacaoVisitante { get; set; }

        public List<TransfermarktEventoInfo> Eventos { get; set; } = new();
    }



    public class TransfermarktService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TransfermarktService> _logger;
        private readonly FutebolContext _context;

        public TransfermarktService(HttpClient httpClient, ILogger<TransfermarktService> logger, FutebolContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;

            // Headers obrigatórios para não receber 403 do Transfermarkt
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.transfermarkt.com.br/");
        }

        public async Task<List<TransfermarktJogoInfo>> BuscarJogosLigaPorLink(
         string linkCompeticao, CancellationToken ct = default)
        {
            var jogos = new List<TransfermarktJogoInfo>();

            // Garante que usa gesamtspielplan (calendário completo)
            var url = linkCompeticao;
            if (!url.Contains("/gesamtspielplan/"))
            {
                url = Regex.Replace(url,
                    @"/(startseite|spieltag|tabelle)/",
                    "/gesamtspielplan/",
                    RegexOptions.IgnoreCase);

                if (!url.Contains("/gesamtspielplan/"))
                    url = url.TrimEnd('/') + "/gesamtspielplan";
            }

            _logger.LogInformation("[Brasileirao] Buscando calendário: {Url}", url);

            string html;
            try
            {
                await Task.Delay(1500, ct); // pausa para não ser bloqueado
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brasileirao] Erro ao buscar calendário: {Url}", url);
                return jogos;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Boxes de rodadas
            var boxes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'box')] | //div[contains(@class,'content-box')]");

            if (boxes == null)
            {
                _logger.LogWarning("[Brasileirao] Nenhum box encontrado.");
                return jogos;
            }

            _logger.LogInformation("[Brasileirao] {N} boxes encontrados.", boxes.Count);

            foreach (var box in boxes)
            {
                // Número da rodada no header
                var header = box.SelectSingleNode(
                    ".//h2 | .//h3 | .//div[contains(@class,'content-box-headline')]");
                if (header == null) continue;

                var headerText = HtmlEntity.DeEntitize(header.InnerText.Trim());
                var rodadaMatch = Regex.Match(headerText, @"\d+");
                int rodada = rodadaMatch.Success ? int.Parse(rodadaMatch.Value) : 0;

                // Linhas de jogos
                var linhasJogo = box.SelectNodes(
                    ".//table[contains(@class,'items')]//tbody/tr[not(contains(@class,'thead'))]");

                if (linhasJogo == null) continue;

                _logger.LogInformation("[Brasileirao] Rodada {Rodada}: {N} linhas de jogo encontradas",
                    rodada, linhasJogo.Count);

                foreach (var linha in linhasJogo)
                {
                    var jogo = ParseLinhaJogoLiga(linha, rodada);
                    if (jogo != null)
                        jogos.Add(jogo);
                }
            }

            _logger.LogInformation("[Brasileirao] Total: {N} jogos ({P} com placar)",
                jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue));

            return jogos;
        }

        private TransfermarktJogoInfo? ParseLinhaJogoLiga(HtmlNode linha, int rodada)
        {
            try
            {
                var cols = linha.SelectNodes(".//td");
                if (cols == null || cols.Count < 4) return null;

                // Data
                DateTime? data = null;
                var dataTexto = cols[0].InnerText.Trim();
                var dm = Regex.Match(dataTexto, @"\d{2}/\d{2}/\d{2,4}");
                if (dm.Success)
                {
                    if (DateTime.TryParseExact(dm.Value,
                        new[] { "dd/MM/yy", "dd/MM/yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                    {
                        data = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }
                }

                // Times
                var linksTime = linha.SelectNodes(".//a[contains(@href,'/verein/')]");
                if (linksTime == null || linksTime.Count < 2) return null;

                var nomeCasa = HtmlEntity.DeEntitize(linksTime[0].GetAttributeValue("title", "").Trim());
                var nomeVisitante = HtmlEntity.DeEntitize(linksTime[1].GetAttributeValue("title", "").Trim());

                // Placar
                int? pc = null, pv = null;
                string linkDetalhes = "";
                var linkPlacar = linha.SelectSingleNode(".//a[contains(@href,'/spielbericht/')]");
                if (linkPlacar != null)
                {
                    linkDetalhes = linkPlacar.GetAttributeValue("href", "");
                    if (!linkDetalhes.StartsWith("http"))
                        linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;

                    var scoreMatch = Regex.Match(HtmlEntity.DeEntitize(linkPlacar.InnerText.Trim()), @"(\d+)\s*[:\-]\s*(\d+)");
                    if (scoreMatch.Success)
                    {
                        pc = int.Parse(scoreMatch.Groups[1].Value);
                        pv = int.Parse(scoreMatch.Groups[2].Value);
                    }
                }

                return new TransfermarktJogoInfo
                {
                    NomeTimeCasa = nomeCasa,
                    NomeTimeVisitante = nomeVisitante,
                    LinkTimeCasa = linksTime[0].GetAttributeValue("href", ""),
                    LinkTimeVisitante = linksTime[1].GetAttributeValue("href", ""),
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = data,
                    Rodada = rodada,
                    LinkDetalhes = linkDetalhes
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Brasileirao] Erro ao parsear linha.");
                return null;
            }
        }

        private static DateTime? ParseDataLiga(string texto)
        {
            string[] fmts = { "dd/MM/yy", "dd/MM/yyyy", "dd.MM.yyyy", "dd.MM.yy" };
            foreach (var f in fmts)
                if (DateTime.TryParseExact(texto.Trim(), f,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }


        public async Task<List<TransfermarktJogoInfo>> BuscarJogosCompeticaoPorLink(string linkCompeticao, CancellationToken ct)
        {
            var jogos = new List<TransfermarktJogoInfo>();

            var html = await _httpClient.GetStringAsync(linkCompeticao, ct);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var jogosNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class,'sb-spieldaten')]");
            if (jogosNodes == null) return jogos;

            foreach (var jogoNode in jogosNodes)
            {
                var jogoInfo = new TransfermarktJogoInfo();

                // 🔹 Times
                var timeCasaNode = jogoNode.SelectSingleNode(".//div[contains(@class,'sb-heim')]");
                var timeVisitanteNode = jogoNode.SelectSingleNode(".//div[contains(@class,'sb-gast')]");

                jogoInfo.NomeTimeCasa = timeCasaNode?.InnerText.Trim() ?? string.Empty;
                jogoInfo.NomeTimeVisitante = timeVisitanteNode?.InnerText.Trim() ?? string.Empty;

                // 🔹 Placar
                var placarNode = jogoNode.SelectSingleNode(".//div[contains(@class,'sb-endstand')]");
                if (placarNode != null)
                {
                    var placarTexto = placarNode.InnerText.Trim();
                    var partes = placarTexto.Split(':');
                    if (partes.Length == 2)
                    {
                        if (int.TryParse(partes[0], out var pc)) jogoInfo.PlacarCasa = pc;
                        if (int.TryParse(partes[1], out var pv)) jogoInfo.PlacarVisitante = pv;
                    }
                }

                // 🔹 Data do jogo
                var dataNode = jogoNode.SelectSingleNode(".//p[@class='sb-datum hide-for-small']");
                if (dataNode != null)
                {
                    var texto = dataNode.InnerText.Trim();
                    texto = Regex.Replace(texto, @"\s+", " ");

                    // Exemplo: "5ª eliminatória - jogos de volta | ter, 12/05/26 | 21:30 | Jogo de ida: 2:2"
                    var partes = texto.Split('|', StringSplitOptions.RemoveEmptyEntries);

                    if (partes.Length >= 3)
                    {
                        var dataStr = partes[1].Trim(); // "ter, 12/05/26"
                        var horaStr = partes[2].Trim(); // "21:30"

                        // Remove prefixo do dia da semana ("ter,", "qua,", etc.)
                        dataStr = Regex.Replace(dataStr, @"^[a-z]{3},\s*", "", RegexOptions.IgnoreCase);

                        if (DateTime.TryParseExact($"{dataStr} {horaStr}",
                            new[] { "dd/MM/yy HH:mm", "dd/MM/yyyy HH:mm" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var dataJogo))
                        {
                            jogoInfo.Data = dataJogo;
                        }
                    }
                }

                jogos.Add(jogoInfo);
            }

            return jogos;
        }

        public async Task<DateTime?> BuscarDataJogoPorLink(
        string linkDetalhes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(linkDetalhes)) return null;

            try
            {
                await Task.Delay(1000, ct);
                var html = await _httpClient.GetStringAsync(linkDetalhes, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Busca o parágrafo sb-datum que contém data e hora
                // Estrutura: "5ª eliminatória | ter, 12/05/26 | 19:30 | ..."
                var datumNode = doc.DocumentNode.SelectSingleNode(
                    "//p[contains(@class,'sb-datum')] | //div[contains(@class,'sb-datum')]");

                if (datumNode == null)
                {
                    _logger.LogWarning("[BuscarData] Node sb-datum não encontrado: {Url}", linkDetalhes);
                    return null;
                }

                var texto = HtmlEntity.DeEntitize(datumNode.InnerText.Trim());
                _logger.LogInformation("[BuscarData] Texto sb-datum: {Texto}", texto);

                // Extrai data no formato dd/MM/yy ou dd/MM/yyyy
                var dataMatch = Regex.Match(texto,
                    @"\b(\d{2}/\d{2}/\d{2,4})\b");

                if (!dataMatch.Success)
                {
                    _logger.LogWarning("[BuscarData] Data não encontrada no texto: {Texto}", texto);
                    return null;
                }

                var dataStr = dataMatch.Groups[1].Value;

                // Extrai hora no formato HH:mm após o segundo pipe
                var horaMatch = Regex.Match(texto,
                    @"\|\s*(\d{2}:\d{2})\s*\|");

                var horaStr = horaMatch.Success ? horaMatch.Groups[1].Value : "00:00";

                _logger.LogInformation("[BuscarData] Data={Data} Hora={Hora}", dataStr, horaStr);

                // Tenta parsear com ano de 2 dígitos (dd/MM/yy) e 4 dígitos (dd/MM/yyyy)
                var formatos = new[]
                {
            "dd/MM/yy HH:mm",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yy",
            "dd/MM/yyyy"
        };

                if (DateTime.TryParseExact(
                    $"{dataStr} {horaStr}",
                    formatos,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
                {
                    // Corrige ano de 2 dígitos: 26 → 2026, não 1926
                    if (dt.Year < 2000)
                        dt = dt.AddYears(2000 - dt.Year +
                            (dt.Year % 100));

                    _logger.LogInformation("[BuscarData] Data parseada: {Data}",
                        dt.ToString("dd/MM/yyyy HH:mm"));

                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }

                _logger.LogWarning("[BuscarData] Falha ao parsear: {Data} {Hora}", dataStr, horaStr);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BuscarData] Erro ao buscar data: {Url}", linkDetalhes);
                return null;
            }
        }

        public async Task<List<TransfermarktPlayerInfo>> BuscarElencoTimePorLink(
            string timeUrl,
            CancellationToken ct = default)
        {
            var jogadores = new List<TransfermarktPlayerInfo>();
            if (string.IsNullOrWhiteSpace(timeUrl)) return jogadores;

            timeUrl = NormalizarUrlElenco(MontarUrlAbsoluta(timeUrl));
            _logger.LogInformation("[Transfermarkt] Buscando elenco do time: {Url}", timeUrl);

            var html = await _httpClient.GetStringAsync(timeUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linhas = doc.DocumentNode.SelectNodes("//table[contains(@class,'items')]/tbody/tr[not(contains(@class,'thead'))]");
            if (linhas == null) return jogadores;

            foreach (var linha in linhas)
            {
                var linkNode = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) continue;

                var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(href)) continue;

                var numeroTexto = linha.SelectSingleNode(".//div[contains(@class,'rn_nummer')]")?.InnerText?.Trim();
                int? numero = int.TryParse(Regex.Match(numeroTexto ?? "", @"\d+").Value, out var n) ? n : null;

                var posicao = linha.SelectSingleNode(".//table[contains(@class,'inline-table')]//tr[2]/td")?.InnerText?.Trim();

                jogadores.Add(new TransfermarktPlayerInfo
                {
                    NomeCompleto = nome,
                    Posicao = ExpandirSiglaPosicaoParaNome(MapearPosicaoTransfermarkt(posicao ?? "")),
                    NumeroCamisa = numero,
                    LinkPerfil = MontarUrlAbsoluta(href)
                });
            }

            return jogadores;
        }

        public string NormalizarLinkTransfermarkt(string url) => MontarUrlAbsoluta(url);


        /// <summary>
        /// Busca foto do jogador. Se tiver linkTransfermarkt salvo, usa direto.
        /// Caso contrário, faz busca genérica pelo nome/clube.
        /// </summary>
        public async Task<string?> BuscarFotoJogador(Jogador jogador)
        {
            try
            {
                if (!string.IsNullOrEmpty(jogador.linktransfermarket))
                {
                    return await BuscarFotoPorLink(jogador.linktransfermarket);
                }

                return await BuscarFotoPorNome(jogador.Nome, jogador.Time?.Nome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao buscar foto de {Nome}", jogador.Nome);
                return null;
            }
        }

        private async Task<string?> BuscarFotoPorLink(string profileUrl)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;

            if (!profileUrl.StartsWith("http"))
                profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

            _logger.LogInformation("[Foto] Acessando perfil direto: {Url}", profileUrl);

            var profileHtml = await _httpClient.GetStringAsync(profileUrl);
            var profileDoc = new HtmlDocument();
            profileDoc.LoadHtml(profileHtml);

            var ogImage = profileDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            var fotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

            if (!string.IsNullOrEmpty(fotoUrl))
            {
                _logger.LogInformation("[Foto] Foto encontrada via link: {Url}", fotoUrl);
                return fotoUrl;
            }

            var imgPerfil = profileDoc.DocumentNode
                .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]");
            return imgPerfil?.GetAttributeValue("src", "")?.Trim();
        }

        private async Task<string?> BuscarFotoPorNome(string nomeJogador, string? nomeClube = null)
        {
            var query = HttpUtility.UrlEncode(nomeJogador);
            var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

            _logger.LogInformation("[Foto] Buscando: {Nome} | Clube: {Clube}", nomeJogador, nomeClube);

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tabela = doc.DocumentNode
                .SelectNodes("//table[contains(@class,'items')]")
                ?.FirstOrDefault();

            if (tabela == null)
            {
                _logger.LogWarning("[Foto] Nenhuma tabela de resultados: {Nome}", nomeJogador);
                return null;
            }

            var linhas = tabela.SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
            if (linhas == null || !linhas.Any()) return null;

            foreach (var l in linhas)
            {
                var nomeCell = ExtrairNomeLinha(l);
                var clubeCell = ExtrairClubeLinha(l);
                _logger.LogInformation("[Foto] Candidato: Nome={Nome} | Clube={Clube}", nomeCell, clubeCell);
            }

            HtmlNode? linhaSelecionada = null;

            if (!string.IsNullOrWhiteSpace(nomeClube))
            {
                linhaSelecionada = linhas.FirstOrDefault(l =>
                    NomesClubeSimilares(ExtrairClubeLinha(l), nomeClube));

                if (linhaSelecionada != null)
                    _logger.LogInformation("[Foto] Clube encontrado (match): {Clube}",
                        ExtrairClubeLinha(linhaSelecionada));
            }

            linhaSelecionada ??= linhas.First();
            _logger.LogInformation("[Foto] Linha selecionada: {Nome} / {Clube}",
                ExtrairNomeLinha(linhaSelecionada), ExtrairClubeLinha(linhaSelecionada));

            return await ExtrairFotoDaLinha(linhaSelecionada);
        }

        public async Task<TransfermarktPlayerInfo?> BuscarJogadorPorLink(string profileUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profileUrl)) return null;

                // Garante que o link é completo
                if (!profileUrl.StartsWith("http"))
                    profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

                _logger.LogInformation("[Transfermarkt] Acessando perfil direto: {Url}", profileUrl);

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                var info = new TransfermarktPlayerInfo();

                // Nome completo
                info.NomeCompleto = profileDoc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")?.InnerText?.Trim()
                    ?? profileDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // Data de nascimento
                var birthNode = profileDoc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                {
                    var texto = birthNode.InnerText.Trim();
                    var partes = texto.Split('(')[0].Trim();
                    info.DataNascimento = ParseDataNascimento(partes);
                }

                // Nacionalidade
                var nacNode = profileDoc.DocumentNode.SelectSingleNode("//span[@class='info-table__content info-table__content--bold']//img");
                if (nacNode != null)
                {
                    var nacRaw = nacNode.GetAttributeValue("title", "");
                    info.Nacionalidade = NacionalidadesHelper.Normalizar(nacRaw);
                }

                // Clube atual
                var clubeNode = profileDoc.DocumentNode.SelectSingleNode("//span[contains(@class,'info-table__content')]");
                if (clubeNode != null)
                    info.Clube = HtmlEntity.DeEntitize(clubeNode.InnerText.Trim());

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro ao acessar perfil direto");
                return null;
            }
        }
        private static string ExtrairNomeLinha(HtmlNode linha)
        {
            // Estrutura: <td class="hauptlink"><a>Nome</a></td>
            var link = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a");
            return HtmlEntity.DeEntitize(link?.InnerText?.Trim() ?? "");
        }

        private static string ExtrairClubeLinha(HtmlNode linha)
        {
            // Estratégia 1: link para /verein/ (o mais confiável)
            var linkVerein = linha.SelectNodes(".//a[contains(@href,'/verein/')]")
                ?.FirstOrDefault();
            if (linkVerein != null)
            {
                // Prefere o atributo title (nome completo do clube)
                var title = linkVerein.GetAttributeValue("title", "").Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    return HtmlEntity.DeEntitize(title);

                var texto = linkVerein.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    return HtmlEntity.DeEntitize(texto);
            }

            // Estratégia 2: segunda célula da inline-table (abaixo do nome)
            var subTds = linha.SelectNodes(".//td[@class='inline-table']//tr");
            if (subTds?.Count >= 2)
            {
                var clubeTexto = subTds[1].InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(clubeTexto))
                    return HtmlEntity.DeEntitize(clubeTexto);
            }

            return "";
        }

        private static bool NomesClubeSimilares(string nomesite, string nomeBanco)
        {
            if (string.IsNullOrWhiteSpace(nomesite) || string.IsNullOrWhiteSpace(nomeBanco))
                return false;

            var a = NormalizarClube(nomesite);
            var b = NormalizarClube(nomeBanco);

            // Match exato após normalização
            if (a == b) return true;

            // Um contém o outro (cobre "Internacional" ↔ "SC Internacional Porto Alegre")
            if (a.Contains(b) || b.Contains(a)) return true;

            // Match parcial: cada token de b aparece em a
            var tokensB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokensB.Length > 0 && tokensB.All(t => a.Contains(t))) return true;

            return false;
        }


        private static string NormalizarClube(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "";

            var s = nome.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                .Replace("ç", "c").Replace("ñ", "n");

            // Remove prefixos/sufixos comuns
            var stopwords = new[] { "sc", "cr", "ec", "fc", "cd", "ca", "ac", "se",
                             "sport", "clube", "club", "futebol", "football",
                             "de", "do", "da", "dos", "las", "los",
                             "porto", "alegre" }; // cidade removida para não interferir

            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => !stopwords.Contains(t) && t.Length > 1)
                          .ToArray();

            return string.Join(" ", tokens);
        }

        /// <summary>Acessa o perfil e extrai a URL da foto via og:image.</summary>
        private async Task<string?> ExtrairFotoDaLinha(HtmlNode linha)
        {
            try
            {
                var linkNode = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Foto] Acessando perfil: {Url}", profileUrl);
                await Task.Delay(TimeSpan.FromSeconds(1.5));

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                // Extrai og:image (mais confiável)
                var ogImage = profileDoc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']");
                var fotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl) && fotoUrl.Contains("transfermarkt"))
                {
                    _logger.LogInformation("[Foto] og:image encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                // Fallback: img de perfil
                var imgPerfil = profileDoc.DocumentNode
                    .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]");
                fotoUrl = imgPerfil?.GetAttributeValue("src", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl))
                {
                    _logger.LogInformation("[Foto] img perfil encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                _logger.LogWarning("[Foto] Nenhuma foto encontrada na página de perfil.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao acessar perfil");
                return null;
            }
        }

        /// <summary>
        /// Busca dados do jogador no Transfermarkt pelo nome.
        /// Quando há múltiplos resultados, compara com o nome do clube para pegar o correto.
        /// </summary>
        public async Task<TransfermarktPlayerInfo?> BuscarJogador(
            string nomeJogador,
            string? nomeClube = null,
            string? nacionalidadeEsperada = null,
            int? idadeEsperada = null)
        {
            var query = HttpUtility.UrlEncode(nomeJogador);
            var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linhas = doc.DocumentNode.SelectNodes("//table[contains(@class,'items')]/tbody/tr[not(contains(@class,'thead'))]");
            if (linhas == null || !linhas.Any()) return null;

            HtmlNode? linhaSelecionada = null;

            foreach (var linha in linhas)
            {
                var nomeCell = linha.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim();
                var clubeCell = ExtrairClubeLinha(linha);

                var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img");
                var nacTexto = nacCell?.GetAttributeValue("title", "") ?? "";
                var nacNormalizada = NacionalidadesHelper.Normalizar(nacTexto);

                var idadeCell = linha.SelectSingleNode(".//td[@class='zentriert']");
                int idade = 0;
                if (idadeCell != null && int.TryParse(Regex.Match(idadeCell.InnerText, @"\d+").Value, out var idadeParsed))
                    idade = idadeParsed;

                _logger.LogInformation("[Transfermarkt] Candidato: Nome={Nome}, Clube={Clube}, Nac={Nac}, Idade={Idade}",
                    nomeCell, clubeCell, nacNormalizada, idade);

                // Critérios de seleção
                if (!string.IsNullOrWhiteSpace(nomeClube) && NomesClubeSimilares(clubeCell, nomeClube))
                {
                    linhaSelecionada = linha;
                    break;
                }
                if (!string.IsNullOrWhiteSpace(nacionalidadeEsperada) &&
                    nacNormalizada.Equals(nacionalidadeEsperada, StringComparison.OrdinalIgnoreCase))
                {
                    if (!idadeEsperada.HasValue || idade == idadeEsperada.Value)
                    {
                        linhaSelecionada = linha;
                        break;
                    }
                }
            }

            // Fallback final
            linhaSelecionada ??= linhas[0];

            return await ExtrairDadosDaLinha(linhaSelecionada);
        }



        private async Task<TransfermarktPlayerInfo?> ExtrairDadosDaLinha(HtmlNode linha)
        {
            try
            {
                // Link do perfil
                var linkNode = linha.SelectSingleNode(".//td[@class='hauptlink']/a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Transfermarkt] Acessando perfil: {Url}", profileUrl);

                await Task.Delay(TimeSpan.FromSeconds(1.5)); // respeita rate limit

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                var info = new TransfermarktPlayerInfo();

                // Nome completo
                info.NomeCompleto = profileDoc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")?.InnerText?.Trim()
                    ?? profileDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // 🔹 Primeiro tenta pegar pelo itemprop birthDate
                var birthNode = profileDoc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                {
                    var texto = birthNode.InnerText.Trim();
                    var partes = texto.Split('(')[0].Trim(); // removes idade entre parênteses
                    var dt = ParseDataNascimento(partes);
                    if (dt.HasValue)
                    {
                        info.DataNascimento = dt;
                    }
                }

                // Extrai dados da tabela (fallback)
                var infoNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--bold']");
                var labelNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--regular']");

                if (infoNodes != null && labelNodes != null)
                {
                    for (int i = 0; i < Math.Min(infoNodes.Count, labelNodes.Count); i++)
                    {
                        var label = labelNodes[i].InnerText.Trim().ToLower();
                        var valor = HtmlEntity.DeEntitize(infoNodes[i].InnerText.Trim());

                        if ((label.Contains("nascimento") || label.Contains("geboren") || label.Contains("date of birth"))
                            && info.DataNascimento == null) // só se não achou antes
                        {
                            info.DataNascimento = ParseDataNascimento(valor);
                            if (info.DataNascimento.HasValue)
                            {
                                if (info.DataNascimento.Value.Year < 1900 ||
                                    info.DataNascimento.Value > DateTime.Today)
                                {
                                    _logger.LogWarning("[Transfermarkt] Data inválida detectada: {Data}", info.DataNascimento);
                                    info.DataNascimento = null;
                                }
                            }
                        }
                        else if (label.Contains("nacionalidade") || label.Contains("nationalität") || label.Contains("nation"))
                        {
                            var imgAlt = infoNodes[i].SelectSingleNode(".//img")
                                ?.GetAttributeValue("title", "")
                                ?? infoNodes[i].SelectSingleNode(".//img")
                                    ?.GetAttributeValue("alt", "");

                            var nomeNacRaw = !string.IsNullOrWhiteSpace(imgAlt) ? imgAlt : valor;
                            info.Nacionalidade = NacionalidadesHelper.Normalizar(nomeNacRaw);
                        }
                        else if (label.Contains("clube") || label.Contains("verein") || label.Contains("club"))
                        {
                            info.Clube = valor;
                        }
                    }
                }

                // Regex fallback (última tentativa)
                if (info.DataNascimento == null)
                {
                    var matchData = Regex.Match(profileHtml, @"(\d{2}/\d{2}/\d{4})");
                    if (matchData.Success)
                        info.DataNascimento = ParseDataNascimento(matchData.Groups[1].Value);
                }

                // Validação final
                if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year < 1900)
                    info.DataNascimento = null;

                _logger.LogInformation(
                    "[Transfermarkt] Perfil escolhido: Nome={Nome}, Clube={Clube}, Nasc={Nasc}, Nac={Nac}",
                    info.NomeCompleto,
                    info.Clube ?? "não informado",
                    info.DataNascimento?.ToString("dd/MM/yyyy") ?? "null",
                    info.Nacionalidade ?? "não informada");

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro ao extrair dados do perfil");
                return null;
            }
        }

        private DateTime? ParseDataNascimento(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;

            // 1. Formato dd/MM/yyyy
            var match = Regex.Match(valor, @"(\d{2}/\d{2}/\d{4})");
            if (match.Success &&
                DateTime.TryParseExact(match.Groups[1].Value, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            // 2. Formato "Nov 18, 1999"
            var matchEn = Regex.Match(valor, @"(\w+ \d{1,2}, \d{4})");
            if (matchEn.Success &&
                DateTime.TryParse(matchEn.Groups[1].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dtEn))
            {
                return dtEn;
            }

            // 3. Fallback genérico
            if (DateTime.TryParse(valor, out var dtAny))
                return dtAny;

            return null;
        }

        /// Verifica se dois nomes de clube são parecidos (ignora maiúsculas, acentos, "FC", "CR" etc.)
        /// </summary>
        private static bool NomesParecidos(string a, string b)
        {
            static string Normalizar(string s) =>
                Regex.Replace(
                    s.ToLowerInvariant()
                     .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                     .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                     .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                     .Replace("ç", "c").Replace("ñ", "n"),
                    @"\b(cr|fc|sc|ec|ac|se|esporte|clube|club|futebol|de|do|da|dos|las|los)\b|\s+",
                    "");

            var na = Normalizar(a);
            var nb = Normalizar(b);

            return na == nb
                || na.Contains(nb)
                || nb.Contains(na);
        }

        private static string MapearPosicaoTransfermarkt(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "MC";

            var s = raw.ToLowerInvariant();

            // Goleiro
            if (s.Contains("gk") || s.Contains("goal") || s.Contains("goleiro") || s.Contains("keeper") || s.Contains("torhüter"))
                return "GL";

            // Zagueiro / defensores / laterais
            if (s.Contains("cb") || s.Contains("center back") || s.Contains("central") || s.Contains("zagueiro") ||
                s.Contains("def") || s.Contains("df") || s.Contains("lb") || s.Contains("rb") ||
                s.Contains("lateral") || s.Contains("wing back") || s.Contains("back"))
                return "ZG";

            // Meia / volantes / meio-campo / ofensivo médio
            if (s.Contains("dm") || s.Contains("cdm") || s.Contains("volante") ||
                s.Contains("cm") || s.Contains("mid") || s.Contains("meia") || s.Contains("am") || s.Contains("cam") || s.Contains("att-mid"))
                return "MC";

            // Atacantes / pontas / centroavante / forwards / striker
            if (s.Contains("fw") || s.Contains("st") || s.Contains("striker") || s.Contains("atacante") ||
                s.Contains("forward") || s.Contains("cf") || s.Contains("lw") || s.Contains("rw") || s.Contains("wing"))
                return "AT";

            // Reservas ou não identificado
            if (s.Contains("sub") || s.Contains("res"))
                return "RES";

            // fallback
            return "MC";
        }

        // helper local dentro da classe TransfermarktService
        private static string ExpandirSiglaPosicaoParaNome(string sigla)
        {
            if (string.IsNullOrWhiteSpace(sigla)) return string.Empty;
            return sigla.ToUpperInvariant() switch
            {
                "GL" => "Goleiro",
                "ZG" => "Zagueiro",
                "MC" => "Meio",
                "AT" => "Atacante",
                "RES" => "Reserva",
                _ => sigla // deixa como veio (pode ser já um nome)
            };
        }

        // Adicione dentro da classe TransfermarktService (ex.: após ExpandirSiglaPosicaoParaNome)
        private static string CleanTeamName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // normaliza espaços e quebras
            var s = Regex.Replace(raw, @"\s+", " ").Replace("\r", "").Replace("\n", "").Trim();

            // remove tokens numéricos grandes que aparecem como ruído (ids, contadores)
            s = Regex.Replace(s, @"\d{2,}", "");

            // normaliza separadores e pontuação
            s = Regex.Replace(s, @"\s*[-–—/\\]\s*", " ");
            s = Regex.Replace(s, @"[,:;·•\u2022]+", " ");

            // remove textos entre parênteses que costumam trazer informação extra
            s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");

            // remove múltiplos espaços remanescentes e trim final
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();

            // retira pontuação inicial/final
            return s.Trim(new char[] { '-', '–', '—', '/', '\\', ',', '.', '(', ')' });
        }

        private static string RemoveNoiseTokens(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return string.Empty;

            var noise = new[]
            {
            "Agenda", "FT", "UCL", "Copa Libertadores", "UEFA Champions League",
            "Copa Sul-Americana", "Copa do Nordeste", "Sudamericano", "CONCACAF Champions Cup",
            "TV", "SC", "SF", "horário", "horario", "min", "′", "’"
             };

            foreach (var n in noise)
                txt = Regex.Replace(txt, Regex.Escape(n), "", RegexOptions.IgnoreCase);

            // remove timestamps e padrões óbvios de ruído
            txt = Regex.Replace(txt, @"\d{1,2}[:h]\d{2}", ""); // 15:00 ou 15h30
            txt = Regex.Replace(txt, @"\b[A-Z]{2,6}\b", "", RegexOptions.IgnoreCase); // tokens tipo TV, UCL
            txt = Regex.Replace(txt, @"\s{2,}", " ").Trim();

            return txt;
        }
      
        private static string MontarUrlAbsoluta(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            url = url.Trim();
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return url;

            if (!url.StartsWith("/"))
                url = "/" + url;

            return "https://www.transfermarkt.com.br" + url;
        }

        private static List<string> MontarUrlsCalendarioCompeticao(string competicaoUrl)
        {
            var urls = new List<string>();
            if (string.IsNullOrWhiteSpace(competicaoUrl)) return urls;

            urls.Add(competicaoUrl);

            var calendario = competicaoUrl;
            if (calendario.Contains("/startseite/", StringComparison.OrdinalIgnoreCase))
                calendario = Regex.Replace(calendario, "/startseite/", "/gesamtspielplan/", RegexOptions.IgnoreCase);
            else if (!calendario.Contains("/gesamtspielplan/", StringComparison.OrdinalIgnoreCase) &&
                     !calendario.Contains("/spieltag/", StringComparison.OrdinalIgnoreCase))
                calendario = calendario.TrimEnd('/') + "/gesamtspielplan";

            if (!urls.Contains(calendario))
                urls.Add(calendario);

            var matchCompeticao = Regex.Match(competicaoUrl, @"pokalwettbewerb/([^/]+)", RegexOptions.IgnoreCase);
            var matchTemporada = Regex.Match(competicaoUrl, @"saison_id/(\d+)", RegexOptions.IgnoreCase);
            if (matchCompeticao.Success && matchTemporada.Success)
            {
                var codigo = matchCompeticao.Groups[1].Value;
                var temporada = matchTemporada.Groups[1].Value;

                for (var rodada = 1; rodada <= 40; rodada++)
                {
                    urls.Add($"https://www.transfermarkt.com.br/-/spieltag/pokalwettbewerb/{codigo}/saison_id/{temporada}/spieltag/{rodada}");
                }
            }

            return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string NormalizarUrlElenco(string timeUrl)
        {
            if (string.IsNullOrWhiteSpace(timeUrl)) return string.Empty;

            if (timeUrl.Contains("/spielplan/", StringComparison.OrdinalIgnoreCase))
                return Regex.Replace(timeUrl, "/spielplan/", "/kader/", RegexOptions.IgnoreCase);

            if (timeUrl.Contains("/startseite/", StringComparison.OrdinalIgnoreCase))
                return Regex.Replace(timeUrl, "/startseite/", "/kader/", RegexOptions.IgnoreCase);

            if (!timeUrl.Contains("/kader/", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(timeUrl, @"/verein/\d+", RegexOptions.IgnoreCase))
            {
                return Regex.Replace(timeUrl, @"(/verein/\d+).*", "$1/kader", RegexOptions.IgnoreCase);
            }

            return timeUrl;
        }

        private List<TransfermarktJogoInfo> ExtrairJogosDeHtml(string html, int rodadaPadrao)
        {
            var jogos = new List<TransfermarktJogoInfo>();
            if (string.IsNullOrWhiteSpace(html)) return jogos;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linksJogo = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/spielbericht/') or contains(@href,'/begegnung_detail/')]");

            if (linksJogo == null) return jogos;

            var processados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var linkJogo in linksJogo)
            {
                var hrefJogo = linkJogo.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(hrefJogo) || !processados.Add(hrefJogo)) continue;

                var bloco = linkJogo.ParentNode;
                for (var i = 0; i < 8 && bloco != null && bloco.Name != "tr"; i++)
                    bloco = bloco.ParentNode;

                bloco ??= linkJogo.ParentNode;
                if (bloco == null) continue;

                var linksTime = bloco.SelectNodes(".//a[contains(@href,'/verein/')]");
                if (linksTime == null || linksTime.Count < 2)
                {
                    var ancestral = bloco.ParentNode;
                    for (var i = 0; i < 4 && ancestral != null && (linksTime == null || linksTime.Count < 2); i++)
                    {
                        linksTime = ancestral.SelectNodes(".//a[contains(@href,'/verein/')]");
                        ancestral = ancestral.ParentNode;
                    }
                }

                if (linksTime == null || linksTime.Count < 2) continue;

                var times = linksTime
                    .Select(l => new
                    {
                        Nome = HtmlEntity.DeEntitize(
                            l.GetAttributeValue("title", "").Trim().Length > 0
                                ? l.GetAttributeValue("title", "").Trim()
                                : l.InnerText.Trim()),
                        Link = MontarUrlAbsoluta(l.GetAttributeValue("href", ""))
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t.Nome) && !t.Link.Contains("/spieler/", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(t => t.Link)
                    .Select(g => g.First())
                    .Take(2)
                    .ToList();

                if (times.Count < 2) continue;

                var textoBloco = HtmlEntity.DeEntitize(bloco.InnerText);
                var scoreMatch = Regex.Match(HtmlEntity.DeEntitize(linkJogo.InnerText), @"(\d+)\s*[:\-]\s*(\d+)");
                if (!scoreMatch.Success)
                    scoreMatch = Regex.Match(textoBloco, @"(\d+)\s*[:\-]\s*(\d+)");

                var dataMatch = Regex.Match(textoBloco, @"\d{1,2}[./]\d{1,2}[./]\d{2,4}");
                var data = dataMatch.Success ? ParseDataTransfermarkt(dataMatch.Value) : null;

                jogos.Add(new TransfermarktJogoInfo
                {
                    NomeTimeCasa = CleanTeamName(times[0].Nome),
                    NomeTimeVisitante = CleanTeamName(times[1].Nome),
                    LinkTimeCasa = times[0].Link,
                    LinkTimeVisitante = times[1].Link,
                    PlacarCasa = scoreMatch.Success ? int.Parse(scoreMatch.Groups[1].Value) : null,
                    PlacarVisitante = scoreMatch.Success ? int.Parse(scoreMatch.Groups[2].Value) : null,
                    Data = data,
                    Rodada = rodadaPadrao,
                    LinkDetalhes = MontarUrlAbsoluta(hrefJogo)
                });
            }

            return jogos;
        }

        private static DateTime? ParseDataTransfermarkt(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var formatos = new[] { "d/M/yy", "dd/MM/yy", "d/M/yyyy", "dd/MM/yyyy", "d.M.yy", "dd.MM.yy", "d.M.yyyy", "dd.MM.yyyy" };
            foreach (var formato in formatos)
            {
                if (DateTime.TryParseExact(texto.Trim(), formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    return DateTime.SpecifyKind(data, DateTimeKind.Utc);
            }

            return DateTime.TryParse(texto, out var dataGenerica)
                ? DateTime.SpecifyKind(dataGenerica, DateTimeKind.Utc)
                : null;
        }
        public async Task<string?> BuscarEscudoTimePorLink(string teamUrl)
        {
            if (string.IsNullOrWhiteSpace(teamUrl)) return null;

            if (!teamUrl.StartsWith("http"))
                teamUrl = "https://www.transfermarkt.com.br" + teamUrl;

            _logger.LogInformation("[Escudo] Buscando escudo do time: {Url}", teamUrl);

            try
            {
                var html = await _httpClient.GetStringAsync(teamUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

              

                // 2. Dentro do bloco data-header__profile-container (página do clube)
                var imgLogo = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'data-header__profile-container')]//img");
                var escudoUrl = imgLogo?.GetAttributeValue("src", "")?.Trim();
                if (!string.IsNullOrEmpty(escudoUrl))
                    return escudoUrl;

                // 3. Fallback: dentro de sb-team (página de jogo)
                var imgGameLogo = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'sb-team')]//img");
                escudoUrl = imgGameLogo?.GetAttributeValue("src", "")?.Trim();
                if (!string.IsNullOrEmpty(escudoUrl))
                    return escudoUrl;

                _logger.LogWarning("[Escudo] Nenhum escudo encontrado para {Url}", teamUrl);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Escudo] Erro ao buscar escudo do time: {Url}", teamUrl);
                return null;
            }
        }



        // Necessário comparer para HashSet de HtmlNode baseado em XPath
        private class HtmlNodeXPathComparer : IEqualityComparer<HtmlAgilityPack.HtmlNode>
        {
            public static HtmlNodeXPathComparer Instance { get; } = new HtmlNodeXPathComparer();

            public bool Equals(HtmlAgilityPack.HtmlNode? x, HtmlAgilityPack.HtmlNode? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return string.Equals(x.XPath, y.XPath, StringComparison.Ordinal);
            }

            public int GetHashCode(HtmlAgilityPack.HtmlNode obj)
            {
                return obj.XPath?.GetHashCode() ?? 0;
            }
        }
    }
}
