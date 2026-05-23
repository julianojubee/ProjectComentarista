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

        // 🔹 Novos campos para mapear jogadores
        public string? JogadorNome { get; set; }   // nome do jogador envolvido
        public string? JogadorLink { get; set; }   // link do perfil no Transfermarkt
        public string? AssistenteNome { get; set; } // nome do jogador que deu a assistência
        public string? AssistenteLink { get; set; } // link do perfil do assistente
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

        public async Task<DateTime?> BuscarDataJogoPorLink(string linkDetalhes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(linkDetalhes)) return null;

            try
            {
                // Pequeno delay para evitar bloqueio
                await Task.Delay(1000, ct);

                var html = await _httpClient.GetStringAsync(linkDetalhes, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Busca o parágrafo sb-datum que contém data e hora
                var datumNode = doc.DocumentNode.SelectSingleNode("//p[contains(@class,'sb-datum')]");
                if (datumNode == null)
                {
                    _logger.LogWarning("[BuscarData] Node sb-datum não encontrado: {Url}", linkDetalhes);
                    return null;
                }

                var texto = HtmlEntity.DeEntitize(datumNode.InnerText.Trim());
                _logger.LogInformation("[BuscarData] Texto sb-datum: {Texto}", texto);

                // Extrai data e hora
                var dataMatch = Regex.Match(texto, @"\d{2}/\d{2}/\d{2,4}");
                var horaMatch = Regex.Match(texto, @"\d{2}:\d{2}");

                if (!dataMatch.Success) return null;

                var dataStr = dataMatch.Value;
                var horaStr = horaMatch.Success ? horaMatch.Value : "00:00";

                _logger.LogInformation("[BuscarData] Data={Data} Hora={Hora}", dataStr, horaStr);

                // Tenta parsear
                var formatos = new[] { "dd/MM/yy HH:mm", "dd/MM/yyyy HH:mm" };
                if (DateTime.TryParseExact($"{dataStr} {horaStr}",
                    formatos,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
                {
                    // Corrige anos de 2 dígitos
                    if (dt.Year < 100)
                        dt = new DateTime(2000 + dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);

                    _logger.LogInformation("[BuscarData] Data parseada: {Data}", dt.ToString("dd/MM/yyyy HH:mm"));
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

        public async Task<Formacao> ObterOuCriarFormacao(FutebolContext context, string? nomeFormacao, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeFormacao))
                nomeFormacao = "4-3-3"; // fallback

            var formacao = await context.Formacoes
                .Include(f => f.Posicoes)
                .FirstOrDefaultAsync(f => f.Nome == nomeFormacao, ct);

            if (formacao == null)
            {
                formacao = new Formacao { Nome = nomeFormacao, Posicoes = new List<PosicaoFormacao>() };
                context.Formacoes.Add(formacao);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[TransfermarktSync] Nova formação criada: {Formacao}", nomeFormacao);
            }

            return formacao;
        }

        public void AdicionarEscalacaoComPosicoes(FutebolContext context, Jogo jogo, Formacao formacao, bool isTimeCasa)
        {
            var posicoes = formacao.Posicoes.OrderBy(p => p.Ordem).ToList();

            foreach (var pos in posicoes)
            {
                context.Escalacoes.Add(new Escalacao
                {
                    Jogo = jogo,
                    IsTimeCasa = isTimeCasa,
                    Titular = true,
                    Posicao = pos.NomePosicao,
                    PosicaoX = pos.PosicaoX,
                    PosicaoY = pos.PosicaoY,
                    FaseEscalacao = "INICIAL"
                });
            }
        }
        

        private TransfermarktJogoInfo? ParseLinhaJogoLiga(HtmlNode linha, int rodada)
        {
            try
            {
                // Placar
                var placarNode = linha.SelectSingleNode(".//span[contains(@class,'matchresult')]");
                if (placarNode == null) return null;

                int? pc = null, pv = null;
                var scoreText = placarNode.InnerText.Trim();
                var scoreMatch = Regex.Match(scoreText, @"(\d+)\s*[:\-]\s*(\d+)");
                if (scoreMatch.Success)
                {
                    pc = int.Parse(scoreMatch.Groups[1].Value);
                    pv = int.Parse(scoreMatch.Groups[2].Value);
                }

                // Times
                var linksTime = linha.SelectNodes(".//a[contains(@href,'/verein/')]");
                if (linksTime == null || linksTime.Count < 2) return null;

                var nomeCasa = HtmlEntity.DeEntitize(linksTime[0].GetAttributeValue("title", "").Trim());
                var nomeVisitante = HtmlEntity.DeEntitize(linksTime[1].GetAttributeValue("title", "").Trim());

                // Link detalhes
                var linkDetalhes = linha.SelectSingleNode(".//a[contains(@href,'/spielbericht/')]")
                    ?.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(linkDetalhes) && !linkDetalhes.StartsWith("http"))
                    linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;

                // Data → procurar no próximo <tr>
                DateTime? data = null;
                var proximoTr = linha.NextSibling;
                while (proximoTr != null && proximoTr.Name != "tr")
                    proximoTr = proximoTr.NextSibling;

                if (proximoTr != null)
                {
                    var textoData = HtmlEntity.DeEntitize(proximoTr.InnerText.Trim());
                    var dm = Regex.Match(textoData, @"\d{2}/\d{2}/\d{2,4}");
                    var hm = Regex.Match(textoData, @"\d{2}:\d{2}");
                    if (dm.Success)
                    {
                        var dataStr = dm.Value;
                        var horaStr = hm.Success ? hm.Value : "00:00";

                        if (DateTime.TryParseExact($"{dataStr} {horaStr}",
                            new[] { "dd/MM/yy HH:mm", "dd/MM/yyyy HH:mm" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var dt))
                        {
                            // 🔹 Corrige anos de dois dígitos
                            if (dt.Year < 100)
                                dt = new DateTime(2000 + dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);

                            data = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        }
                    }
                }

                var jogo = new TransfermarktJogoInfo
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

                _logger.LogInformation("[Brasileirao] Jogo extraído: {Casa} x {Vis} em {Data} ({Pc}-{Pv})",
                    jogo.NomeTimeCasa, jogo.NomeTimeVisitante,
                    data?.ToString("dd/MM/yyyy HH:mm") ?? "sem data",
                    pc ?? -1, pv ?? -1);

                return jogo;
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
            {
                if (DateTime.TryParseExact(texto.Trim(), f,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                {
                    // Corrige anos de 2 dígitos
                    if (dt.Year < 100)
                        dt = new DateTime(2000 + dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);

                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }
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

        public async Task<List<TransfermarktJogoInfo>> BuscarJogosLigaPorLink(
    string linkCompeticao, CancellationToken ct = default)
        {
            var jogos = new List<TransfermarktJogoInfo>();

            var url = linkCompeticao;
            if (!url.Contains("/gesamtspielplan/"))
                url = url.TrimEnd('/') + "/gesamtspielplan";

            _logger.LogInformation("[Brasileirao] Buscando calendário: {Url}", url);

            string html;
            try
            {
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Brasileirao] Erro ao buscar calendário: {Url}", url);
                return jogos;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var boxes = doc.DocumentNode.SelectNodes("//div[contains(@class,'box')]");
            if (boxes == null)
            {
                _logger.LogWarning("[Brasileirao] Nenhum box encontrado.");
                return jogos;
            }

            foreach (var box in boxes)
            {
                var header = box.SelectSingleNode(".//div[contains(@class,'content-box-headline')]");
                var headerText = HtmlEntity.DeEntitize(header?.InnerText.Trim() ?? "");
                var rodadaMatch = Regex.Match(headerText, @"\d+");
                int rodada = rodadaMatch.Success ? int.Parse(rodadaMatch.Value) : 0;

                var linhas = box.SelectNodes(".//table//tbody/tr");
                if (linhas == null) continue;

                string ultimaDataStr = "";
                string ultimaHoraStr = "";

                int countRodada = 0;
                foreach (var linha in linhas)
                {
                    try
                    {
                        var classe = linha.GetAttributeValue("class", "");

                        // Atualiza cache de data/hora quando encontra <tr class="bg_blau_20">
                        if (classe.Contains("bg_blau_20"))
                        {
                            var textoData = HtmlEntity.DeEntitize(linha.InnerText.Trim());
                            ultimaDataStr = Regex.Match(textoData, @"\d{2}/\d{2}/\d{2,4}").Value;
                            ultimaHoraStr = Regex.Match(textoData, @"\d{2}:\d{2}").Value;
                            continue;
                        }

                        // Times (apenas os links de nome, não os de escudo)
                        var linksTime = linha.SelectNodes(".//td[contains(@class,'hauptlink')]/a[contains(@href,'/verein/')]");
                        if (linksTime == null || linksTime.Count < 2) continue;

                        string nomeCasa = HtmlEntity.DeEntitize(linksTime[0].GetAttributeValue("title", "").Trim());
                        string nomeVisitante = HtmlEntity.DeEntitize(linksTime[1].GetAttributeValue("title", "").Trim());

                        string linkCasa = linksTime[0].GetAttributeValue("href", "");
                        string linkVisitante = linksTime[1].GetAttributeValue("href", "");

                        // Placar
                        int? pc = null, pv = null;
                        var placarNode = linha.SelectSingleNode(".//a[contains(@class,'ergebnis-link')]");
                        string linkDetalhes = "";
                        if (placarNode != null)
                        {
                            linkDetalhes = placarNode.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(linkDetalhes) && !linkDetalhes.StartsWith("http"))
                                linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;

                            var scoreText = HtmlEntity.DeEntitize(placarNode.InnerText.Trim());
                            var scoreMatch = Regex.Match(scoreText, @"(\d+)\s*[:\-]\s*(\d+)");
                            if (scoreMatch.Success)
                            {
                                pc = int.Parse(scoreMatch.Groups[1].Value);
                                pv = int.Parse(scoreMatch.Groups[2].Value);
                            }
                        }

                        // Data e hora → usa cache da última linha bg_blau_20
                        DateTime? data = null;
                        if (!string.IsNullOrEmpty(ultimaDataStr))
                        {
                            var horaStr = !string.IsNullOrEmpty(ultimaHoraStr) ? ultimaHoraStr : "00:00";
                            if (DateTime.TryParseExact($"{ultimaDataStr} {horaStr}",
                                new[] { "dd/MM/yy HH:mm", "dd/MM/yyyy HH:mm" },
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var dt))
                            {
                                data = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                            }
                        }

                        jogos.Add(new TransfermarktJogoInfo
                        {
                            NomeTimeCasa = nomeCasa,
                            NomeTimeVisitante = nomeVisitante,
                            LinkTimeCasa = linkCasa,
                            LinkTimeVisitante = linkVisitante,
                            PlacarCasa = pc,
                            PlacarVisitante = pv,
                            Data = data,
                            Rodada = rodada,
                            LinkDetalhes = linkDetalhes
                        });

                        countRodada++;
                        _logger.LogInformation("[Brasileirao] Jogo extraído: {Casa} x {Vis} em {Data} ({Pc}-{Pv})",
                            nomeCasa, nomeVisitante,
                            data?.ToString("dd/MM/yyyy HH:mm") ?? "sem data",
                            pc ?? -1, pv ?? -1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Brasileirao] Erro ao parsear linha.");
                    }
                }

                if (countRodada > 0)
                    _logger.LogInformation("[Brasileirao] Rodada {Rodada}: {N} jogos extraídos.", rodada, countRodada);
            }

            _logger.LogInformation("[Brasileirao] Total: {N} jogos ({P} com placar)",
                jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue));

            return jogos;
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

        public async Task<DetalhesJogoTM?> BuscarDetalhesJogoAsync(
    string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            if (!url.StartsWith("http"))
                url = "https://www.transfermarkt.com.br" + url;

            string html;
            try
            {
                await Task.Delay(2000, ct);
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Falha ao buscar detalhes: {Url}", url);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var detalhes = new DetalhesJogoTM();

            // ── Placar ────────────────────────────────────────────────────────────
            var placarNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'sb-endstand')]");
            if (placarNode != null)
            {
                var m = Regex.Match(placarNode.InnerText.Trim(), @"(\d+)\s*:\s*(\d+)");
                if (m.Success)
                {
                    detalhes.PlacarCasa = int.Parse(m.Groups[1].Value);
                    detalhes.PlacarVisitante = int.Parse(m.Groups[2].Value);
                }
            }

            // ── Formações ─────────────────────────────────────────────────────────
            // Exemplo do HTML: <div class="... formation-subtitle">Onze inicial: 4-4-2</div>
            var formNodes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'formation-subtitle')]");
            if (formNodes != null && formNodes.Count >= 1)
            {
                detalhes.FormacaoCasa = Regex.Match(
                    formNodes[0].InnerText.Trim(), @"\d-\d-\d(-\d)?").Value;
            }
            if (formNodes != null && formNodes.Count >= 2)
            {
                detalhes.FormacaoVisitante = Regex.Match(
                    formNodes[1].InnerText.Trim(), @"\d-\d-\d(-\d)?").Value;
            }

            // ── Escalações ────────────────────────────────────────────────────────
            // Titulares ficam nos containers .aufstellung-box (um por time)
            // Banco fica em table.ersatzbank (uma por time)
            var aufstellungBoxes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'aufstellung-box')]");

            if (aufstellungBoxes != null && aufstellungBoxes.Count >= 1)
            {
                detalhes.EscalacaoInicialCasa = ExtrairTitulares(aufstellungBoxes[0]);
                detalhes.EscalacaoInicialCasa.AddRange(ExtrairBanco(aufstellungBoxes[0]));
            }
            if (aufstellungBoxes != null && aufstellungBoxes.Count >= 2)
            {
                detalhes.EscalacaoInicialVisitante = ExtrairTitulares(aufstellungBoxes[1]);
                detalhes.EscalacaoInicialVisitante.AddRange(ExtrairBanco(aufstellungBoxes[1]));
            }

            // Copia para Final (o site não separa inicial/final explicitamente)
            detalhes.EscalacaoFinalCasa = detalhes.EscalacaoInicialCasa
                .Select(j => j with { Fase = "FINAL" }).ToList();
            detalhes.EscalacaoFinalVisitante = detalhes.EscalacaoInicialVisitante
                .Select(j => j with { Fase = "FINAL" }).ToList();

            // ── Gols ──────────────────────────────────────────────────────────────
            // <div id="sb-tore">  →  <li class="sb-aktion-heim"> ou "sb-aktion-gast">
            detalhes.Gols = ExtrairGolsDetalhado(doc);

            // ── Eventos (gols, assistências, cartões) ─────────────────────────────
            detalhes.Eventos = new List<TransfermarktEventoInfo>();
            detalhes.Eventos.AddRange(ExtrairEventosGols(doc));
            detalhes.Eventos.AddRange(ExtrairEventosCartoes(doc));

            _logger.LogInformation(
                "[Transfermarkt] Detalhes extraídos — " +
                "FormCasa={FC} FormVis={FV} " +
                "TitCasa={TC} TitVis={TV} " +
                "Gols={G} Eventos={E}",
                detalhes.FormacaoCasa ?? "-",
                detalhes.FormacaoVisitante ?? "-",
                detalhes.EscalacaoInicialCasa.Count(j => j.Titular),
                detalhes.EscalacaoInicialVisitante.Count(j => j.Titular),
                detalhes.Gols.Count,
                detalhes.Eventos.Count);

            return detalhes;
        }

        // ── Extrai titulares a partir do bloco aufstellung-box ─────────────────────
        // Os jogadores ficam em <div class="formation-player-container">
        // com um link <a href="/slug/profil/spieler/ID">
        private List<JogadorEscalacaoTM> ExtrairTitulares(HtmlNode blocoBox)
        {
            var lista = new List<JogadorEscalacaoTM>();

            var containers = blocoBox.SelectNodes(
                ".//div[contains(@class,'formation-player-container')]");
            if (containers == null) return lista;

            foreach (var c in containers)
            {
                var linkNode = c.SelectSingleNode(
                    ".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) continue;

                var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                var href = linkNode.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href, @"/spieler/(\d+)");

                // Número da camisa
                var numNode = c.SelectSingleNode(
                    ".//div[contains(@class,'tm-shirt-number')]");
                int? numero = null;
                if (numNode != null && int.TryParse(numNode.InnerText.Trim(), out var n))
                    numero = n;

                lista.Add(new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = InferirPosicaoPorNumero(numero),
                    Titular = true,
                    IdExterno = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : null,
                    Fase = "INICIAL"
                });
            }

            return lista;
        }

        // ── Extrai banco de reservas a partir do bloco aufstellung-box ─────────────
        // Os reservas ficam em <table class="ersatzbank">
        // Cada <tr> tem: número | link jogador | sigla posição
        private List<JogadorEscalacaoTM> ExtrairBanco(HtmlNode blocoBox)
        {
            var lista = new List<JogadorEscalacaoTM>();

            var tabela = blocoBox.SelectSingleNode(".//table[contains(@class,'ersatzbank')]");
            if (tabela == null) return lista;

            var linhas = tabela.SelectNodes(".//tr");
            if (linhas == null) return lista;

            foreach (var linha in linhas)
            {
                var linkNode = linha.SelectSingleNode(
                    ".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) continue;

                var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                var href = linkNode.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href, @"/spieler/(\d+)");

                // Número da camisa
                var numNode = linha.SelectSingleNode(
                    ".//div[contains(@class,'tm-shirt-number')]");
                int? numero = null;
                if (numNode != null && int.TryParse(numNode.InnerText.Trim(), out var n))
                    numero = n;

                // Posição (3ª célula, ex: "GOL", "ZAG", "MEI", "CA")
                var tds = linha.SelectNodes(".//td");
                string sigla = "";
                if (tds != null && tds.Count >= 3)
                    sigla = HtmlEntity.DeEntitize(tds[2].InnerText.Trim());

                lista.Add(new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = ExpandirSiglaPosicaoBR(sigla),
                    Titular = false,
                    IdExterno = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : null,
                    Fase = "INICIAL"
                });
            }

            return lista;
        }

        // ── Extrai gols do bloco #sb-tore ──────────────────────────────────────────
        // <li class="sb-aktion-heim"> ou "sb-aktion-gast">
        // O link do marcador está em <div class="sb-aktion-spielerbild"> → <a>
        private List<GolTM> ExtrairGolsDetalhado(HtmlDocument doc)
        {
            var gols = new List<GolTM>();

            var toreDiv = doc.DocumentNode.SelectSingleNode("//*[@id='sb-tore']");
            if (toreDiv == null) return gols;

            var items = toreDiv.SelectNodes(".//li[contains(@class,'sb-aktion')]");
            if (items == null) return gols;

            foreach (var li in items)
            {
                bool isCasa = li.GetAttributeValue("class", "").Contains("sb-aktion-heim");

                // Minuto
                var uhrSpan = li.SelectSingleNode(".//span[contains(@class,'sb-sprite-uhr-klein')]");
                string uhrText = uhrSpan?.InnerText.Trim() ?? "0";
                int minuto = ParseMinuto(uhrText);

                // Marcador (primeiro link em sb-aktion-spielerbild)
                var bilderLink = li.SelectSingleNode(
                    ".//div[contains(@class,'sb-aktion-spielerbild')]//a[contains(@href,'/profil/spieler/')]");
                if (bilderLink == null) continue;

                var nomeGolador = bilderLink.GetAttributeValue("title", "").Trim();
                if (string.IsNullOrWhiteSpace(nomeGolador))
                    nomeGolador = HtmlEntity.DeEntitize(bilderLink.InnerText.Trim());

                var hrefGolador = bilderLink.GetAttributeValue("href", "");
                var idGolador = ExtrairIdJogadorDoLink(hrefGolador);

                // Gol contra?
                var acaoDiv = li.SelectSingleNode(".//div[contains(@class,'sb-aktion-aktion')]");
                bool contra = acaoDiv != null &&
                    (acaoDiv.InnerText.Contains("contra") || acaoDiv.InnerText.Contains("Eigentor"));

                gols.Add(new GolTM
                {
                    NomeJogador = nomeGolador,
                    IdExterno = idGolador,
                    Minuto = minuto,
                    IsTimeCasa = isCasa,
                    Contra = contra
                });
            }

            return gols;
        }

        // ── Extrai eventos de gols/assistências do #sb-tore ────────────────────────
        private List<TransfermarktEventoInfo> ExtrairEventosGols(HtmlDocument doc)
        {
            var eventos = new List<TransfermarktEventoInfo>();

            var toreDiv = doc.DocumentNode.SelectSingleNode("//*[@id='sb-tore']");
            if (toreDiv == null) return eventos;

            var items = toreDiv.SelectNodes(".//li[contains(@class,'sb-aktion')]");
            if (items == null) return eventos;

            foreach (var li in items)
            {
                var uhrSpan = li.SelectSingleNode(".//span[contains(@class,'sb-sprite-uhr-klein')]");
                int minuto = ParseMinuto(uhrSpan?.InnerText.Trim() ?? "0");

                var acaoDiv = li.SelectSingleNode(".//div[contains(@class,'sb-aktion-aktion')]");
                if (acaoDiv == null) continue;

                // Todos os links de jogadores na linha de ação
                var links = acaoDiv.SelectNodes(".//a[contains(@href,'/profil/spieler/') or contains(@href,'/leistungsdaten')]");
                if (links == null || links.Count == 0) continue;

                // 1° link = marcador
                var linkMarcador = links[0];
                string nomeMarcador = linkMarcador.GetAttributeValue("title", "").Trim();
                if (string.IsNullOrWhiteSpace(nomeMarcador))
                    nomeMarcador = HtmlEntity.DeEntitize(linkMarcador.InnerText.Trim());
                string hrefMarcador = linkMarcador.GetAttributeValue("href", "");
                long? idMarcador = ExtrairIdJogadorDoLink(hrefMarcador);

                bool contra = acaoDiv.InnerText.Contains("contra") ||
                              acaoDiv.InnerText.Contains("Eigentor");

                eventos.Add(new TransfermarktEventoInfo
                {
                    Tipo = "Gol",
                    JogadorNome = nomeMarcador,
                    JogadorLink = hrefMarcador,
                    Minuto = minuto,
                    Contra = contra,
                    Detalhe = "Gol"
                });

                // Se existe "Assistência:" no texto, 2° link = assistente
                if (links.Count >= 2 && acaoDiv.InnerText.Contains("Assistência"))
                {
                    var linkAssist = links[1];
                    string nomeAssist = linkAssist.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(nomeAssist))
                        nomeAssist = HtmlEntity.DeEntitize(linkAssist.InnerText.Trim());
                    string hrefAssist = linkAssist.GetAttributeValue("href", "");

                    eventos.Add(new TransfermarktEventoInfo
                    {
                        Tipo = "Assistencia",
                        AssistenteNome = nomeAssist,
                        AssistenteLink = hrefAssist,
                        Minuto = minuto,
                        Detalhe = "Assistência"
                    });
                }
            }

            return eventos;
        }

        // ── Extrai cartões do bloco #sb-karten ────────────────────────────────────
        // <div class="sb-aktion-spielstand"> → <span class="sb-sprite sb-gelb"> ou sb-rot
        private List<TransfermarktEventoInfo> ExtrairEventosCartoes(HtmlDocument doc)
        {
            var eventos = new List<TransfermarktEventoInfo>();

            var kartDiv = doc.DocumentNode.SelectSingleNode("//*[@id='sb-karten']");
            if (kartDiv == null) return eventos;

            var items = kartDiv.SelectNodes(".//li[contains(@class,'sb-aktion')]");
            if (items == null) return eventos;

            foreach (var li in items)
            {
                var uhrSpan = li.SelectSingleNode(".//span[contains(@class,'sb-sprite-uhr-klein')]");
                int minuto = ParseMinuto(uhrSpan?.InnerText.Trim() ?? "0");

                // Tipo de cartão
                var cartaoSpan = li.SelectSingleNode(
                    ".//span[contains(@class,'sb-gelb') or contains(@class,'sb-rot')]");
                string tipo = "Amarelo";
                if (cartaoSpan != null)
                {
                    var cls = cartaoSpan.GetAttributeValue("class", "");
                    tipo = cls.Contains("sb-rot") ? "Vermelho" : "Amarelo";
                }

                // Jogador
                var bilderLink = li.SelectSingleNode(
                    ".//div[contains(@class,'sb-aktion-spielerbild')]//a[contains(@href,'/profil/spieler/')]");
                if (bilderLink == null) continue;

                string nomeJogador = bilderLink.GetAttributeValue("title", "").Trim();
                if (string.IsNullOrWhiteSpace(nomeJogador))
                    nomeJogador = HtmlEntity.DeEntitize(bilderLink.InnerText.Trim());
                string hrefJogador = bilderLink.GetAttributeValue("href", "");

                eventos.Add(new TransfermarktEventoInfo
                {
                    Tipo = tipo == "Vermelho" ? "CartaoVermelho" : "CartaoAmarelo",
                    JogadorNome = nomeJogador,
                    JogadorLink = hrefJogador,
                    Minuto = minuto,
                    Detalhe = tipo
                });
            }

            return eventos;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static int ParseMinuto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0;
            var m = Regex.Match(texto.Trim(), @"(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static long? ExtrairIdJogadorDoLink(string href)
        {
            var m = Regex.Match(href ?? "", @"/spieler/(\d+)");
            return m.Success ? long.Parse(m.Groups[1].Value) : null;
        }

        // Converte sigla BR do banco de reservas para nome de posição
        private static string ExpandirSiglaPosicaoBR(string sigla) =>
            sigla.ToUpperInvariant() switch
            {
                "GOL" => "Goleiro",
                "ZAG" => "Zagueiro",
                "LD" => "Lateral Direito",
                "LE" => "Lateral Esquerdo",
                "VOL" => "Volante",
                "MEI" => "Meio-campo",
                "PE" => "Ponta Esquerda",
                "PD" => "Ponta Direita",
                "CA" => "Centroavante",
                "ATA" => "Atacante",
                _ => "Meio-campo"
            };

        // Infere posição pela faixa numérica do goleiro (1 = Goleiro)
        private static string InferirPosicaoPorNumero(int? numero) =>
            numero switch
            {
                1 => "Goleiro",
                2 or 3 or 4 or 5 or 6 => "Zagueiro",
                7 or 8 or 9 or 10 or 11 => "Meio-campo",
                _ => "Meio-campo"
            };
        private List<GolTM> ExtrairGols(HtmlDocument doc)
        {
            var gols = new List<GolTM>();

            var golNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'sb-aktion')]");
            if (golNodes == null) return gols;

            foreach (var node in golNodes)
            {
                // Só processa se for gol
                if (!node.InnerText.Contains("Gol")) continue;

                // Minuto
                var minutoMatch = Regex.Match(node.InnerText, @"(\d+)'");
                int minuto = minutoMatch.Success ? int.Parse(minutoMatch.Groups[1].Value) : 0;

                // Jogador
                var jogadorNode = node.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                var jogadorNome = HtmlEntity.DeEntitize(jogadorNode?.InnerText.Trim() ?? "");
                var jogadorLink = jogadorNode?.GetAttributeValue("href", "");
                var jogadorId = ExtrairIdDoLink(jogadorLink ?? "");

                // Gol contra
                bool contra = node.InnerText.Contains("Gol contra");

                // Pênalti
                bool penalti = node.InnerText.Contains("pênalti") || node.InnerText.Contains("Pen.");

                // Prorrogação
                bool prorroga = node.InnerText.Contains("ET") || node.InnerText.Contains("Prorrogação");

                gols.Add(new GolTM
                {
                    NomeJogador = jogadorNome,
                    IdExterno = jogadorId,
                    Minuto = minuto,
                    Contra = contra,
                    IsTimeCasa = DetectarSeGolDoTimeCasa(node)
                });
            }

            return gols;
        }

        private bool DetectarSeGolDoTimeCasa(HtmlNode node)
        {
            // Heurística: Transfermarkt marca gols do time da casa à esquerda
            var parentClass = node.GetAttributeValue("class", "");
            return parentClass.Contains("sb-aktion-heim");
        }


        private List<JogadorEscalacaoTM> ExtrairEscalacao(HtmlDocument doc, int lado, string fase)
        {
            var lista = new List<JogadorEscalacaoTM>();

            // lado: 0 = casa, 1 = visitante
            var blocos = doc.DocumentNode.SelectNodes("//div[contains(@class,'aufstellung')]");
            if (blocos == null || blocos.Count <= lado) return lista;

            var bloco = blocos[lado];

            // 🔹 Titulares
            var titularesNodes = bloco.SelectNodes(".//div[contains(@class,'aufstellung-spieler')]//tr");
            if (titularesNodes != null)
            {
                foreach (var tr in titularesNodes)
                {
                    var linkNode = tr.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                    if (linkNode == null) continue;

                    var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                    var href = linkNode.GetAttributeValue("href", "");
                    var numeroNode = tr.SelectSingleNode(".//div[contains(@class,'rn_nummer')]");
                    int? numero = int.TryParse(numeroNode?.InnerText.Trim(), out var n) ? n : (int?)null;

                    var posicaoNode = tr.SelectSingleNode(".//td[contains(@class,'pos')]") ??
                                      tr.SelectSingleNode(".//span[contains(@class,'pos')]");
                    var posicao = HtmlEntity.DeEntitize(posicaoNode?.InnerText.Trim() ?? "");

                    lista.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Numero = numero,
                        Posicao = posicao,
                        Titular = true,
                        Fase = fase
                    });
                }
            }

            // 🔹 Reservas (banco de cada lado)
            var reservasNodes = bloco.SelectNodes(".//div[contains(@class,'ersatzbank')]//tr");
            if (reservasNodes != null)
            {
                foreach (var tr in reservasNodes)
                {
                    var linkNode = tr.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                    if (linkNode == null) continue;

                    var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                    var href = linkNode.GetAttributeValue("href", "");
                    var numeroNode = tr.SelectSingleNode(".//div[contains(@class,'rn_nummer')]");
                    int? numero = int.TryParse(numeroNode?.InnerText.Trim(), out var n) ? n : (int?)null;

                    var posicaoNode = tr.SelectSingleNode(".//td[contains(@class,'pos')]") ??
                                      tr.SelectSingleNode(".//span[contains(@class,'pos')]");
                    var posicao = HtmlEntity.DeEntitize(posicaoNode?.InnerText.Trim() ?? "");

                    lista.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Numero = numero,
                        Posicao = posicao,
                        Titular = false,
                        Fase = fase
                    });
                }
            }

            // 🔹 Substituições (quando aparecem)
            var subsNodes = bloco.SelectNodes(".//div[contains(@class,'sb-wechsel')]//tr");
            if (subsNodes != null)
            {
                foreach (var tr in subsNodes)
                {
                    var linkNode = tr.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                    if (linkNode == null) continue;

                    var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                    var href = linkNode.GetAttributeValue("href", "");

                    lista.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Posicao = "Substituto",
                        Titular = false,
                        Fase = fase
                    });
                }
            }

            return lista;
        }

        private List<JogadorEscalacaoTM> ExtrairEscalacaoFinal(HtmlDocument doc, int lado)
        {
            var finalNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'aufstellung-endaufstellung')]");
            if (finalNodes == null || finalNodes.Count <= lado)
                return ExtrairEscalacao(doc, lado, "FINAL");

            var bloco = finalNodes[lado];
            var lista = new List<JogadorEscalacaoTM>();

            var jogadoresNodes = bloco.SelectNodes(".//a[contains(@href,'/profil/spieler/')]");
            if (jogadoresNodes != null)
            {
                foreach (var node in jogadoresNodes)
                {
                    var nome = HtmlEntity.DeEntitize(node.InnerText.Trim());
                    var href = node.GetAttributeValue("href", "");

                    lista.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Posicao = "Final",
                        Titular = true,
                        Fase = "FINAL"
                    });
                }
            }

            return lista;
        }

        private long? ExtrairIdDoLink(string link)
        {
            var match = Regex.Match(link ?? "", @"/spieler/(\d+)");
            if (match.Success)
                return long.Parse(match.Groups[1].Value);
            return null;
        }

        public async Task<DetalhesJogoTM?> ParseDetalhesJogo(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!url.StartsWith("http"))
                url = "https://www.transfermarkt.com.br" + url;

            string html;
            try
            {
                await Task.Delay(2000, ct);
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseDetalhesJogo] Erro ao buscar detalhes: {Url}", url);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var detalhes = new DetalhesJogoTM();

            // ── Formação ─────────────────────────────────────────────
            var formNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'formation-subtitle')]");
            if (formNodes != null && formNodes.Count >= 2)
            {
                detalhes.FormacaoCasa = Regex.Match(formNodes[0].InnerText, @"\d-\d-\d(-\d)?").Value;
                detalhes.FormacaoVisitante = Regex.Match(formNodes[1].InnerText, @"\d-\d-\d(-\d)?").Value;
            }

            // ── Titulares (apenas nome/link/número) ──────────────────
            var titularesNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'formation-player-container')]");
            if (titularesNodes != null)
            {
                foreach (var node in titularesNodes)
                {
                    var nomeNode = node.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                    var nome = HtmlEntity.DeEntitize(nomeNode?.InnerText.Trim() ?? "");
                    var href = nomeNode?.GetAttributeValue("href", "");
                    var numeroNode = node.SelectSingleNode(".//div[contains(@class,'tm-shirt-number')]");
                    int? numero = int.TryParse(numeroNode?.InnerText.Trim(), out var n) ? n : (int?)null;

                    detalhes.EscalacaoInicialCasa.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Numero = numero,
                        Titular = true,
                        Fase = "INICIAL"
                    });
                }
            }

            // ── Reservas ─────────────────────────────────────────────
            var reservasNodes = doc.DocumentNode.SelectNodes("//table[@class='ersatzbank']/tr");
            if (reservasNodes != null)
            {
                foreach (var r in reservasNodes)
                {
                    var nomeNode = r.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                    var nome = HtmlEntity.DeEntitize(nomeNode?.InnerText.Trim() ?? "");
                    var href = nomeNode?.GetAttributeValue("href", "");
                    var numeroNode = r.SelectSingleNode(".//div[contains(@class,'tm-shirt-number')]");
                    int? numero = int.TryParse(numeroNode?.InnerText.Trim(), out var n) ? n : (int?)null;
                    var posicao = HtmlEntity.DeEntitize(r.SelectSingleNode("./td[last()]")?.InnerText.Trim() ?? "");

                    detalhes.EscalacaoInicialCasa.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        IdExterno = ExtrairIdDoLink(href),
                        Numero = numero,
                        Posicao = posicao,
                        Titular = false,
                        Fase = "INICIAL"
                    });
                }
            }

            // ── Eventos (gols e assistências) ────────────────────────
            var golsNodes = doc.DocumentNode.SelectNodes("//div[@id='sb-tore']//li");
            if (golsNodes != null)
            {
                foreach (var li in golsNodes)
                {
                    var jogadorNode = li.SelectSingleNode(".//div[@class='sb-aktion-aktion']/a[1]");
                    var jogadorNome = HtmlEntity.DeEntitize(jogadorNode?.InnerText.Trim() ?? "");
                    var jogadorLink = jogadorNode?.GetAttributeValue("href", "");

                    var assistNode = li.SelectSingleNode(".//div[@class='sb-aktion-aktion']/a[2]");
                    var assistenteNome = HtmlEntity.DeEntitize(assistNode?.InnerText.Trim() ?? "");
                    var assistenteLink = assistNode?.GetAttributeValue("href", "");

                    detalhes.Eventos.Add(new TransfermarktEventoInfo
                    {
                        Tipo = "Gol",
                        JogadorNome = jogadorNome,
                        JogadorLink = jogadorLink,
                        Minuto = ExtrairMinuto(li),
                        Contra = li.InnerText.Contains("contra")
                    });

                    if (!string.IsNullOrEmpty(assistenteNome))
                    {
                        detalhes.Eventos.Add(new TransfermarktEventoInfo
                        {
                            Tipo = "Assistencia",
                            AssistenteNome = assistenteNome,
                            AssistenteLink = assistenteLink,
                            Minuto = ExtrairMinuto(li)
                        });
                    }
                }
            }

            return detalhes;
        }

        private static int ExtrairMinuto(HtmlNode li)
        {
            // Procura o texto do relógio dentro do evento
            var minutoNode = li.SelectSingleNode(".//div[contains(@class,'sb-aktion-uhr')]");
            if (minutoNode != null)
            {
                var texto = HtmlEntity.DeEntitize(minutoNode.InnerText.Trim());

                // Exemplo: "45'", "90+2'"
                var match = Regex.Match(texto, @"(\d+)(\+\d+)?");
                if (match.Success)
                {
                    var baseMinuto = int.Parse(match.Groups[1].Value);
                    if (match.Groups[2].Success)
                    {
                        var acrescimo = int.Parse(match.Groups[2].Value.Replace("+", ""));
                        return baseMinuto + acrescimo;
                    }
                    return baseMinuto;
                }
            }

            // Se não encontrar, retorna 0
            return 0;
        }

        private async Task AdicionarEscalacoesComJogadoresAsync(
        FutebolContext context,
        Jogo jogo,
        List<JogadorEscalacaoTM> jogadores,
        Time time,
        bool isTimeCasa,
        string fase,
        List<PosicaoFormacao> posicoes,
        CancellationToken ct)
        {
            foreach (var j in jogadores)
            {
                // Resolve jogador no banco
                var jogadorBanco = await ResolverJogadorAsync(context, j.Nome, j.JogadorLink, time.Id, ct);
                if (jogadorBanco == null) continue;

                // Converte posição para sigla
                var sigla = MapearPosicaoParaSigla(jogadorBanco.Posicao);

                // Encontra posição correspondente na formação
                var posFormacao = posicoes.FirstOrDefault(p => p.NomePosicao == sigla);
                if (posFormacao == null)
                {
                    _logger.LogWarning("[Escalacao] Posição {Sigla} não encontrada na formação {Formacao}", sigla, jogo.FormacaoCasaId);
                    continue;
                }

                // Adiciona escalação
                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogadorBanco.Id,
                    IsTimeCasa = isTimeCasa,
                    Titular = j.Titular,
                    Posicao = posFormacao.NomePosicao,
                    PosicaoX = posFormacao.PosicaoX,
                    PosicaoY = posFormacao.PosicaoY,
                    FaseEscalacao = fase
                });
            }
        }

        private static string MapearPosicaoParaSigla(string? p) => p?.ToLower() switch
        {
            var s when s?.Contains("gol") == true ||
                       s?.Contains("keeper") == true => "GL",
            var s when s?.Contains("zagueiro") == true ||
                       s?.Contains("defesa") == true ||
                       s?.Contains("lateral") == true => "ZG",
            var s when s?.Contains("meio") == true ||
                       s?.Contains("volante") == true ||
                       s?.Contains("meia") == true => "MC",
            var s when s?.Contains("atacante") == true ||
                       s?.Contains("centroavante") == true ||
                       s?.Contains("ponta") == true ||
                       s?.Contains("extremo") == true => "AT",
            _ => "MC"
        };

        private async Task<Jogador?> ResolverJogadorAsync(
    FutebolContext context,
    string nome,
    string? linkTransfermarkt,
    int timeId,
    CancellationToken ct)
        {
            Jogador? jogador = null;

            // 1. Tenta pelo linkTransfermarkt (mais confiável)
            if (!string.IsNullOrWhiteSpace(linkTransfermarkt))
            {
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.linktransfermarket == linkTransfermarkt && j.TimeId == timeId, ct);
                if (jogador != null) return jogador;
            }

            // 2. Tenta pelo nome exato
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var nomeNormalizado = nome.Trim().ToLowerInvariant();
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.Nome.ToLower() == nomeNormalizado && j.TimeId == timeId, ct);
                if (jogador != null) return jogador;
            }

            // 3. Tenta por nome aproximado
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var candidatos = await context.Jogadores
                    .Where(j => j.TimeId == timeId)
                    .ToListAsync(ct);

                jogador = candidatos.FirstOrDefault(j =>
                    j.Nome.Contains(nome, StringComparison.InvariantCultureIgnoreCase));

                if (jogador != null) return jogador;
            }

            // 4. Se não encontrou, cria placeholder
            jogador = new Jogador
            {
                Nome = nome,
                TimeId = timeId,
                linktransfermarket = linkTransfermarkt,
                Posicao = "Indefinida",
                Atualizado = false
            };

            context.Jogadores.Add(jogador);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[ResolverJogador] Criado placeholder para {Nome} ({TimeId})", nome, timeId);

            return jogador;
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
