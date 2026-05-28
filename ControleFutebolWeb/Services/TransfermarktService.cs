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

    public class JogoTM
    {
        public string NomeTimeCasa { get; set; } = "";
        public string NomeTimeVisitante { get; set; } = "";
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public DateTime? Data { get; set; }
        public string Grupo { get; set; } = "";
        public int Rodada { get; set; }
        public string LinkDetalhes { get; set; } = "";
    }

    public class DetalhesJogoTM
    {
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public string? FormacaoCasa { get; set; }
        public string? FormacaoVisitante { get; set; }

        public List<JogadorEscalacaoTM> EscalacaoInicialCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoInicialVisitante { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalVisitante { get; set; } = new();

        public List<GolTM> Gols { get; set; } = new();
        public List<TransfermarktEventoInfo> Eventos { get; set; } = new();
    }

    public record JogadorEscalacaoTM
    {
        public string Nome { get; init; } = "";
        public int? Numero { get; init; }
        public string Posicao { get; init; } = "";
        public bool Titular { get; init; }
        public long? IdExterno { get; init; }
        public string Fase { get; init; } = "INICIAL";
        public string? JogadorLink { get; set; }
        // CSS position from the formation visual (top % from panel top, left %)
        public float TopPct { get; set; } = 0;
        public float LeftPct { get; set; } = 0;
    }

    // Dados completos do perfil do jogador (uma única requisição HTTP)
    internal record InfoPerfilJogadorTM(string? Posicao, string? Nacionalidade, string? FotoUrl, DateTime? DataNascimento);


    public class GolTM
    {
        public string NomeJogador { get; set; } = "";
        public long? IdExterno { get; set; }
        public int Minuto { get; set; }
        public bool IsTimeCasa { get; set; }
        public bool Contra { get; set; }
    }

    public class SubstituicaoTM
    {
        public string NomeEntrou { get; set; } = "";
        public long? IdEntrou { get; set; }
        public string NomeSaiu { get; set; } = "";
        public long? IdSaiu { get; set; }
    }

    public class SincronizacaoResultado
    {
        public int JogosEncontradosNaSite { get; set; }
        public int JogosAtualizados { get; set; }
        public int JogosNaoEncontrados { get; set; }
        public int PlacaresAtualizados { get; set; }
        public int EscalacoesImportadas { get; set; }
        public int GolsImportados { get; set; }
        public List<string> Avisos { get; } = new();

        public override string ToString() =>
            $"Site:{JogosEncontradosNaSite} | Atualizados:{JogosAtualizados} | " +
            $"Placares:{PlacaresAtualizados} | Escalações:{EscalacoesImportadas} | " +
            $"Gols:{GolsImportados} | NãoEncontrados:{JogosNaoEncontrados} | " +
            $"Avisos:{Avisos.Count}";
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

        public bool IsTimeCasa { get; set; }
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
            if (!linkDetalhes.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;

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
                    var dataValida = ValidarAnoDt(dt);
                    if (dataValida == null)
                    {
                        _logger.LogWarning("[BuscarData] Ano inválido {Ano} em '{Str}' — ignorado.", dt.Year, $"{dataStr} {horaStr}");
                        return null;
                    }
                    _logger.LogInformation("[BuscarData] Data parseada: {Data}", dataValida.Value.ToString("dd/MM/yyyy HH:mm"));
                    return dataValida;
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
                // 🔹 Em vez de criar vazio, usa fallback
                formacao = await context.Formacoes
                    .Include(f => f.Posicoes)
                    .FirstOrDefaultAsync(f => f.Nome == "4-3-3", ct);

                _logger.LogWarning("[TransfermarktSync] Formação {Formacao} não encontrada, usando fallback 4-3-3.", nomeFormacao);
            }

            return formacao;
        }

        // ── Gera posições genéricas em memória para formações sem cadastro no banco ──
        // Exemplo: "4-3-3" → GK (Y=85) + 4 defensores (Y=72) + 3 meias (Y=45) + 3 atacantes (Y=18)
        // Garante que a normalização dinâmica funcione corretamente mesmo sem formação configurada.
        private static List<PosicaoFormacao> GerarPosicoesGenericas(string? nomeFormacao, int formacaoId)
        {
            // Parse "4-3-3" → [4, 3, 3]
            var linhas = new List<int>();
            if (!string.IsNullOrWhiteSpace(nomeFormacao))
            {
                foreach (var parte in nomeFormacao.Trim().Split('-'))
                    if (int.TryParse(parte.Trim(), out int n)) linhas.Add(n);
            }

            // Valida: GK + linhas = 11; fallback 4-4-2
            if (!linhas.Any() || linhas.Sum() + 1 != 11)
                linhas = new List<int> { 4, 4, 2 };

            var posicoes = new List<PosicaoFormacao>();
            int ordem = 1;

            // Goleiro: sempre no fundo, centro
            posicoes.Add(new PosicaoFormacao
            {
                FormacaoId = formacaoId,
                NomePosicao = "Goleiro",
                PosicaoX = 50,
                PosicaoY = 85,
                Ordem = ordem++
            });

            // Linhas: Y de 72 (mais próximo ao GK) a 15 (mais próximo ao ataque), igualmente espaçadas
            int totalLinhas = linhas.Count;
            for (int l = 0; l < totalLinhas; l++)
            {
                double y = totalLinhas > 1
                    ? 72.0 - l * 57.0 / (totalLinhas - 1)
                    : 45.0;

                int n = linhas[l];
                for (int c = 0; c < n; c++)
                {
                    double x = (c + 1.0) / (n + 1) * 100.0;
                    posicoes.Add(new PosicaoFormacao
                    {
                        FormacaoId = formacaoId,
                        NomePosicao = $"P{ordem}",
                        PosicaoX = Math.Round(x, 1),
                        PosicaoY = Math.Round(y, 1),
                        Ordem = ordem++
                    });
                }
            }

            return posicoes;
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
                            data = ValidarAnoDt(dt);
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
                    return ValidarAnoDt(dt);
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
                            jogoInfo.Data = ValidarAnoDt(dataJogo);
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

        public async Task<string?> BuscarPosicaoPorLink(string profileUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;
            if (!profileUrl.StartsWith("http"))
                profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

            try
            {
                await Task.Delay(1000, ct);
                var html = await _httpClient.GetStringAsync(profileUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // <li class="data-header__label">Posição: <span class="data-header__content">Centroavante</span></li>
                var liNodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'data-header__label')]");
                if (liNodes != null)
                {
                    foreach (var li in liNodes)
                    {
                        var textoLi = HtmlEntity.DeEntitize(li.InnerText).ToLowerInvariant();
                        if (textoLi.Contains("posi"))
                        {
                            var span = li.SelectSingleNode(".//span[contains(@class,'data-header__content')]");
                            if (span != null)
                            {
                                var posicao = HtmlEntity.DeEntitize(span.InnerText.Trim());
                                if (!string.IsNullOrWhiteSpace(posicao))
                                {
                                    _logger.LogInformation("[BuscarPosicao] {Url} → {Posicao}", profileUrl, posicao);
                                    return posicao;
                                }
                            }
                        }
                    }
                }

                _logger.LogWarning("[BuscarPosicao] Posição não encontrada: {Url}", profileUrl);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BuscarPosicao] Erro ao buscar posição: {Url}", profileUrl);
                return null;
            }
        }

        // Busca posição, nacionalidade e foto em uma única requisição ao perfil do jogador
        private async Task<InfoPerfilJogadorTM?> BuscarInfoPerfilJogador(string profileUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;
            if (!profileUrl.StartsWith("http"))
                profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

            try
            {
                await Task.Delay(1000, ct);
                var html = await _httpClient.GetStringAsync(profileUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Posição
                string? posicao = null;
                var liNodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'data-header__label')]");
                if (liNodes != null)
                {
                    foreach (var li in liNodes)
                    {
                        if (HtmlEntity.DeEntitize(li.InnerText).ToLowerInvariant().Contains("posi"))
                        {
                            var span = li.SelectSingleNode(".//span[contains(@class,'data-header__content')]");
                            var v = HtmlEntity.DeEntitize(span?.InnerText.Trim() ?? "");
                            if (!string.IsNullOrWhiteSpace(v)) { posicao = v; break; }
                        }
                    }
                }

                // Nacionalidade
                string? nacionalidade = null;
                var infoSpans = doc.DocumentNode.SelectNodes(
                    "//span[@class='info-table__content info-table__content--bold']");
                var labelSpans = doc.DocumentNode.SelectNodes(
                    "//span[@class='info-table__content info-table__content--regular']");
                if (infoSpans != null && labelSpans != null)
                {
                    for (int i = 0; i < Math.Min(infoSpans.Count, labelSpans.Count); i++)
                    {
                        var label = labelSpans[i].InnerText.Trim().ToLower();
                        if (label.Contains("nacional") || label.Contains("nationalit") || label.Contains("nation"))
                        {
                            var imgTitle = infoSpans[i].SelectSingleNode(".//img")
                                ?.GetAttributeValue("title", "")
                                ?? infoSpans[i].SelectSingleNode(".//img")
                                    ?.GetAttributeValue("alt", "");
                            var raw = !string.IsNullOrWhiteSpace(imgTitle)
                                ? imgTitle
                                : HtmlEntity.DeEntitize(infoSpans[i].InnerText.Trim());
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                nacionalidade = NacionalidadesHelper.Normalizar(raw);
                                break;
                            }
                        }
                    }
                }

                // Data de nascimento
                DateTime? dataNascimento = null;
                var birthNode = doc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                    dataNascimento = ParseDataNascimento(birthNode.InnerText.Trim().Split('(')[0].Trim());

                if (dataNascimento == null && infoSpans != null && labelSpans != null)
                {
                    for (int i = 0; i < Math.Min(infoSpans.Count, labelSpans.Count); i++)
                    {
                        var label = labelSpans[i].InnerText.Trim().ToLower();
                        if (label.Contains("nascimento") || label.Contains("geboren") || label.Contains("date of birth"))
                        {
                            dataNascimento = ParseDataNascimento(
                                HtmlEntity.DeEntitize(infoSpans[i].InnerText.Trim()));
                            break;
                        }
                    }
                }

                if (dataNascimento.HasValue &&
                    (dataNascimento.Value.Year < 1900 || dataNascimento.Value > DateTime.Today))
                    dataNascimento = null;

                // Foto (og:image preferido, fallback na tag img)
                string? fotoUrl = doc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']")
                    ?.GetAttributeValue("content", "")?.Trim();
                if (string.IsNullOrEmpty(fotoUrl))
                    fotoUrl = doc.DocumentNode
                        .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]")
                        ?.GetAttributeValue("src", "")?.Trim();

                _logger.LogInformation("[InfoPerfil] {Url} → Pos={Pos} Nac={Nac} Nasc={Nasc} Foto={Foto}",
                    profileUrl, posicao ?? "–", nacionalidade ?? "–",
                    dataNascimento?.ToString("dd/MM/yyyy") ?? "–",
                    string.IsNullOrEmpty(fotoUrl) ? "–" : "ok");

                return new InfoPerfilJogadorTM(posicao, nacionalidade, fotoUrl, dataNascimento);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InfoPerfil] Erro: {Url}", profileUrl);
                return null;
            }
        }

        // Resolve (ou cria) uma Nacionalidade pelo nome normalizado e retorna o Id
        private async Task<int?> ResolverNacionalidadeAsync(
            FutebolContext context, string? nomePt, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomePt)) return null;
            var nac = await context.Nacionalidades
                .FirstOrDefaultAsync(n => n.Nome.ToLower() == nomePt.ToLower(), ct);
            if (nac == null)
            {
                nac = new Nacionalidade { Nome = nomePt };
                context.Nacionalidades.Add(nac);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[Nacionalidade] Criada: {Nome}", nomePt);
            }
            return nac.Id;
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
                                data = ValidarAnoDt(dt);
                                if (data == null)
                                    _logger.LogWarning("[Brasileirao] Ano inválido {Ano} em '{Str}' — data ignorada.", dt.Year, $"{ultimaDataStr} {horaStr}");
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
                    return ValidarAnoDt(data);
            }

            // Fallback com InvariantCulture para evitar interpretação dependente de locale
            if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataGenerica))
                return ValidarAnoDt(dataGenerica);

            return null;
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
            // Titulares ficam nos containers .aufstellung-box, mas o Transfermarkt
            // alterna entre um bloco por time e colunas large-6 envolvendo cada time.
            var aufstellungBoxes = SelecionarBlocosEscalacao(doc);

            if (aufstellungBoxes.Count == 1)
            {
                var blocoUnico = aufstellungBoxes[0];

                detalhes.EscalacaoInicialCasa = ExtrairTitulares(blocoUnico, isVisitante: false);
                detalhes.EscalacaoInicialCasa.AddRange(ExtrairBanco(blocoUnico, isVisitante: false));

                detalhes.EscalacaoInicialVisitante = ExtrairTitulares(blocoUnico, isVisitante: true);
                detalhes.EscalacaoInicialVisitante.AddRange(ExtrairBanco(blocoUnico, isVisitante: true));
            }
            else if (aufstellungBoxes.Count >= 2)
            {
                detalhes.EscalacaoInicialCasa = ExtrairTitulares(aufstellungBoxes[0], isVisitante: false);
                detalhes.EscalacaoInicialCasa.AddRange(ExtrairBanco(aufstellungBoxes[0], isVisitante: false));

                detalhes.EscalacaoInicialVisitante = ExtrairTitulares(aufstellungBoxes[1], isVisitante: true);
                detalhes.EscalacaoInicialVisitante.AddRange(ExtrairBanco(aufstellungBoxes[1], isVisitante: true));
            }
            else
            {
                _logger.LogWarning("[Transfermarkt] aufstellung-box do visitante nao encontrada. " +
                    "aufstellungBoxes={Count}", aufstellungBoxes.Count);
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

        private static List<HtmlNode> SelecionarBlocosEscalacao(HtmlDocument doc)
        {
            var seletoresPreferenciais = new[]
            {
                "//div[contains(concat(' ', normalize-space(@class), ' '), ' large-6 ') and contains(concat(' ', normalize-space(@class), ' '), ' columns ')][.//div[contains(@class,'formation-player-container')]]",
                "//div[contains(@class,'aufstellung-vereinsseite')][.//div[contains(@class,'formation-player-container')]]",
                "//div[contains(@class,'aufstellung-box')][.//div[contains(@class,'formation-player-container')]]"
            };

            foreach (var seletor in seletoresPreferenciais)
            {
                var blocos = doc.DocumentNode.SelectNodes(seletor)?
                    .Where(b => b.SelectNodes(".//div[contains(@class,'formation-player-container')]")?.Count > 0)
                    .DistinctBy(b => b.XPath)
                    .ToList() ?? new List<HtmlNode>();

                if (blocos.Count >= 2)
                    return blocos.Take(2).ToList();
            }

            var fallback = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'aufstellung-box') or contains(@class,'aufstellung')]")?
                .Where(b => b.SelectNodes(".//div[contains(@class,'formation-player-container')]")?.Count > 0)
                .DistinctBy(b => b.XPath)
                .ToList() ?? new List<HtmlNode>();

            return fallback.Take(2).ToList();
        }

        // Extrai titulares a partir do bloco de escalacao.
        // Os jogadores ficam em <div class="formation-player-container">
        // com um link <a href="/slug/profil/spieler/ID">
        private List<JogadorEscalacaoTM> ExtrairTitulares(HtmlNode blocoBox, bool isVisitante = false)
        {
            var lista = new List<JogadorEscalacaoTM>();

            var containers = blocoBox.SelectNodes(
                ".//div[contains(@class,'formation-player-container')]");
            if (containers == null) return lista;

            var containersDoLado = FiltrarContainersTitularesPorLado(containers.ToList(), isVisitante);

            foreach (var c in containersDoLado)
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

                // Posição visual no campo via CSS (top/left %)
                var style = c.GetAttributeValue("style", "");
                var tmatch = Regex.Match(style, @"top\s*:\s*([\d.]+)%");
                var lmatch = Regex.Match(style, @"left\s*:\s*([\d.]+)%");
                float topPct = tmatch.Success ? float.Parse(tmatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                float leftPct = lmatch.Success ? float.Parse(lmatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;

                lista.Add(new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = InferirPosicaoPorNumero(numero),
                    Titular = true,
                    IdExterno = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : null,
                    Fase = "INICIAL",
                    JogadorLink = MontarUrlAbsoluta(href),
                    TopPct = topPct,
                    LeftPct = leftPct
                });
            }

            // GK tem top alto (~80%), atacante tem top baixo (~3%) → ordenação decrescente = GK primeiro
            return lista.OrderByDescending(j => j.TopPct).ThenBy(j => j.LeftPct).ToList();
        }

        // ── Extrai banco de reservas a partir do bloco aufstellung-box ─────────────
        // Os reservas ficam em <table class="ersatzbank">
        // Cada <tr> tem: número | link jogador | sigla posição
        private static List<HtmlNode> FiltrarContainersTitularesPorLado(List<HtmlNode> containers, bool isVisitante)
        {
            if (containers.Count <= 11)
                return containers;

            var porClasse = containers
                .Where(c => NoOuAncestralIndicaLado(c, isVisitante))
                .ToList();
            if (porClasse.Any())
                return porClasse;

            var comLeft = containers
                .Select(c => new { Node = c, Left = ExtrairPercentualCss(c, "left") })
                .Where(x => x.Left.HasValue)
                .OrderBy(x => x.Left!.Value)
                .ToList();

            if (comLeft.Count >= 16)
            {
                var metade = comLeft.Count / 2;
                return (isVisitante ? comLeft.Skip(metade) : comLeft.Take(metade))
                    .Select(x => x.Node)
                    .ToList();
            }

            return isVisitante ? new List<HtmlNode>() : containers;
        }

        private static bool NoOuAncestralIndicaLado(HtmlNode node, bool isVisitante)
        {
            for (var atual = node; atual != null; atual = atual.ParentNode)
            {
                var classe = atual.GetAttributeValue("class", "").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(classe)) continue;

                var indicaCasa = classe.Contains("heim") || classe.Contains("home");
                var indicaVisitante = classe.Contains("gast") || classe.Contains("away");

                if (isVisitante && indicaVisitante) return true;
                if (!isVisitante && indicaCasa) return true;
            }

            return false;
        }

        private static float? ExtrairPercentualCss(HtmlNode node, string propriedade)
        {
            var style = node.GetAttributeValue("style", "");
            var match = Regex.Match(style, $@"{Regex.Escape(propriedade)}\s*:\s*([\d.]+)%", RegexOptions.IgnoreCase);
            return match.Success
                ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                : null;
        }

        private List<JogadorEscalacaoTM> ExtrairBanco(HtmlNode blocoBox, bool isVisitante = false)
        {
            var lista = new List<JogadorEscalacaoTM>();

            var tabelas = blocoBox.SelectNodes(".//table[contains(@class,'ersatzbank')]")?.ToList();
            var tabela = tabelas == null || !tabelas.Any()
                ? null
                : tabelas[Math.Min(isVisitante ? 1 : 0, tabelas.Count - 1)];
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
                    Fase = "INICIAL",
                    JogadorLink = MontarUrlAbsoluta(href)
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

                int minuto = ExtrairMinutoDoNo(li);

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

                var textoAcao = HtmlEntity.DeEntitize(li.InnerText).ToLowerInvariant();

                var textoAcaoLower = textoAcao.ToLowerInvariant();
                bool contra = textoAcaoLower.Contains("gol contra")
                   && !textoAcaoLower.Contains("contra-ataque")
                   && !textoAcaoLower.Contains("contra ataque")
                   || li.InnerHtml.Contains("Eigentor");

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
                int minuto = ExtrairMinutoDoNo(li);

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
                int minuto = ExtrairMinutoDoNo(li);

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
                var textoNode = node.InnerText.ToLowerInvariant();
                bool contra = textoNode.Contains("gol contra") &&
                              !textoNode.Contains("contra-ataque") &&
                              !textoNode.Contains("contra ataque");

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

        private async Task AdicionarEscalacoesComJogadoresAsync(
        FutebolContext context,
        Jogo jogo,
        List<JogadorEscalacaoTM> jogadores,
        Time time,
        bool isTimeCasa,
        string fase,
        List<PosicaoFormacao> posicoes,
        Guid cicloId,
        CancellationToken ct)
        {
            // Nearest-neighbor com normalização dinâmica:
            // Ambos os sistemas de coordenadas são normalizados para [0,1] com base nos
            // min/max reais dos dados (TM e DB), tornando a comparação invariante à escala/formação.
            var titulares = jogadores.Where(j => j.Titular).ToList();
            var reservas = jogadores.Where(j => !j.Titular).ToList();
            var slotsOrdenados = posicoes.OrderBy(p => p.Ordem).ToList();
            var slotsDisponiveis = slotsOrdenados.ToList();

            // ── Ranges TM (apenas jogadores com coordenadas válidas) ──────────
            var tmComCoords = titulares.Where(j => j.TopPct > 0 || j.LeftPct > 0).ToList();
            float tmMinY = tmComCoords.Any() ? tmComCoords.Min(j => j.TopPct)  : 0f;
            float tmMaxY = tmComCoords.Any() ? tmComCoords.Max(j => j.TopPct)  : 100f;
            float tmMinX = tmComCoords.Any() ? tmComCoords.Min(j => j.LeftPct) : 0f;
            float tmMaxX = tmComCoords.Any() ? tmComCoords.Max(j => j.LeftPct) : 100f;

            // ── Ranges DB (slots da formação) ─────────────────────────────────
            float dbMinY = posicoes.Any() ? (float)posicoes.Min(p => p.PosicaoY) : 0f;
            float dbMaxY = posicoes.Any() ? (float)posicoes.Max(p => p.PosicaoY) : 100f;
            float dbMinX = posicoes.Any() ? (float)posicoes.Min(p => p.PosicaoX) : 0f;
            float dbMaxX = posicoes.Any() ? (float)posicoes.Max(p => p.PosicaoX) : 100f;

            // Normaliza value no range [min,max] → [0,1]  (retorna 0.5 se range=0)
            float Norm(float v, float min, float max) =>
                max > min ? (v - min) / (max - min) : 0.5f;

            _logger.LogInformation(
                "[Escalacao] Ranges TM  Y[{TYmin}–{TYmax}] X[{TXmin}–{TXmax}] | " +
                "DB Y[{DYmin}–{DYmax}] X[{DXmin}–{DXmax}]",
                tmMinY, tmMaxY, tmMinX, tmMaxX, dbMinY, dbMaxY, dbMinX, dbMaxX);

            var pareamentoPorIndice = CalcularMelhorPareamentoPorCoordenadas(
                titulares, slotsOrdenados, Norm,
                tmMinY, tmMaxY, tmMinX, tmMaxX,
                dbMinY, dbMaxY, dbMinX, dbMaxX);
            var slotsPareados = pareamentoPorIndice.Values.ToHashSet();

            for (int i = 0; i < titulares.Count; i++)
            {
                var j = titulares[i];
                PosicaoFormacao? posFormacao;
                if (pareamentoPorIndice.TryGetValue(i, out var slotPareado) &&
                    slotsDisponiveis.Contains(slotPareado))
                {
                    posFormacao = slotPareado;
                }
                else
                {
                    posFormacao = slotsDisponiveis.FirstOrDefault(p => !slotsPareados.Contains(p))
                        ?? slotsDisponiveis.FirstOrDefault();
                }
                if (posFormacao != null) slotsDisponiveis.Remove(posFormacao);

                var linkNormalizado = !string.IsNullOrWhiteSpace(j.JogadorLink)
                    ? (j.JogadorLink.StartsWith("http") ? j.JogadorLink
                       : "https://www.transfermarkt.com.br" + j.JogadorLink)
                    : null;

                InfoPerfilJogadorTM? perfilTM = null;
                if (!string.IsNullOrWhiteSpace(linkNormalizado))
                    perfilTM = await BuscarInfoPerfilJogador(linkNormalizado, ct);

                var posicaoParaCriacao = perfilTM?.Posicao ?? j.Posicao;
                var jogadorBanco = await ResolverJogadorAsync(
                    context, j.Nome, linkNormalizado, time.Id, ct,
                    posicaoParaCriacao, j.Numero, j.IdExterno, cicloId);
                if (jogadorBanco == null) continue;

                if (!string.IsNullOrWhiteSpace(perfilTM?.Posicao) && jogadorBanco.Posicao != perfilTM.Posicao)
                {
                    _logger.LogInformation("[Escalacao] Atualizando posição {Nome}: {Antiga} → {Nova}",
                        jogadorBanco.Nome, jogadorBanco.Posicao, perfilTM.Posicao);
                    jogadorBanco.Posicao = perfilTM.Posicao;
                }

                await AtualizarNacionalidadeFotoJogador(context, jogadorBanco, perfilTM, ct);

                var posicaoJogador = perfilTM?.Posicao
                    ?? (!string.IsNullOrWhiteSpace(j.Posicao) ? j.Posicao : jogadorBanco.Posicao);

                _logger.LogInformation("[Escalacao] Titular[{I}] {Nome} → slot {Slot} ({SlotPos}) TM({TX}%,{TL}%)",
                    i, jogadorBanco.Nome, posFormacao?.Ordem ?? -1, posFormacao?.NomePosicao ?? "—",
                    j.TopPct, j.LeftPct);

                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogadorBanco.Id,
                    IsTimeCasa = isTimeCasa,
                    Titular = true,
                    Posicao = posicaoJogador,
                    PosicaoX = posFormacao?.PosicaoX ?? 50,
                    PosicaoY = posFormacao?.PosicaoY ?? 50,
                    FaseEscalacao = fase
                });
            }

            // Reservas: sem slot de formação (PosicaoX/Y = 0)
            foreach (var j in reservas)
            {
                var linkNormalizado = !string.IsNullOrWhiteSpace(j.JogadorLink)
                    ? (j.JogadorLink.StartsWith("http") ? j.JogadorLink
                       : "https://www.transfermarkt.com.br" + j.JogadorLink)
                    : null;

                InfoPerfilJogadorTM? perfilTM = null;
                if (!string.IsNullOrWhiteSpace(linkNormalizado))
                    perfilTM = await BuscarInfoPerfilJogador(linkNormalizado, ct);

                var posicaoParaCriacao = perfilTM?.Posicao ?? j.Posicao;
                var jogadorBanco = await ResolverJogadorAsync(
                    context, j.Nome, linkNormalizado, time.Id, ct,
                    posicaoParaCriacao, j.Numero, j.IdExterno, cicloId);
                if (jogadorBanco == null) continue;

                if (!string.IsNullOrWhiteSpace(perfilTM?.Posicao) && jogadorBanco.Posicao != perfilTM.Posicao)
                    jogadorBanco.Posicao = perfilTM.Posicao;

                await AtualizarNacionalidadeFotoJogador(context, jogadorBanco, perfilTM, ct);

                var posicaoJogador = perfilTM?.Posicao
                    ?? (!string.IsNullOrWhiteSpace(j.Posicao) ? j.Posicao : jogadorBanco.Posicao);

                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogadorBanco.Id,
                    IsTimeCasa = isTimeCasa,
                    Titular = false,
                    Posicao = posicaoJogador,
                    PosicaoX = 0,
                    PosicaoY = 0,
                    FaseEscalacao = fase
                });
            }
        }

        private static Dictionary<int, PosicaoFormacao> CalcularMelhorPareamentoPorCoordenadas(
            List<JogadorEscalacaoTM> titulares,
            List<PosicaoFormacao> slots,
            Func<float, float, float, float> norm,
            float tmMinY,
            float tmMaxY,
            float tmMinX,
            float tmMaxX,
            float dbMinY,
            float dbMaxY,
            float dbMinX,
            float dbMaxX)
        {
            var jogadoresComCoords = titulares
                .Select((Jogador, Indice) => new { Jogador, Indice })
                .Where(x => x.Jogador.TopPct > 0 || x.Jogador.LeftPct > 0)
                .ToList();

            if (!jogadoresComCoords.Any() || !slots.Any())
                return new Dictionary<int, PosicaoFormacao>();

            var totalJogadores = Math.Min(jogadoresComCoords.Count, slots.Count);
            jogadoresComCoords = jogadoresComCoords.Take(totalJogadores).ToList();
            var totalMascaras = 1 << slots.Count;
            var dp = Enumerable.Repeat(double.PositiveInfinity, totalMascaras).ToArray();
            var escolhas = new int[totalJogadores, totalMascaras];
            for (var jogadorIdx = 0; jogadorIdx < totalJogadores; jogadorIdx++)
                for (var mascara = 0; mascara < totalMascaras; mascara++)
                    escolhas[jogadorIdx, mascara] = -1;
            dp[0] = 0;

            for (var jogadorIdx = 0; jogadorIdx < totalJogadores; jogadorIdx++)
            {
                var proximoDp = Enumerable.Repeat(double.PositiveInfinity, totalMascaras).ToArray();
                var jogador = jogadoresComCoords[jogadorIdx].Jogador;
                var normY = norm(jogador.TopPct, tmMinY, tmMaxY);
                var normX = norm(jogador.LeftPct, tmMinX, tmMaxX);

                for (var mascara = 0; mascara < totalMascaras; mascara++)
                {
                    if (double.IsPositiveInfinity(dp[mascara])) continue;

                    for (var slotIdx = 0; slotIdx < slots.Count; slotIdx++)
                    {
                        if ((mascara & (1 << slotIdx)) != 0) continue;

                        var slot = slots[slotIdx];
                        var custo = Math.Pow(norm((float)slot.PosicaoX, dbMinX, dbMaxX) - normX, 2) +
                                    Math.Pow(norm((float)slot.PosicaoY, dbMinY, dbMaxY) - normY, 2);
                        var novaMascara = mascara | (1 << slotIdx);
                        var novoCusto = dp[mascara] + custo;

                        if (novoCusto < proximoDp[novaMascara])
                        {
                            proximoDp[novaMascara] = novoCusto;
                            escolhas[jogadorIdx, novaMascara] = slotIdx;
                        }
                    }
                }

                dp = proximoDp;
            }

            var melhorMascara = Enumerable.Range(0, totalMascaras)
                .Where(mascara => CountBits(mascara) == totalJogadores)
                .OrderBy(mascara => dp[mascara])
                .FirstOrDefault();

            var resultado = new Dictionary<int, PosicaoFormacao>();
            for (var jogadorIdx = totalJogadores - 1; jogadorIdx >= 0; jogadorIdx--)
            {
                var slotIdx = escolhas[jogadorIdx, melhorMascara];
                if (slotIdx < 0) break;

                resultado[jogadoresComCoords[jogadorIdx].Indice] = slots[slotIdx];
                melhorMascara &= ~(1 << slotIdx);
            }

            return resultado;
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        private async Task AtualizarNacionalidadeFotoJogador(
            FutebolContext context, Jogador jogador,
            InfoPerfilJogadorTM? perfil, CancellationToken ct)
        {
            if (perfil == null) return;

            if (!string.IsNullOrWhiteSpace(perfil.Nacionalidade) && jogador.NacionalidadeId == null)
            {
                var nacId = await ResolverNacionalidadeAsync(context, perfil.Nacionalidade, ct);
                if (nacId.HasValue)
                    jogador.NacionalidadeId = nacId.Value;
            }

            if (!string.IsNullOrEmpty(perfil.FotoUrl) && string.IsNullOrEmpty(jogador.FotoUrl))
                jogador.FotoUrl = perfil.FotoUrl;

            if (perfil.DataNascimento.HasValue && jogador.DataNascimento == null)
                jogador.DataNascimento = DateTime.SpecifyKind(perfil.DataNascimento.Value, DateTimeKind.Unspecified);
        }

        private static bool PosicaoCompativel(string nomePosicao, string sigla)
        {
            var s = (nomePosicao ?? "").ToLowerInvariant();
            return sigla switch
            {
                "GL" => s.Contains("gol") || s == "gl",
                "ZG" => s.Contains("zagueiro") || s.Contains("lateral") || s.Contains("defes") || s == "zg" || s == "ld" || s == "le",
                "MC" => s.Contains("meio") || s.Contains("volante") || s.Contains("meia") || s == "mc",
                "AT" => s.Contains("atac") || s.Contains("centroavante") || s.Contains("ponta") || s == "at",
                _ => false
            };
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
    CancellationToken ct,
    string? posicaoTransfermarkt = null,
    int? numeroCamisa = null,
    long? idExterno = null,
    Guid? cicloId = null)
        {
            Jogador? jogador = null;

            // 1. Tenta pelo linkTransfermarkt
            if (!string.IsNullOrWhiteSpace(linkTransfermarkt))
            {
                var linkNormalizado = NormalizarLinkTransfermarkt(linkTransfermarkt);
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.linktransfermarket == linkNormalizado && j.TimeId == timeId, ct);
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

            // 4. Se não encontrou, cria novo jogador (mas não placeholder "Indefinida")
            bool nomePareceLinkOuInvalido = string.IsNullOrWhiteSpace(nome) ||
                nome.StartsWith("/") ||
                nome.StartsWith("http") ||
                nome.Contains("/spieler/") ||
                nome.Contains("/profil/") ||
                nome == "Indefinida";

            if (nomePareceLinkOuInvalido)
            {
                _logger.LogWarning("[ResolverJogador] Nome inválido ignorado: '{Nome}' | Link={Link}", nome, linkTransfermarkt);
                return null;
            }

            jogador = new Jogador
            {
                Nome = nome,
                TimeId = timeId,
                linktransfermarket = NormalizarLinkTransfermarkt(linkTransfermarkt ?? ""),
                Posicao = MapearPosicaoParaNome(posicaoTransfermarkt),
                NumeroCamisa = numeroCamisa,
                IdApi = idExterno,
                DataNascimento = null,
                DtInc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                Atualizado = false
            };

            context.Jogadores.Add(jogador);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[ResolverJogador] Criado novo jogador: {Nome} ({TimeId}) | Posicao={Posicao} | Link={Link}",
                jogador.Nome, timeId, jogador.Posicao, jogador.linktransfermarket);

            if (cicloId.HasValue)
            {
                var timeNome = context.Times.Local.FirstOrDefault(t => t.Id == timeId)?.Nome ?? $"TimeId:{timeId}";
                RegistrarLog(context, cicloId.Value, "Jogador", "Criado",
                    timeNome: timeNome,
                    detalhes: $"Criado: {jogador.Nome} | Posição: {jogador.Posicao ?? "–"} | Nº: {jogador.NumeroCamisa?.ToString() ?? "–"}");
            }

            return jogador;
        }


        public async Task IncluirOuAtualizarJogo(
        FutebolContext context,
        Competicao competicao,
        TransfermarktJogoInfo jogoWeb,
        Time timeCasa,
        Time timeVisitante,
        Guid cicloId,
        TransfermarktService transfermarkt,
        CancellationToken ct)
        {
            // ── Resolve data ──────────────────────────────────────────────────────
            DateTime? dataJogo = jogoWeb.Data.HasValue ? ValidarAnoDt(jogoWeb.Data.Value) : null;

            if (dataJogo == null && jogoWeb.Data.HasValue)
                _logger.LogWarning("[IncluirJogo] Data da lista com ano inválido ({Ano}) — buscando no detalhe.", jogoWeb.Data.Value.Year);

            if (!dataJogo.HasValue && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                _logger.LogInformation("[IncluirJogo] Buscando data no detalhe: {Link}", jogoWeb.LinkDetalhes);
                dataJogo = await transfermarkt.BuscarDataJogoPorLink(jogoWeb.LinkDetalhes, ct);
            }

            // ── Busca jogos do mesmo confronto na competição ──────────────────────
            var jogosBanco = await context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id &&
                            j.TimeCasaId == timeCasa.Id &&
                            j.TimeVisitanteId == timeVisitante.Id)
                .ToListAsync(ct);

            // ── Validação anti-duplicata ──────────────────────────────────────────
            if (jogoWeb.Rodada > 0)
            {
                var jogoMesmaRodada = jogosBanco.FirstOrDefault(j => j.Rodada == jogoWeb.Rodada);
                if (jogoMesmaRodada != null)
                {
                    if (dataJogo.HasValue)
                    {
                        var dataPersistida = DateTime.SpecifyKind(dataJogo.Value, DateTimeKind.Utc);
                        if (!jogoMesmaRodada.Data.HasValue || jogoMesmaRodada.Data.Value != dataPersistida)
                            jogoMesmaRodada.Data = dataPersistida;
                    }

                    if (!string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
                        jogoMesmaRodada.LinkDetalhes = jogoWeb.LinkDetalhes;

                    jogoMesmaRodada.Grupo = jogoWeb.Grupo ?? jogoMesmaRodada.Grupo;
                    if (!jogoWeb.PlacarCasa.HasValue && string.IsNullOrWhiteSpace(jogoMesmaRodada.Status))
                        jogoMesmaRodada.Status = "Agendado";

                    if (jogoWeb.PlacarCasa.HasValue &&
                        (jogoMesmaRodada.PlacarCasa != jogoWeb.PlacarCasa ||
                         jogoMesmaRodada.PlacarVisitante != jogoWeb.PlacarVisitante))
                    {
                        jogoMesmaRodada.PlacarCasa = jogoWeb.PlacarCasa;
                        jogoMesmaRodada.PlacarVisitante = jogoWeb.PlacarVisitante;
                        jogoMesmaRodada.Status = "Finalizado";
                        jogoMesmaRodada.Atualizado = 1;
                    }
                    await ReimportarEscalacoesSeNecessario(context, jogoMesmaRodada, jogoWeb, timeCasa, timeVisitante, cicloId, transfermarkt, ct);
                    return;
                }
            }

            if (dataJogo.HasValue)
            {
                var jogoPorData = jogosBanco.FirstOrDefault(j =>
                    j.Data.HasValue &&
                    Math.Abs((j.Data.Value.Date - dataJogo.Value.Date).TotalDays) <= 2);

                if (jogoPorData != null)
                {
                    var dataPersistida = DateTime.SpecifyKind(dataJogo.Value, DateTimeKind.Utc);
                    if (!jogoPorData.Data.HasValue || jogoPorData.Data.Value != dataPersistida)
                        jogoPorData.Data = dataPersistida;

                    if (!string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
                        jogoPorData.LinkDetalhes = jogoWeb.LinkDetalhes;

                    jogoPorData.Rodada = jogoWeb.Rodada > 0 ? jogoWeb.Rodada : jogoPorData.Rodada;
                    jogoPorData.Grupo = jogoWeb.Grupo ?? jogoPorData.Grupo;
                    if (!jogoWeb.PlacarCasa.HasValue && string.IsNullOrWhiteSpace(jogoPorData.Status))
                        jogoPorData.Status = "Agendado";

                    if (jogoWeb.PlacarCasa.HasValue &&
                        (jogoPorData.PlacarCasa != jogoWeb.PlacarCasa ||
                         jogoPorData.PlacarVisitante != jogoWeb.PlacarVisitante))
                    {
                        jogoPorData.PlacarCasa = jogoWeb.PlacarCasa;
                        jogoPorData.PlacarVisitante = jogoWeb.PlacarVisitante;
                        jogoPorData.Status = "Finalizado";
                        jogoPorData.Atualizado = 1;
                    }
                    await ReimportarEscalacoesSeNecessario(context, jogoPorData, jogoWeb, timeCasa, timeVisitante, cicloId, transfermarkt, ct);
                    return;
                }
            }

            // ── Busca detalhes do jogo ────────────────────────────────────────────
            DetalhesJogoTM? detalhes = null;
            if (jogoWeb.PlacarCasa.HasValue && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                _logger.LogInformation("[IncluirJogo] Buscando detalhes: {Link}", jogoWeb.LinkDetalhes);
                await Task.Delay(2000, ct);
                detalhes = await transfermarkt.BuscarDetalhesJogoAsync(jogoWeb.LinkDetalhes, ct);
            }

            // ── Resolve formações ─────────────────────────────────────────────────
            var nomeFCasa = detalhes?.FormacaoCasa ?? jogoWeb.FormacaoCasa;
            var nomeFVis = detalhes?.FormacaoVisitante ?? jogoWeb.FormacaoVisitante;

            var formacaoCasa = await transfermarkt.ObterOuCriarFormacao(context, nomeFCasa, ct);
            var formacaoVisitante = await transfermarkt.ObterOuCriarFormacao(context, nomeFVis, ct);

            // ── Cria o jogo ───────────────────────────────────────────────────────
            var jogo = new Jogo
            {
                CompeticaoId = competicao.Id,
                TimeCasa = timeCasa,
                TimeVisitante = timeVisitante,
                Data = dataJogo.HasValue ? DateTime.SpecifyKind(dataJogo.Value, DateTimeKind.Utc) : null,
                Rodada = jogoWeb.Rodada,
                PlacarCasa = detalhes?.PlacarCasa ?? jogoWeb.PlacarCasa,
                PlacarVisitante = detalhes?.PlacarVisitante ?? jogoWeb.PlacarVisitante,
                Grupo = jogoWeb.Grupo,
                Status = jogoWeb.PlacarCasa.HasValue ? "Finalizado" : "Agendado",
                Atualizado = jogoWeb.PlacarCasa.HasValue ? 1 : 0,
                FormacaoCasaId = formacaoCasa.Id,
                FormacaoVisitanteId = formacaoVisitante.Id,
                LinkDetalhes = jogoWeb.LinkDetalhes
            };

            context.Jogos.Add(jogo);
            await context.SaveChangesAsync(ct);

            // ── Log de inclusão do jogo ───────────────────────────────────────────
            RegistrarLog(context, cicloId, "Jogo", "Criado",
                competicaoNome: competicao.Nome,
                timeNome: $"{timeCasa.Nome} × {timeVisitante.Nome}",
                jogoDescricao: jogoWeb.Rodada > 0
                    ? $"Rodada {jogoWeb.Rodada} | {dataJogo?.ToString("dd/MM/yyyy") ?? "sem data"}"
                    : dataJogo?.ToString("dd/MM/yyyy") ?? "sem data",
                detalhes: jogo.PlacarCasa.HasValue
                    ? $"Placar: {jogo.PlacarCasa}×{jogo.PlacarVisitante} | Status: {jogo.Status}"
                    : $"Status: {jogo.Status}");

            // ── Escalações ────────────────────────────────────────────────────────
            if (detalhes != null &&
                (detalhes.EscalacaoInicialCasa.Any() || detalhes.EscalacaoInicialVisitante.Any()))
            {
                var posicoesCasa = await context.PosicoesFormacao
                    .Where(p => p.FormacaoId == formacaoCasa.Id)
                    .OrderBy(p => p.Ordem)
                    .ToListAsync(ct);

                var posicoesVis = await context.PosicoesFormacao
                    .Where(p => p.FormacaoId == formacaoVisitante.Id)
                    .OrderBy(p => p.Ordem)
                    .ToListAsync(ct);

                // Fallback: gera posições genéricas a partir do nome da formação
                // Garante normalização correta mesmo sem cadastro de posições no banco
                if (!posicoesCasa.Any())
                    posicoesCasa = GerarPosicoesGenericas(nomeFCasa, formacaoCasa.Id);

                if (!posicoesVis.Any())
                    posicoesVis = GerarPosicoesGenericas(nomeFVis, formacaoVisitante.Id);

                await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialCasa,
                    timeCasa, true, "INICIAL", posicoesCasa, cicloId, ct);

                await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialVisitante,
                    timeVisitante, false, "INICIAL", posicoesVis, cicloId, ct);

                var finalCasa = detalhes.EscalacaoFinalCasa.Any()
                    ? detalhes.EscalacaoFinalCasa
                    : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();

                var finalVis = detalhes.EscalacaoFinalVisitante.Any()
                    ? detalhes.EscalacaoFinalVisitante
                    : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();

                await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalCasa,
                    timeCasa, true, "FINAL", posicoesCasa, cicloId, ct);

                await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalVis,
                    timeVisitante, false, "FINAL", posicoesVis, cicloId, ct);
            }
            else
            {
                transfermarkt.AdicionarEscalacaoComPosicoes(context, jogo, formacaoCasa, true);
                transfermarkt.AdicionarEscalacaoComPosicoes(context, jogo, formacaoVisitante, false);
            }

            // ── Eventos (gols, assistências, cartões) ─────────────────────────────
            if (detalhes?.Eventos.Any() == true)
            {
                var (gols, assists, cartoes) = await transfermarkt
                     .ImportarEventosPorLinkJogadorAsync(context, jogo, jogo.LinkDetalhes!, ct);
                _logger.LogInformation(
                "[Eventos] Jogo {Id}: {G} gols, {A} assistências, {C} cartões",
                jogo.Id, gols, assists, cartoes);
            }

            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[IncluirJogo] Incluído: {Casa} x {Vis} | Rodada {R} | {Data} | Escalação: {EscType}",
                timeCasa.Nome, timeVisitante.Nome,
                jogoWeb.Rodada,
                dataJogo?.ToString("dd/MM/yyyy HH:mm") ?? "sem data",
                detalhes != null ? "com jogadores e eventos" : "slots vazios");
        }

        private async Task ReimportarEscalacoesSeNecessario(
            FutebolContext context,
            Jogo jogo,
            TransfermarktJogoInfo jogoWeb,
            Time timeCasa,
            Time timeVisitante,
            Guid cicloId,
            TransfermarktService transfermarkt,
            CancellationToken ct)
        {
            // Só re-importa se o jogo está finalizado e tem link de detalhes
            if (!jogoWeb.PlacarCasa.HasValue || string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
                return;

            var temVisitante = await context.Escalacoes
                .AnyAsync(e => e.JogoId == jogo.Id && !e.IsTimeCasa && e.JogadorId != null, ct);
            if (temVisitante) return;

            _logger.LogInformation("[ReimporEscalacao] Jogo {Casa}x{Vis} sem escalação visitante — buscando: {Link}",
                timeCasa.Nome, timeVisitante.Nome, jogoWeb.LinkDetalhes);

            await Task.Delay(2000, ct);
            var detalhes = await transfermarkt.BuscarDetalhesJogoAsync(jogoWeb.LinkDetalhes, ct);
            if (detalhes == null || !detalhes.EscalacaoInicialVisitante.Any()) return;

            // Remove slots sem jogador (círculos vazios)
            var slotsVazios = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.JogadorId == null)
                .ToListAsync(ct);
            context.Escalacoes.RemoveRange(slotsVazios);

            // Atualiza formações se vieram do detalhe
            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoCasa))
            {
                var fc = await transfermarkt.ObterOuCriarFormacao(context, detalhes.FormacaoCasa, ct);
                jogo.FormacaoCasaId = fc.Id;
            }
            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoVisitante))
            {
                var fv = await transfermarkt.ObterOuCriarFormacao(context, detalhes.FormacaoVisitante, ct);
                jogo.FormacaoVisitanteId = fv.Id;
            }

            var posicoesCasa = await context.PosicoesFormacao
                .Where(p => p.FormacaoId == jogo.FormacaoCasaId)
                .OrderBy(p => p.Ordem).ToListAsync(ct);
            var posicoesVis = await context.PosicoesFormacao
                .Where(p => p.FormacaoId == jogo.FormacaoVisitanteId)
                .OrderBy(p => p.Ordem).ToListAsync(ct);

            if (!posicoesCasa.Any())
                posicoesCasa = GerarPosicoesGenericas(detalhes.FormacaoCasa, jogo.FormacaoCasaId ?? 0);
            if (!posicoesVis.Any())
                posicoesVis = GerarPosicoesGenericas(detalhes.FormacaoVisitante, jogo.FormacaoVisitanteId ?? 0);

            // Re-importa casa se também estiver vazia
            var temCasa = await context.Escalacoes
                .AnyAsync(e => e.JogoId == jogo.Id && e.IsTimeCasa && e.JogadorId != null, ct);
            if (!temCasa && detalhes.EscalacaoInicialCasa.Any())
            {
                await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialCasa,
                    timeCasa, true, "INICIAL", posicoesCasa, cicloId, ct);
                var finalCasa = detalhes.EscalacaoFinalCasa.Any()
                    ? detalhes.EscalacaoFinalCasa
                    : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();
                await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalCasa,
                    timeCasa, true, "FINAL", posicoesCasa, cicloId, ct);
            }

            await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialVisitante,
                timeVisitante, false, "INICIAL", posicoesVis, cicloId, ct);
            var finalVis = detalhes.EscalacaoFinalVisitante.Any()
                ? detalhes.EscalacaoFinalVisitante
                : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();
            await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalVis,
                timeVisitante, false, "FINAL", posicoesVis, cicloId, ct);

            await context.SaveChangesAsync(ct);

            // Re-importa eventos após ter os jogadores criados
            if (detalhes.Eventos.Any() && !string.IsNullOrWhiteSpace(jogo.LinkDetalhes))
            {
                var (gols, assists, cartoes) = await transfermarkt
                    .ImportarEventosPorLinkJogadorAsync(context, jogo, jogo.LinkDetalhes!, ct);
                _logger.LogInformation("[ReimporEscalacao] Eventos: {G} gols, {A} assists, {C} cartões",
                    gols, assists, cartoes);
                await context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("[ReimporEscalacao] Escalações salvas: {Casa}x{Vis}",
                timeCasa.Nome, timeVisitante.Nome);
        }

        // ── Força re-importação completa da escalação de um jogo (ignora dados existentes) ──
        public async Task<(bool Ok, string Mensagem)> ForcarReimportarEscalacaoAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            if (jogo == null)
                return (false, $"Jogo {jogoId} não encontrado.");

            if (string.IsNullOrWhiteSpace(jogo.LinkDetalhes))
                return (false, "Jogo sem link do Transfermarkt — re-importação não disponível.");

            _logger.LogInformation("[ForcarReimport] Iniciando re-importação jogo {Id}: {Casa}x{Vis}",
                jogoId, jogo.TimeCasa?.Nome, jogo.TimeVisitante?.Nome);

            // 1. Busca dados do Transfermarkt
            await Task.Delay(1500, ct);
            var detalhes = await BuscarDetalhesJogoAsync(jogo.LinkDetalhes, ct);
            if (detalhes == null)
                return (false, "Não foi possível obter detalhes do Transfermarkt.");

            if (!detalhes.EscalacaoInicialCasa.Any() && !detalhes.EscalacaoInicialVisitante.Any())
                return (false, "Transfermarkt não retornou escalação para este jogo.");

            // 2. Remove TODAS as escalações existentes (com e sem jogador)
            var escalacoes = await context.Escalacoes
                .Where(e => e.JogoId == jogoId)
                .ToListAsync(ct);
            context.Escalacoes.RemoveRange(escalacoes);
            await context.SaveChangesAsync(ct);

            // 3. Remove gols, cartões e assistências para não duplicar
            var gols = await context.Gols.Where(g => g.JogoId == jogoId).ToListAsync(ct);
            context.Gols.RemoveRange(gols);
            var cartoes = await context.Cartoes.Where(c => c.JogoId == jogoId).ToListAsync(ct);
            context.Cartoes.RemoveRange(cartoes);
            var assists = await context.Assistencias.Where(a => a.JogoId == jogoId).ToListAsync(ct);
            context.Assistencias.RemoveRange(assists);
            await context.SaveChangesAsync(ct);

            // 4. Atualiza formações se vieram do detalhe
            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoCasa))
            {
                var fc = await ObterOuCriarFormacao(context, detalhes.FormacaoCasa, ct);
                jogo.FormacaoCasaId = fc.Id;
            }
            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoVisitante))
            {
                var fv = await ObterOuCriarFormacao(context, detalhes.FormacaoVisitante, ct);
                jogo.FormacaoVisitanteId = fv.Id;
            }

            // Atualiza placar se vier
            if (detalhes.PlacarCasa.HasValue)
            {
                jogo.PlacarCasa = detalhes.PlacarCasa;
                jogo.PlacarVisitante = detalhes.PlacarVisitante;
                jogo.Status = "Finalizado";
            }

            // 5. Carrega slots de formação
            var posicoesCasa = await context.PosicoesFormacao
                .Where(p => p.FormacaoId == jogo.FormacaoCasaId)
                .OrderBy(p => p.Ordem).ToListAsync(ct);
            var posicoesVis = await context.PosicoesFormacao
                .Where(p => p.FormacaoId == jogo.FormacaoVisitanteId)
                .OrderBy(p => p.Ordem).ToListAsync(ct);

            if (!posicoesCasa.Any())
                posicoesCasa = GerarPosicoesGenericas(detalhes.FormacaoCasa, jogo.FormacaoCasaId ?? 0);
            if (!posicoesVis.Any())
                posicoesVis = GerarPosicoesGenericas(detalhes.FormacaoVisitante, jogo.FormacaoVisitanteId ?? 0);

            var cicloId = Guid.NewGuid();

            // 6. Re-importa escalações (INICIAL + FINAL para casa e visitante)
            await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialCasa,
                jogo.TimeCasa!, true, "INICIAL", posicoesCasa, cicloId, ct);

            await AdicionarEscalacoesComJogadoresAsync(context, jogo, detalhes.EscalacaoInicialVisitante,
                jogo.TimeVisitante!, false, "INICIAL", posicoesVis, cicloId, ct);

            var finalCasa = detalhes.EscalacaoFinalCasa.Any()
                ? detalhes.EscalacaoFinalCasa
                : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();
            var finalVis = detalhes.EscalacaoFinalVisitante.Any()
                ? detalhes.EscalacaoFinalVisitante
                : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();

            await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalCasa,
                jogo.TimeCasa!, true, "FINAL", posicoesCasa, cicloId, ct);
            await AdicionarEscalacoesComJogadoresAsync(context, jogo, finalVis,
                jogo.TimeVisitante!, false, "FINAL", posicoesVis, cicloId, ct);

            await context.SaveChangesAsync(ct);

            // 7. Re-importa eventos
            if (detalhes.Eventos.Any())
            {
                var (g, a, c2) = await ImportarEventosPorLinkJogadorAsync(
                    context, jogo, jogo.LinkDetalhes!, ct);
                _logger.LogInformation("[ForcarReimport] Eventos: {G} gols, {A} assists, {C} cartões", g, a, c2);
                await context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("[ForcarReimport] Concluído jogo {Id}", jogoId);
            return (true, $"Re-importação concluída — " +
                $"{detalhes.EscalacaoInicialCasa.Count(x => x.Titular)} titulares casa, " +
                $"{detalhes.EscalacaoInicialVisitante.Count(x => x.Titular)} titulares visitante.");
        }

        public async Task CorrigirDatasJogos(FutebolContext context, TransfermarktService transfermarkt, CancellationToken ct)
        {
            var jogos = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => !string.IsNullOrEmpty(j.LinkDetalhes))
                .ToListAsync(ct);

            foreach (var jogo in jogos)
            {
                try
                {
                    var novaData = await transfermarkt.BuscarDataJogoPorLink(jogo.LinkDetalhes, ct);
                    if (novaData.HasValue)
                    {
                        var dataPersistida = DateTime.SpecifyKind(novaData.Value, DateTimeKind.Utc);
                        if (!jogo.Data.HasValue || jogo.Data.Value != dataPersistida)
                        {
                            jogo.Data = dataPersistida;
                            _logger.LogInformation("[CorrigirDatas] Atualizado jogo {Casa} x {Vis} para {Data}",
                                jogo.TimeCasa.Nome, jogo.TimeVisitante.Nome, jogo.Data?.ToString("dd/MM/yyyy HH:mm"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CorrigirDatas] Erro ao atualizar data do jogo {Id}", jogo.Id);
                }
            }

            await context.SaveChangesAsync(ct);
        }
        //Copa do Brasil
        public async Task<List<TransfermarktJogoInfo>> BuscarJogosCopaPorLink(
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

        public List<TransfermarktJogoInfo> ExtrairJogosDaRodada(string html, int rodada)
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

        public List<TransfermarktEventoInfo> ExtrairEventosDoJogo(HtmlDocument doc)
        {
            var eventos = new List<TransfermarktEventoInfo>();

            // Seleciona os blocos de eventos do Transfermarkt
            var eventosNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'sb-aktion')]");

            if (eventosNodes == null)
                return eventos;

            foreach (var node in eventosNodes)
            {
                // Determina se o evento está na coluna da casa ou visitante
                bool isCasa = node.GetClasses().Contains("sb-aktion-heim");
                bool isVisitante = node.GetClasses().Contains("sb-aktion-gast");

                // Extrai informações básicas
                var minuto = ExtrairMinuto(node);
                var tipoEvento = ExtrairTipoEvento(node);
                var jogadorNome = ExtrairJogadorNome(node);
                var jogadorLink = ExtrairJogadorLink(node);
                var assistenteNome = ExtrairAssistenteNome(node);
                var assistenteLink = ExtrairAssistenteLink(node);
                var detalhe = ExtrairDetalhe(node);

                var ev = new TransfermarktEventoInfo
                {
                    Tipo = tipoEvento,
                    JogadorNome = jogadorNome,
                    JogadorLink = jogadorLink,
                    AssistenteNome = assistenteNome,
                    AssistenteLink = assistenteLink,
                    Minuto = minuto,
                    Detalhe = detalhe,
                    IsTimeCasa = isCasa // ✅ aqui está a chave
                };

                eventos.Add(ev);
            }

            return eventos;
        }

        private int ExtrairMinuto(HtmlNode node)
        {
            // Exemplo: "45'" ou "90+2'"
            var minutoNode = node.SelectSingleNode(".//span[contains(@class,'sb-aktion-uhr')]");
            if (minutoNode == null) return 0;

            var texto = minutoNode.InnerText.Trim();
            texto = texto.Replace("'", "").Replace("+", "");

            if (int.TryParse(texto, out var minuto))
                return minuto;

            return 0;
        }

        private string ExtrairTipoEvento(HtmlNode node)
        {
            // Transfermarkt usa ícones para diferenciar os eventos
            if (node.InnerHtml.Contains("icon-tor")) return "Gol";
            if (node.InnerHtml.Contains("icon-gelb")) return "Cartao";
            if (node.InnerHtml.Contains("icon-rot")) return "Cartao";
            if (node.InnerHtml.Contains("icon-auswechslung")) return "Substituicao";

            return "Outro";
        }

        private string? ExtrairJogadorNome(HtmlNode node)
        {
            var jogadorNode = node.SelectSingleNode(".//a[contains(@class,'spielprofil_tooltip')]");
            return jogadorNode?.InnerText.Trim();
        }

        private string? ExtrairJogadorLink(HtmlNode node)
        {
            var jogadorNode = node.SelectSingleNode(".//a[contains(@class,'spielprofil_tooltip')]");
            return jogadorNode?.GetAttributeValue("href", null);
        }

        private string? ExtrairAssistenteNome(HtmlNode node)
        {
            var assistNode = node.SelectSingleNode(".//span[contains(@class,'assist')]/a");
            return assistNode?.InnerText.Trim();
        }

        private string? ExtrairAssistenteLink(HtmlNode node)
        {
            var assistNode = node.SelectSingleNode(".//span[contains(@class,'assist')]/a");
            return assistNode?.GetAttributeValue("href", null);
        }

        private string? ExtrairDetalhe(HtmlNode node)
        {
            // Exemplo: "Cartão Amarelo", "Cartão Vermelho"
            var detalheNode = node.SelectSingleNode(".//span[contains(@class,'sb-aktion-karte')]");
            return detalheNode?.InnerText.Trim();
        }

        public async Task PersistirEventosAsync(
        FutebolContext context,
        Jogo jogo,
        List<TransfermarktEventoInfo> eventos,
        CancellationToken ct)
            {
                foreach (var ev in eventos)
                {
                    var nomeJogador = ev.JogadorNome ?? ev.AssistenteNome;

                    var jogador = await ResolverJogadorAsync(
                        context,
                        nomeJogador,
                        ev.JogadorLink ?? ev.AssistenteLink,
                        ev.IsTimeCasa ? jogo.TimeCasa.Id : jogo.TimeVisitante.Id,
                        ct);

                    // 🔹 Log detalhado para conferência
                    _logger.LogInformation("[Evento] {Tipo} - {Jogador} - Min {Minuto} - Casa? {IsCasa}",
                        ev.Tipo, nomeJogador, ev.Minuto, ev.IsTimeCasa);

                    if (jogador == null)
                    {
                        _logger.LogWarning("[Evento] Jogador não encontrado: {Jogador} ({Link})",
                            nomeJogador, ev.JogadorLink ?? ev.AssistenteLink);
                        continue;
                    }

                    switch (ev.Tipo)
                    {
                        case "Gol":
                            context.Gols.Add(new Gol
                            {
                                Jogo = jogo,
                                JogadorId = jogador.Id,
                                Minuto = ev.Minuto,
                                Contra = ev.Contra
                            });
                            break;

                        case "Assistencia":
                            context.Assistencias.Add(new Assistencia
                            {
                                Jogo = jogo,
                                JogadorId = jogador.Id,
                                Minuto = ev.Minuto
                            });
                            break;

                        case "Cartao":
                            context.Cartoes.Add(new Cartao
                            {
                                Jogo = jogo,
                                JogadorId = jogador.Id,
                                Minuto = ev.Minuto,
                                Tipo = ev.Detalhe ?? "Amarelo"
                            });
                            break;

                        default:
                            _logger.LogWarning("[Evento] Tipo desconhecido: {Tipo}", ev.Tipo);
                            break;
                    }
                }

                await context.SaveChangesAsync(ct);
            }

        public static DateTime? ParseData(string texto)
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
                    return ValidarAnoDt(dt);
                }
            }
            return null;
        }

        // Valida e corrige ano de 2 dígitos; retorna null se fora do intervalo 2000-2099.
        private static DateTime? ValidarAnoDt(DateTime dt)
        {
            if (dt.Year < 100)
                dt = new DateTime(2000 + dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            return (dt.Year >= 2000 && dt.Year <= 2099)
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : null;
        }

        // ── Resolve jogador por link do Transfermarkt ─────────────────────────────
        /// <summary>
        /// Extrai o ID numérico do Transfermarkt de qualquer link do perfil.
        /// Ex.: /matheus-pereira/leistungsdatendetails/spieler/225984/... → 225984
        /// </summary>
        private static long? ExtrairIdDoLinkQualquer(string? href)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            var m = Regex.Match(href, @"/spieler/(\d+)");
            return m.Success ? long.Parse(m.Groups[1].Value) : null;
        }

        private async Task<(Jogador? jogador, bool isCasa)?> ResolverJogadorPorLinkAsync(
        FutebolContext context,
        string? href,
        int timeCasaId,
        int timeVisitanteId,
        CancellationToken ct)
            {
                if (string.IsNullOrWhiteSpace(href))
                {
                    _logger.LogWarning("[ResolverPorLink] Href vazio.");
                    return null;
                }

                _logger.LogInformation("[ResolverPorLink] Procurando jogador pelo link: {Href}", href);

                var tmId = ExtrairIdDoLinkQualquer(href);
                if (!tmId.HasValue)
                {
                    _logger.LogWarning("[ResolverPorLink] Não foi possível extrair ID do link: {Href}", href);
                    return null;
                }

                _logger.LogInformation("[ResolverPorLink] ID extraído do link: {Id}", tmId);

                // 1. Busca pelo campo linktransfermarket
                var porLink = await context.Jogadores
                    .FirstOrDefaultAsync(j =>
                        j.linktransfermarket != null &&
                        j.linktransfermarket.Contains($"/spieler/{tmId}"), ct);

                if (porLink != null)
                {
                    bool isCasa = porLink.TimeId == timeCasaId;
                    _logger.LogInformation(
                        "[ResolverPorLink] Jogador encontrado: {Nome} (TimeId={TimeId}) | CasaId={CasaId} | VisitanteId={VisId} | isCasa={IsCasa}",
                        porLink.Nome, porLink.TimeId, timeCasaId, timeVisitanteId, isCasa);
                    return (porLink, isCasa);
                }

                // 2. Fallback: busca por IdApi
                var porIdApi = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.IdApi == tmId, ct);

                if (porIdApi != null)
                {
                    bool isCasa = porIdApi.TimeId == timeCasaId;
                    _logger.LogInformation(
                        "[ResolverPorLink] Jogador encontrado por IdApi: {Nome} (TimeId={TimeId}) | CasaId={CasaId} | VisitanteId={VisId} | isCasa={IsCasa}",
                        porIdApi.Nome, porIdApi.TimeId, timeCasaId, timeVisitanteId, isCasa);
                    return (porIdApi, isCasa);
                }

                _logger.LogWarning(
                    "[ResolverPorLink] ID {Id} não encontrado no banco. Link={Href}",
                    tmId, href);
                return null;
            }

        private static string MapearPosicaoParaNome(string? p) => p?.ToLower() switch
        {
            var s when s?.Contains("gol") == true ||
                        s?.Contains("keeper") == true => "Goleiro",
            var s when s?.Contains("zagueiro") == true ||
                        s?.Contains("defesa") == true => "Zagueiro",
            var s when s?.Contains("lateral") == true => "Lateral",
            var s when s?.Contains("volante") == true => "Volante",
            var s when s?.Contains("meio") == true ||
                        s?.Contains("meia") == true => "Meio-campo",
            var s when s?.Contains("atacante") == true ||
                        s?.Contains("centroavante") == true => "Atacante",
            var s when s?.Contains("ponta") == true => "Ponta",
            _ => "Meio-campo"
        };
        // ── Helpers de posição ────────────────────────────────────────────────────
        /// <summary>
        /// Importa gols, assistências e cartões de uma página de detalhe do jogo
        /// resolvendo os jogadores pelo link Transfermarkt (mais confiável que busca por nome).
        /// </summary>
        public async Task<(int gols, int assistencias, int cartoes)> ImportarEventosPorLinkJogadorAsync(
        FutebolContext context,
        Jogo jogo,
        string urlDetalhes,
        CancellationToken ct = default)
    {

        _logger.LogInformation("[ImportarEventos] Buscando detalhes: {Url}", urlDetalhes);

        if (string.IsNullOrWhiteSpace(urlDetalhes))
            return (0, 0, 0);

        if (!urlDetalhes.StartsWith("http"))
            urlDetalhes = "https://www.transfermarkt.com.br" + urlDetalhes;

        string html;
        try
        {
            await Task.Delay(2000, ct);
            html = await _httpClient.GetStringAsync(urlDetalhes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImportarEventos] Erro ao buscar: {Url}", urlDetalhes);
            return (0, 0, 0);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        int totalGols = 0, totalAssist = 0, totalCartoes = 0;

        // ── GOL (div#sb-tore li com ação de gol) ─────────────────────────────
        var toreDiv = doc.DocumentNode.SelectSingleNode("//*[@id='sb-tore']");
        if (toreDiv != null)
        {
            var itensGol = toreDiv.SelectNodes(".//li[contains(@class,'sb-aktion')]");
            if (itensGol != null)
            {
                foreach (var li in itensGol)
                {
                    var minuto = ExtrairMinutoDoNo(li);
                    var textoLi = li.InnerText.ToLowerInvariant();
                    bool contra = (textoLi.Contains("gol contra") &&
                                    !textoLi.Contains("contra-ataque") &&
                                    !textoLi.Contains("contra ataque"))
                                || li.InnerHtml.Contains("Eigentor");

                    // Link do marcador fica em div.sb-aktion-aktion a[1]
                    var acaoDiv = li.SelectSingleNode(".//div[contains(@class,'sb-aktion-aktion')]");
                    if (acaoDiv == null) continue;

                    var linksAcao = acaoDiv.SelectNodes(".//a[@href]");
                    if (linksAcao == null || linksAcao.Count == 0) continue;

                    var linkMarcador = linksAcao[0].GetAttributeValue("href", "");

                    var resultMarcador = await ResolverJogadorPorLinkAsync(
                        context, linkMarcador,
                        jogo.TimeCasaId, jogo.TimeVisitanteId, ct);

                    if (resultMarcador?.jogador != null)
                    {
                        // Evita duplicar gols
                        bool jaExiste = await context.Gols.AnyAsync(g =>
                            g.JogoId == jogo.Id &&
                            g.JogadorId == resultMarcador.Value.jogador.Id &&
                            g.Minuto == minuto, ct);

                        if (!jaExiste)
                        {
                            context.Gols.Add(new Gol
                            {
                                JogoId = jogo.Id,
                                JogadorId = resultMarcador.Value.jogador.Id,
                                Minuto = minuto,
                                Contra = contra
                            });
                            totalGols++;
                            _logger.LogInformation(
                                "[ImportarEventos] Gol: {Nome} min={Min} contra={C}",
                                resultMarcador.Value.jogador.Nome, minuto, contra);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[ImportarEventos] Gol ignorado (jogador não encontrado): link={L}",
                            linkMarcador);
                    }

                    // Assistência: segundo link em "Assistência: <a>"
                    if (linksAcao.Count >= 2)
                    {
                        var textoAcao = HtmlEntity.DeEntitize(acaoDiv.InnerText);
                        if (textoAcao.Contains("Assistência") || textoAcao.Contains("Assistencia"))
                        {
                            var linkAssist = linksAcao[1].GetAttributeValue("href", "");
                            var resultAssist = await ResolverJogadorPorLinkAsync(
                                context, linkAssist,
                                jogo.TimeCasaId, jogo.TimeVisitanteId, ct);

                            if (resultAssist?.jogador != null)
                            {
                                bool jaExisteAssist = await context.Assistencias.AnyAsync(a =>
                                    a.JogoId == jogo.Id &&
                                    a.JogadorId == resultAssist.Value.jogador.Id &&
                                    a.Minuto == minuto, ct);

                                if (!jaExisteAssist)
                                {
                                    context.Assistencias.Add(new Assistencia
                                    {
                                        JogoId = jogo.Id,
                                        JogadorId = resultAssist.Value.jogador.Id,
                                        Minuto = minuto
                                    });
                                    totalAssist++;
                                    _logger.LogInformation(
                                        "[ImportarEventos] Assistência: {Nome} min={Min}",
                                        resultAssist.Value.jogador.Nome, minuto);
                                }
                            }
                        }
                    }
                }
            }
        }

        // ── CARTÕES (div#sb-karten li) ────────────────────────────────────────
        var kartenDiv = doc.DocumentNode.SelectSingleNode("//*[@id='sb-karten']");
        if (kartenDiv != null)
        {
            var itensCartao = kartenDiv.SelectNodes(".//li[contains(@class,'sb-aktion')]");
            if (itensCartao != null)
            {
                foreach (var li in itensCartao)
                {
                    var minuto = ExtrairMinutoDoNo(li);

                    // Tipo pelo ícone: sb-sprite-gelb = amarelo, sb-rot = vermelho
                    var outerHtml = li.OuterHtml;
                    string tipo = outerHtml.Contains("sb-rot") || outerHtml.Contains("Rot")
                        ? "Vermelho" : "Amarelo";

                    // Link do jogador
                    var linkNode = li.SelectSingleNode(
                        ".//div[contains(@class,'sb-aktion-spielerbild')]//a[@href]") ??
                        li.SelectSingleNode(".//div[contains(@class,'sb-aktion-aktion')]//a[@href]");

                    if (linkNode == null) continue;

                    var linkCartao = linkNode.GetAttributeValue("href", "");
                    var resultCartao = await ResolverJogadorPorLinkAsync(
                        context, linkCartao,
                        jogo.TimeCasaId, jogo.TimeVisitanteId, ct);

                    if (resultCartao?.jogador != null)
                    {
                        bool jaExiste = await context.Cartoes.AnyAsync(c =>
                            c.JogoId == jogo.Id &&
                            c.JogadorId == resultCartao.Value.jogador.Id &&
                            c.Minuto == minuto &&
                            c.Tipo == tipo, ct);

                        if (!jaExiste)
                        {
                            context.Cartoes.Add(new Cartao
                            {
                                JogoId = jogo.Id,
                                JogadorId = resultCartao.Value.jogador.Id,
                                Minuto = minuto,
                                Tipo = tipo
                            });
                            totalCartoes++;
                            _logger.LogInformation(
                                "[ImportarEventos] Cartão {Tipo}: {Nome} min={Min}",
                                tipo, resultCartao.Value.jogador.Nome, minuto);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[ImportarEventos] Cartão ignorado (jogador não encontrado): link={L}",
                            linkCartao);
                    }
                }
            }
        }

        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[ImportarEventos] Jogo {Id} finalizado: {G} gols, {A} assist, {C} cartões",
            jogo.Id, totalGols, totalAssist, totalCartoes);

        return (totalGols, totalAssist, totalCartoes);
    }

        // Helper separado para extrair minuto de um <li> de evento
        private static int ExtrairMinutoDoNo(HtmlNode no)
        {
            // SelectSingleNode com "|" (union) é instável no HtmlAgilityPack — usa cadeia de fallback
            var span =
                no.SelectSingleNode(".//span[contains(@class,'sb-sprite-uhr-klein')]")
                ?? no.SelectSingleNode(".//span[contains(@class,'sb-aktion-uhr')]")
                ?? no.SelectSingleNode(".//div[contains(@class,'sb-aktion-uhr')]")
                ?? no.SelectSingleNode(".//span[contains(@class,'uhr')]")
                ?? no.SelectSingleNode(".//div[contains(@class,'uhr')]");

            if (span != null)
            {
                var texto = HtmlEntity.DeEntitize(span.InnerText.Trim());
                var m = Regex.Match(texto, @"(\d+)");
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }

            // Fallback: busca padrão "NN'" ou "NN." no texto completo do nó
            var textoNo = HtmlEntity.DeEntitize(no.InnerText);
            var mFallback = Regex.Match(textoNo, @"(\d{1,3})['\.]");
            return mFallback.Success ? int.Parse(mFallback.Groups[1].Value) : 0;
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
