using System.Globalization;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    public record ResultadoFifa(bool Ok, string Mensagem, string? IdMatch = null);

    /// <summary>
    /// Uma temporada (edição) de competição no catálogo da FIFA — o que a tela de
    /// cadastro mostra depois de validar o link.
    /// </summary>
    public record SeasonFifa(
        string IdCompetition, string IdSeason, string Nome, int Ano,
        DateTime? Inicio, DateTime? Fim, string? LogoUrl);

    /// <summary>
    /// Uma competição do catálogo da FIFA, como a tela de escolha a mostra.
    /// </summary>
    public record CompeticaoFifa(string Id, string Nome, bool Feminino, int AgeType);

    /// <summary>
    /// Integração com a API pública da FIFA (api.fifa.com/api/v3), a mesma que
    /// alimenta fifa.com.
    /// Link da competição no banco: "fifa:IDCOMPETITION:IDSEASON"  ex.: "fifa:108:291518"
    /// (108 = FIFA U-20 Women's World Cup, 291518 = edição Polônia 2026).
    ///
    /// Existe porque as competições de base e femininas da FIFA simplesmente não estão
    /// nas outras fontes: o Mundial Sub-20 Feminino não tem liga na api-football, e a
    /// ESPN cataloga o sub-20 masculino e o sub-17 feminino, mas não este
    /// (fifa.wworld.u20 devolve 400). Sem esta fonte a competição teria de ser digitada
    /// jogo a jogo.
    ///
    /// A API não pede chave nem token — responde JSON direto com um User-Agent de
    /// navegador. Para não virar abuso mesmo assim, todas as chamadas passam pela
    /// <see cref="_porta"/> (uma por vez, com intervalo mínimo) e por cache, no mesmo
    /// molde do EspnEstatisticasService.
    ///
    /// O que a FIFA cobre, e o que não cobre:
    ///   calendar/matches → tabela, placar, estádio, árbitro, grupo e fase;
    ///   live/football/…  → escalação com camisa e posição, técnicos, posse de bola;
    ///   timelines/…      → gols (com assistência), cartões e substituições.
    /// Estatística POR JOGADOR (passes, desarmes, finalizações individuais) não é
    /// publicada em endpoint nenhum da v3 — os paths de statistics respondem "null".
    /// Por isso não existe um FifaEstatisticasService: importar nota/estatística
    /// continua sendo trabalho do analista nesta competição.
    /// </summary>
    public class FifaService
    {
        private const string Base = "https://api.fifa.com/api/v3/";

        // Idioma das descrições. en-GB é o único que a FIFA preenche em todos os nós
        // (SeasonName, StageName, TypeLocalized); pt-BR volta com boa parte vazia.
        private const string Lingua = "en";

        // Uma requisição por vez para o host, com intervalo mínimo entre elas.
        // Estático porque o serviço é transiente (AddHttpClient<T>) e o que precisa ser
        // serializado é o acesso ao host, não a instância.
        private static readonly SemaphoreSlim _porta = new(1, 1);
        private static DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(700);

        private readonly HttpClient _http;
        private readonly ILogger<FifaService> _logger;
        private readonly IMemoryCache _cache;

        public FifaService(HttpClient http, ILogger<FifaService> logger, IMemoryCache cache)
        {
            _http = http;
            _http.BaseAddress = new Uri(Base);
            _http.Timeout = TimeSpan.FromSeconds(25);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            _logger = logger;
            _cache = cache;
        }

        // ── Parse do link "fifa:108:291518" ──────────────────────────────────

        public static bool IsFifaLink(string? link) =>
            link?.StartsWith("fifa:", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Os ids da FIFA são numéricos mas viajam como string na API (e algumas
        /// competições de clubes usam id alfanumérico), então não são convertidos —
        /// só validados como não-vazios e sem ':'.
        /// </summary>
        public static (string IdCompetition, string IdSeason) ParseLink(string link)
        {
            var partes = link.Split(':');
            if (partes.Length < 3 ||
                string.IsNullOrWhiteSpace(partes[1]) ||
                string.IsNullOrWhiteSpace(partes[2]))
                throw new ArgumentException(
                    $"Link inválido: {link}. Formato: fifa:IDCOMPETITION:IDSEASON");

            return (partes[1].Trim(), partes[2].Trim());
        }

        /// <summary>
        /// Endereço de uma partida dentro da FIFA. O IdStage não sai de lugar nenhum
        /// além do calendário, e os endpoints de escalação e de lances exigem os quatro
        /// ids na URL — por isso os quatro ficam gravados em Jogo.LinkDetalhes
        /// ("fifa:108:291518:291519:400022428") em vez de só o id da partida.
        /// </summary>
        public record PartidaRef(string Competicao, string Season, string Stage, string Match)
        {
            public override string ToString() => $"fifa:{Competicao}:{Season}:{Stage}:{Match}";
        }

        public static PartidaRef? RefDaPartida(string? linkDetalhes)
        {
            if (!IsFifaLink(linkDetalhes)) return null;

            var p = linkDetalhes!.Split(':');
            if (p.Length < 5 || p.Skip(1).Take(4).Any(string.IsNullOrWhiteSpace)) return null;

            return new PartidaRef(p[1].Trim(), p[2].Trim(), p[3].Trim(), p[4].Trim());
        }

        // ── Catálogo: validação do link no cadastro da competição ────────────

        /// <summary>
        /// A edição pedida, ou null quando a FIFA não conhece o par
        /// competição/temporada. Usado pelo formulário de Competições para não deixar
        /// salvar um link que nunca vai trazer jogo nenhum.
        /// </summary>
        public async Task<SeasonFifa?> BuscarSeasonAsync(
            string idCompetition, string idSeason, CancellationToken ct = default)
        {
            // O catálogo de edições muda de ano em ano: TTL longo, mas por competição.
            var doc = await BuscarJsonAsync(
                $"seasons?idCompetition={Uri.EscapeDataString(idCompetition)}&count=200&language={Lingua}",
                TimeSpan.FromHours(12), ct);
            if (doc == null) return null;

            using var _ = doc;
            if (!doc.RootElement.TryGetProperty("Results", out var results)) return null;

            foreach (var s in results.EnumerateArray())
            {
                if (Texto(s, "IdSeason") != idSeason) continue;

                var inicio = Data(s, "StartDate");
                var nome = Localizado(s, "Name") ?? $"Temporada {idSeason}";

                return new SeasonFifa(
                    idCompetition, idSeason,
                    // O ™ e as aspas tipográficas da FIFA não acrescentam nada ao nome
                    // que vai virar rótulo de tela.
                    nome.Replace("™", "").Replace("’", "'").Trim(),
                    inicio?.Year ?? DateTime.UtcNow.Year,
                    inicio, Data(s, "EndDate"),
                    await LogoDaSeasonAsync(idSeason, ct));
            }

            return null;
        }

        /// <summary>
        /// As competições organizadas pela própria FIFA (Mundiais adulto, de base e
        /// femininos), que são as que fazem sentido buscar por aqui: o resto do catálogo
        /// é liga nacional e continental, terreno da api-football.
        ///
        /// Futsal e beach soccer ficam de fora (FootballType != 0) — o modelo de jogo
        /// do sistema é o de campo.
        /// </summary>
        public async Task<List<CompeticaoFifa>> ListarCompeticoesAsync(CancellationToken ct = default)
        {
            var doc = await BuscarJsonAsync(
                $"competitions?language={Lingua}&count=500", TimeSpan.FromHours(24), ct);
            if (doc == null) return new();

            using var _ = doc;
            if (!doc.RootElement.TryGetProperty("Results", out var results)) return new();

            return results.EnumerateArray()
                .Where(c => (Texto(c, "IdOwner") ?? "").Contains("FIFA", StringComparison.OrdinalIgnoreCase)
                            && Numero(c, "FootballType") == 0)
                .Select(c => new CompeticaoFifa(
                    Texto(c, "IdCompetition") ?? "",
                    (Localizado(c, "Name") ?? "").Replace("™", "").Replace("’", "'").Trim(),
                    Numero(c, "Gender") == 2,
                    Numero(c, "AgeType")))
                .Where(c => c.Id.Length > 0 && c.Nome.Length > 0)
                .OrderBy(c => c.Nome, StringComparer.CurrentCulture)
                .ToList();
        }

        /// <summary>
        /// Lista as edições de uma competição, da mais recente para a mais antiga —
        /// para a tela de catálogo oferecer o IdSeason em vez de exigir que o usuário
        /// descubra o número na mão.
        /// </summary>
        public async Task<List<SeasonFifa>> ListarSeasonsAsync(
            string idCompetition, CancellationToken ct = default)
        {
            var doc = await BuscarJsonAsync(
                $"seasons?idCompetition={Uri.EscapeDataString(idCompetition)}&count=200&language={Lingua}",
                TimeSpan.FromHours(12), ct);
            if (doc == null) return new();

            using var _ = doc;
            if (!doc.RootElement.TryGetProperty("Results", out var results)) return new();

            return results.EnumerateArray()
                .Select(s =>
                {
                    var inicio = Data(s, "StartDate");
                    return new SeasonFifa(
                        idCompetition,
                        Texto(s, "IdSeason") ?? "",
                        (Localizado(s, "Name") ?? "").Replace("™", "").Replace("’", "'").Trim(),
                        inicio?.Year ?? 0, inicio, Data(s, "EndDate"), null);
                })
                .Where(s => s.IdSeason.Length > 0)
                .OrderByDescending(s => s.Inicio ?? DateTime.MinValue)
                .ToList();
        }

        /// <summary>
        /// A arte do torneio, quando já existe. A FIFA responde 200 com corpo VAZIO
        /// para edição que ainda não teve a identidade visual publicada (é o caso da
        /// Polônia 2026 hoje) — gravar essa URL deixaria um escudo quebrado na tela,
        /// então só volta o endereço quando vem imagem de verdade.
        /// </summary>
        private async Task<string?> LogoDaSeasonAsync(string idSeason, CancellationToken ct)
        {
            var url = $"{Base}picture/tournaments-sq-4/{Uri.EscapeDataString(idSeason)}";
            try
            {
                await AguardarPortaAsync(ct);
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return null;

                var tamanho = resp.Content.Headers.ContentLength;
                if (tamanho is null or 0) return null;

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Fifa] Falha ao conferir a arte da edição {Season}.", idSeason);
                return null;
            }
        }

        // ── Sincronização da competição ──────────────────────────────────────

        /// <summary>
        /// Traz a tabela inteira da edição: cria os times que faltam, insere os jogos
        /// novos e atualiza os que já existem (placar, data, estádio, árbitro, fase).
        ///
        /// Mesma assinatura de <see cref="ApiFootballService.SincronizarCompeticaoAsync"/>
        /// porque quem chama é o mesmo botão "Buscar jogos" da tela de Competições.
        ///
        /// Não importa escalação nem lances: isso é por partida e sai de
        /// FifaEscalacaoService / FifaEventosService, acionados pelo "Reimportar dados"
        /// do jogo. Puxar tudo aqui significaria 2 chamadas extras por jogo em toda
        /// sincronização, inclusive nos jogos que o analista nem abriu.
        /// </summary>
        public async Task<(int jogosProcessados, int timesCriados, int erros, List<string> avisos)>
            SincronizarCompeticaoAsync(
                FutebolContext context, Competicao competicao, CancellationToken ct = default)
        {
            int jogosProcessados = 0, timesCriados = 0, erros = 0;
            var avisos = new List<string>();

            if (!IsFifaLink(competicao.LinkTransfermarket))
            {
                avisos.Add("Link não é do formato fifa:IDCOMPETITION:IDSEASON.");
                return (0, 0, 0, avisos);
            }

            var (idComp, idSeason) = ParseLink(competicao.LinkTransfermarket!);

            // Mesma limpeza da api-football: a tela de logs mostra o ciclo atual, e
            // acumular ciclos antigos da mesma competição só polui a leitura.
            var logsAntigos = await context.TransfermarktSincronizacaoLogs
                .Where(l => l.CompeticaoNome == competicao.Nome)
                .ToListAsync(ct);
            context.TransfermarktSincronizacaoLogs.RemoveRange(logsAntigos);
            await context.SaveChangesAsync(ct);

            var cicloId = Guid.NewGuid();
            Log(context, cicloId, "Ciclo", "Iniciado", competicao.Nome,
                detalhes: $"Fonte: api.fifa.com | Link: {competicao.LinkTransfermarket}");

            var season = await BuscarSeasonAsync(idComp, idSeason, ct);
            if (season == null)
            {
                avisos.Add($"A FIFA não reconhece a edição {idSeason} da competição {idComp}.");
                Log(context, cicloId, "Erro", "Season", competicao.Nome, detalhes: avisos[^1]);
                await context.SaveChangesAsync(ct);
                return (0, 0, 1, avisos);
            }

            var partidas = await BuscarPartidasAsync(idComp, idSeason, ct);
            _logger.LogInformation("[Fifa] {Nome} ({Season}): {N} partidas no calendário.",
                competicao.Nome, season.Nome, partidas.Count);

            Log(context, cicloId, "Ciclo", "Calendário", competicao.Nome,
                detalhes: $"{partidas.Count} partidas encontradas em {season.Nome}");
            await context.SaveChangesAsync(ct);

            var aguardandoSorteio = 0;

            foreach (var p in partidas)
            {
                if (ct.IsCancellationRequested) break;

                // Jogo de mata-mata antes do sorteio: a FIFA já publica data, estádio e
                // horário, mas Home/Away vêm nulos e o confronto é só a vaga
                // ("B1 x B2", em PlaceHolderA/B). Não dá para criar o jogo sem os dois
                // times, e isso não é erro — é a tabela ainda não definida. Entra na
                // próxima sincronização, depois que a fase de grupos acabar.
                if (!TemTimes(p))
                {
                    aguardandoSorteio++;
                    continue;
                }

                var descricao = $"{Localizado(p, "Home", "TeamName")} x {Localizado(p, "Away", "TeamName")}";
                try
                {
                    var (timeCasa, casaCriado) = await ResolverOuCriarTimeAsync(
                        context, p.GetProperty("Home"), competicao, cicloId, ct);
                    if (casaCriado) timesCriados++;

                    var (timeVis, visCriado) = await ResolverOuCriarTimeAsync(
                        context, p.GetProperty("Away"), competicao, cicloId, ct);
                    if (visCriado) timesCriados++;

                    await IncluirOuAtualizarJogoAsync(
                        context, competicao, p, timeCasa, timeVis, season, cicloId, ct);

                    await context.SaveChangesAsync(ct);
                    jogosProcessados++;
                }
                catch (Exception ex)
                {
                    erros++;
                    avisos.Add($"{descricao}: {ex.Message}");
                    Log(context, cicloId, "Erro", "Partida", competicao.Nome,
                        jogoDescricao: descricao, detalhes: ex.Message);
                    _logger.LogWarning(ex, "[Fifa] Erro ao processar a partida {Id}.", Texto(p, "IdMatch"));
                }
            }

            if (aguardandoSorteio > 0)
                Log(context, cicloId, "Ciclo", "Aguardando sorteio", competicao.Nome,
                    detalhes: $"{aguardandoSorteio} partida(s) de mata-mata ainda sem as duas seleções definidas");

            Log(context, cicloId, "Ciclo", "Concluído", competicao.Nome,
                detalhes: $"{jogosProcessados} jogos | {timesCriados} times criados | {erros} erros" +
                          (aguardandoSorteio > 0 ? $" | {aguardandoSorteio} aguardando sorteio" : ""));
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[Fifa] Concluído: {J} jogos, {T} times criados, {E} erros.",
                jogosProcessados, timesCriados, erros);

            return (jogosProcessados, timesCriados, erros, avisos);
        }

        /// <summary>
        /// A partida já tem os dois times? Antes do sorteio da fase seguinte a FIFA
        /// manda Home/Away nulos (ou sem IdTeam) e só a vaga em PlaceHolderA/B.
        /// </summary>
        private static bool TemTimes(JsonElement p) =>
            new[] { "Home", "Away" }.All(lado =>
                p.TryGetProperty(lado, out var time) &&
                time.ValueKind == JsonValueKind.Object &&
                !string.IsNullOrWhiteSpace(Texto(time, "IdTeam")));

        /// <summary>O calendário completo da edição (fase de grupos e mata-mata juntos).</summary>
        private async Task<List<JsonElement>> BuscarPartidasAsync(
            string idComp, string idSeason, CancellationToken ct)
        {
            // TTL curto: sincronizar é ação explícita do usuário e costuma acontecer
            // logo depois de um jogo terminar — cachear por horas devolveria o placar
            // velho justamente na hora em que ele interessa.
            var doc = await BuscarJsonAsync(
                $"calendar/matches?idCompetition={Uri.EscapeDataString(idComp)}" +
                $"&idSeason={Uri.EscapeDataString(idSeason)}&count=500&language={Lingua}",
                TimeSpan.FromMinutes(2), ct);
            if (doc == null) return new();

            using var _ = doc;
            if (!doc.RootElement.TryGetProperty("Results", out var results)) return new();

            // Clone: o JsonDocument morre no fim deste método e os JsonElement
            // devolvidos precisam sobreviver ao laço de importação.
            return results.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        // ── Times ────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve (ou cria) o Time da seleção que a FIFA descreve no nó Home/Away.
        ///
        /// O nome carrega a CATEGORIA ("Brasil Sub-20 (F)") de propósito: a FIFA manda
        /// só "Brazil", igual à seleção principal masculina, e casar as duas juntaria
        /// no mesmo Time dois elencos que não têm nenhuma jogadora em comum — o perfil
        /// da atleta, as anotações e as estatísticas agregadas passariam a misturar
        /// sub-20 feminino com seleção principal.
        ///
        /// IdApi fica em 0 de propósito: aquele campo é o id da api-football, e os ids
        /// da FIFA vivem em outro espaço numérico — gravá-los ali faria um time da FIFA
        /// casar com um clube qualquer de mesmo id na próxima importação. O vínculo com
        /// a FIFA vai em Time.LinkTransfermarket ("fifa:1888622"), que é campo livre.
        /// </summary>
        private async Task<(Time Time, bool Criado)> ResolverOuCriarTimeAsync(
            FutebolContext context, JsonElement time, Competicao competicao,
            Guid cicloId, CancellationToken ct)
        {
            var idTeam = Texto(time, "IdTeam") ?? "";
            var vinculo = $"fifa:{idTeam}";
            var nome = NomeDaSelecao(time);

            var achado = await context.Times.FirstOrDefaultAsync(
                    t => t.LinkTransfermarket == vinculo, ct)
                ?? await context.Times.FirstOrDefaultAsync(t => t.Nome == nome, ct);

            var bandeira = Bandeira(time);

            if (achado != null)
            {
                if (string.IsNullOrWhiteSpace(achado.LinkTransfermarket) && idTeam.Length > 0)
                    achado.LinkTransfermarket = vinculo;

                // Time cadastrado antes de o país entrar no CountryHelper ficou com o
                // nome como a FIFA escreve ("China PR Sub-20 (F)", "DPR Korea Sub-20 (F)").
                // Traduzido o país, o cadastro acompanha — mesma correção que a
                // importação da api-football faz. Só quando o nome salvo AINDA é o da
                // FIFA: nome ajustado à mão pelo usuário não é sobrescrito.
                var original = Localizado(time, "TeamName");
                if (achado.Nome != nome && !string.IsNullOrWhiteSpace(original) &&
                    achado.Nome.Contains(original, StringComparison.OrdinalIgnoreCase))
                    achado.Nome = nome;

                if (string.IsNullOrWhiteSpace(achado.EscudoUrl) && bandeira != null)
                    achado.EscudoUrl = bandeira;
                if (competicao.EhSelecaoNacional && !achado.EhSelecao)
                    achado.EhSelecao = true;

                await context.SaveChangesAsync(ct);
                return (achado, false);
            }

            var formacaoPadrao = await context.Formacoes.FirstOrDefaultAsync(ct)
                ?? new Formacao { Nome = "4-3-3" };

            var novo = new Time
            {
                Nome              = nome,
                IdApi             = 0,
                LinkTransfermarket = idTeam.Length > 0 ? vinculo : null,
                EscudoUrl         = bandeira ?? "",
                Cidade            = "Importado",
                CorPrincipal      = "#000000",
                CorSecundaria     = "#FFFFFF",
                FormacaoPadraoId  = formacaoPadrao.Id,
                // Seleção de base continua sendo seleção: é o que separa
                // Jogador.SelecaoId de Jogador.TimeId (o clube) na importação.
                EhSelecao         = competicao.EhSelecaoNacional,
            };

            context.Times.Add(novo);
            await context.SaveChangesAsync(ct);

            Log(context, cicloId, "Time", "Criado", competicao.Nome, timeNome: nome,
                detalhes: $"IdTeam FIFA={idTeam} | {Localizado(time, "TeamName")} | Bandeira={bandeira ?? "sem bandeira"}");

            return (novo, true);
        }

        /// <summary>
        /// "Brazil" + AgeType 4 + Gender 2 → "Brasil Sub-20 (F)". O país sai do
        /// CountryHelper (o mesmo de-para que a importação da api-football usa, para o
        /// nome bater com o das outras competições) e a categoria vem dos campos que a
        /// própria FIFA manda em cada time da partida.
        /// </summary>
        private static string NomeDaSelecao(JsonElement time)
        {
            var original = Localizado(time, "TeamName") ?? Texto(time, "Abbreviation") ?? "Seleção";
            var pais = CountryHelper.Traduzir(original);

            var idade = Numero(time, "AgeType") switch
            {
                1 => "Sub-17",
                2 => "Sub-19",
                3 => "Sub-21",
                4 => "Sub-20",
                5 => "Sub-23",
                6 => "Sub-18",
                // 7 = adulto; qualquer código novo entra sem sufixo, que é o erro mais
                // barato: some a categoria do nome, não aparece uma errada.
                _ => null,
            };

            // Gender 2 = feminino. O masculino fica sem marca porque é assim que as
            // seleções já cadastradas no sistema se chamam ("Brasil", não "Brasil (M)").
            var genero = Numero(time, "Gender") == 2 ? "(F)" : null;

            return string.Join(' ', new[] { pais, idade, genero }.Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>
        /// Bandeira do país como escudo. A FIFA publica a URL com placeholders
        /// ("flags-{format}-{size}/BRA"); sq-4 é o quadrado grande, o formato que
        /// serve de escudo nas telas.
        /// </summary>
        private static string? Bandeira(JsonElement time)
        {
            var pais = Texto(time, "IdCountry");
            if (string.IsNullOrWhiteSpace(pais)) return null;

            return $"{Base}picture/flags-sq-4/{Uri.EscapeDataString(pais)}";
        }

        /// <summary>
        /// Este nó de time da FIFA (Home/Away do calendário, HomeTeam/AwayTeam da
        /// partida) é o time <paramref name="nosso"/> do jogo?
        ///
        /// A comparação é pelo IdTeam da FIFA, que a sincronização gravou em
        /// Time.LinkTransfermarket. Nome não serve: a FIFA manda "Japan" e "Korea DPR",
        /// o nosso cadastro guarda "Japão Sub-20 (F)", e o TimeNomeMatcher é afinado em
        /// clube brasileiro e sul-americano, não em nome de país em inglês.
        ///
        /// Só cai no nome quando o time não tem vínculo com a FIFA (cadastro manual
        /// anterior à importação) — e aí compara país com país, pelo mesmo de-para que
        /// nomeia os times aqui.
        /// </summary>
        internal static bool ETimeDaFifa(JsonElement timeFifa, Time? nosso)
        {
            if (nosso == null) return false;

            var idTeam = Texto(timeFifa, "IdTeam");

            if (IsFifaLink(nosso.LinkTransfermarket))
                return !string.IsNullOrWhiteSpace(idTeam) &&
                       string.Equals(nosso.LinkTransfermarket, $"fifa:{idTeam}",
                           StringComparison.OrdinalIgnoreCase);

            var deles = Localizado(timeFifa, "TeamName");
            if (string.IsNullOrWhiteSpace(deles)) return false;

            return string.Equals(SemCategoria(nosso.Nome), CountryHelper.Traduzir(deles),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// "Brasil Sub-20 (F)" → "Brasil". O sufixo de categoria é nosso (ver
        /// <see cref="NomeDaSelecao"/>) e não existe do lado da FIFA.
        /// </summary>
        internal static string SemCategoria(string nome)
        {
            var limpo = nome;
            foreach (var sufixo in new[]
                     { "(F)", "Sub-17", "Sub-18", "Sub-19", "Sub-20", "Sub-21", "Sub-23" })
                limpo = limpo.Replace(sufixo, "", StringComparison.OrdinalIgnoreCase);

            return limpo.Trim();
        }

        /// <summary>
        /// Grava em <paramref name="jogo"/> o placar final que o documento da partida
        /// (live/football) publicou, e devolve se mexeu em alguma coisa.
        ///
        /// Existe porque o placar da FIFA só entrava pela sincronização do calendário
        /// da competição, que é manual: o jogo importado antes do apito ficava sem
        /// PlacarCasa/PlacarVisitante mesmo depois de a reimportação e o ciclo ao vivo
        /// terem trazido escalação e lances — e é PlacarCasa preenchido que o resto do
        /// sistema lê como "jogo realizado". O mesmo documento que traz a escalação já
        /// traz o resultado (HomeTeam/AwayTeam › Score), então ele é gravado na mesma
        /// visita, como a reimportação da api-football faz com Goals.
        ///
        /// MatchStatus 0 = encerrada. O placar é conferido junto porque a FIFA já
        /// publicou partida marcada como encerrada antes de preencher o resultado, e os
        /// dois lados são conferidos pelo IdTeam antes de gravar: placar trocado é pior
        /// do que placar ausente.
        /// </summary>
        internal static bool AplicarPlacarFinal(Jogo jogo, JsonElement raiz)
        {
            if (Numero(raiz, "MatchStatus") != 0) return false;

            if (!raiz.TryGetProperty("HomeTeam", out var casa) ||
                !raiz.TryGetProperty("AwayTeam", out var visitante)) return false;

            if (!ETimeDaFifa(casa, jogo.TimeCasa) || !ETimeDaFifa(visitante, jogo.TimeVisitante))
                return false;

            var placarCasa = NumeroNulavel(casa, "Score");
            var placarVis = NumeroNulavel(visitante, "Score");
            if (placarCasa == null || placarVis == null) return false;

            var penaltisCasa = NumeroNulavel(raiz, "HomeTeamPenaltyScore");
            var penaltisVis = NumeroNulavel(raiz, "AwayTeamPenaltyScore");

            if (jogo.PlacarCasa == placarCasa && jogo.PlacarVisitante == placarVis &&
                jogo.PenaltisCasa == penaltisCasa && jogo.PenaltisVisitante == penaltisVis &&
                jogo.Status == "Finalizado") return false;

            jogo.PlacarCasa = placarCasa;
            jogo.PlacarVisitante = placarVis;
            jogo.PenaltisCasa = penaltisCasa;
            jogo.PenaltisVisitante = penaltisVis;
            jogo.Status = "Finalizado";
            jogo.Atualizado = 1;
            return true;
        }

        // ── Jogos ────────────────────────────────────────────────────────────

        private async Task IncluirOuAtualizarJogoAsync(
            FutebolContext context, Competicao competicao, JsonElement p,
            Time timeCasa, Time timeVis, SeasonFifa season, Guid cicloId, CancellationToken ct)
        {
            var idMatch = Texto(p, "IdMatch") ?? throw new InvalidOperationException("Partida sem IdMatch.");
            var idStage = Texto(p, "IdStage") ?? "";
            var referencia = new PartidaRef(season.IdCompetition, season.IdSeason, idStage, idMatch).ToString();

            var data = Data(p, "Date");
            var rodada = int.TryParse(Texto(p, "MatchDay"), out var md) ? md : 0;
            var grupo = FaseDaPartida(p);
            var placarCasa = NumeroNulavel(p, "HomeTeamScore");
            var placarVis = NumeroNulavel(p, "AwayTeamScore");

            // MatchStatus 0 = encerrada, 1 = ainda não começou, 3 = em andamento. O
            // placar é conferido junto porque a FIFA já publicou partida marcada como
            // encerrada antes de preencher o resultado.
            var status = Numero(p, "MatchStatus");
            var finalizado = status == 0 && placarCasa.HasValue && placarVis.HasValue;

            var existente = await context.Jogos.FirstOrDefaultAsync(j =>
                j.CompeticaoId == competicao.Id &&
                (j.LinkDetalhes == referencia ||
                    (j.Temporada == season.Ano &&
                     j.TimeCasaId == timeCasa.Id &&
                     j.TimeVisitanteId == timeVis.Id)), ct);

            if (existente != null)
            {
                if (data.HasValue) existente.Data = data;
                if (rodada > 0) existente.Rodada = rodada;
                existente.Temporada = season.Ano;
                // Reescreve sempre: o IdStage do mata-mata só passa a existir depois do
                // sorteio, e um jogo importado antes disso ficou com a referência da
                // fase antiga (que devolveria 404 na escalação).
                existente.LinkDetalhes = referencia;

                if (!string.IsNullOrWhiteSpace(grupo)) existente.Grupo = grupo;

                // Estádio e árbitro só entram no calendário perto da partida — o jogo
                // nasce sem eles e é aqui que aparecem.
                if (string.IsNullOrWhiteSpace(existente.Estadio) && Estadio(p) is { } est)
                    existente.Estadio = est;
                if (string.IsNullOrWhiteSpace(existente.Arbitro) && Arbitro(p) is { } arb)
                    existente.Arbitro = arb;

                if (finalizado)
                {
                    existente.PlacarCasa = placarCasa;
                    existente.PlacarVisitante = placarVis;
                    existente.PenaltisCasa = NumeroNulavel(p, "HomeTeamPenaltyScore");
                    existente.PenaltisVisitante = NumeroNulavel(p, "AwayTeamPenaltyScore");
                    existente.Status = "Finalizado";
                    existente.Atualizado = 1;
                }

                return;
            }

            var formacaoPadrao = await context.Formacoes.FirstOrDefaultAsync(ct)
                ?? new Formacao { Nome = "4-3-3" };

            var jogo = new Jogo
            {
                CompeticaoId        = competicao.Id,
                TimeCasa            = timeCasa,
                TimeVisitante       = timeVis,
                Data                = data,
                Rodada              = rodada,
                Temporada           = season.Ano,
                PlacarCasa          = finalizado ? placarCasa : null,
                PlacarVisitante     = finalizado ? placarVis : null,
                PenaltisCasa        = finalizado ? NumeroNulavel(p, "HomeTeamPenaltyScore") : null,
                PenaltisVisitante   = finalizado ? NumeroNulavel(p, "AwayTeamPenaltyScore") : null,
                Grupo               = grupo,
                Status              = finalizado ? "Finalizado" : "Agendado",
                Atualizado          = finalizado ? 1 : 0,
                FormacaoCasaId      = formacaoPadrao.Id,
                FormacaoVisitanteId = formacaoPadrao.Id,
                LinkDetalhes        = referencia,
                Estadio             = Estadio(p),
                Arbitro             = Arbitro(p),
            };

            context.Jogos.Add(jogo);
            await context.SaveChangesAsync(ct);

            // Onze slots vazios por lado, como faz a importação da api-football para
            // jogo sem escalação publicada — é o que a tela de análise espera encontrar.
            foreach (var ehCasa in new[] { true, false })
                for (var i = 0; i < 11; i++)
                    context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = jogo.Id,
                        IsTimeCasa = ehCasa,
                        Titular = true,
                        FaseEscalacao = "INICIAL",
                    });

            Log(context, cicloId, "Jogo", "Criado", competicao.Nome,
                timeNome: $"{timeCasa.Nome} × {timeVis.Nome}",
                jogoDescricao: data?.ToLocalTime().ToString("dd/MM/yyyy") ?? "sem data",
                detalhes: finalizado ? $"Placar: {placarCasa}×{placarVis}" : $"Agendado | {grupo}");
        }

        /// <summary>
        /// O que vai para Jogo.Grupo, que é de onde o FaseJogoClassifier deduz a fase.
        /// Na fase de grupos vale o grupo ("Group B"); no mata-mata, o nome do estágio
        /// ("Quarter-final", "Final") — os dois rótulos da FIFA já são reconhecidos
        /// pelo classificador, então não são traduzidos aqui.
        /// </summary>
        private static string? FaseDaPartida(JsonElement p)
        {
            var grupo = Localizado(p, "GroupName");
            if (!string.IsNullOrWhiteSpace(grupo) &&
                grupo.StartsWith("Group", StringComparison.OrdinalIgnoreCase))
                return grupo;

            var estagio = Localizado(p, "StageName");

            // "First Stage" é o rótulo genérico da fase de grupos: sem a letra do grupo
            // ele não classifica como grupo nem diz nada ao usuário, então vira o grupo
            // quando existir, ou nada.
            if (!string.IsNullOrWhiteSpace(estagio) &&
                !estagio.Equals("First Stage", StringComparison.OrdinalIgnoreCase))
                return estagio;

            return grupo;
        }

        /// <summary>Estádio da partida no JSON da FIFA (público: a reimportação de
        /// escalação preenche com ele o jogo que nasceu sem estádio).</summary>
        public static string? Estadio(JsonElement p) =>
            p.TryGetProperty("Stadium", out var e) && e.ValueKind == JsonValueKind.Object
                ? Localizado(e, "Name")
                : null;

        /// <summary>O árbitro principal (OfficialType 1); os assistentes ficam de fora.</summary>
        public static string? Arbitro(JsonElement p)
        {
            if (!p.TryGetProperty("Officials", out var oficiais) ||
                oficiais.ValueKind != JsonValueKind.Array) return null;

            foreach (var o in oficiais.EnumerateArray())
                if (Numero(o, "OfficialType") == 1)
                    return Localizado(o, "Name");

            return null;
        }

        // ── Elenco da seleção na competição ──────────────────────────────────

        /// <summary>
        /// Cadastra o elenco que a seleção inscreveu na edição (teams/{id}/squad).
        ///
        /// Existe para o pré-jogo: a FIFA publica a lista de inscritas assim que as
        /// federações entregam, semanas antes da bola rolar, enquanto o elenco no
        /// sistema só nascia da escalação do primeiro jogo. Quem vai cobrir a
        /// competição precisa das jogadoras cadastradas ANTES — para montar o pré-jogo,
        /// escrever anotação e conhecer quem vai entrar em campo.
        ///
        /// Não apaga nem desvincula ninguém: quem já está no elenco e não aparece na
        /// lista da FIFA continua onde está. Tirar jogadora de elenco é decisão do
        /// usuário, e uma inscrição corrigida pela federação no meio do caminho
        /// apagaria trabalho de anotação sem aviso.
        /// </summary>
        public async Task<ResultadoFifa> ImportarElencoAsync(
            FutebolContext context, Time time, string idCompetition, string idSeason,
            CancellationToken ct = default)
        {
            if (!IsFifaLink(time.LinkTransfermarket))
                return new ResultadoFifa(false,
                    $"O time \"{time.Nome}\" não tem vínculo com a FIFA. " +
                    "Ele aparece assim depois de \"Buscar jogos\" na competição.");

            var idTeam = time.LinkTransfermarket!.Split(':')[1];

            var doc = await BuscarJsonAsync(
                $"teams/{Uri.EscapeDataString(idTeam)}/squad" +
                $"?idCompetition={Uri.EscapeDataString(idCompetition)}" +
                $"&idSeason={Uri.EscapeDataString(idSeason)}&language={Lingua}",
                TimeSpan.FromMinutes(10), ct);

            if (doc == null)
                return new ResultadoFifa(false, "A FIFA não respondeu o elenco desta seleção.");

            using var _ = doc;

            if (!doc.RootElement.TryGetProperty("Players", out var jogadoras) ||
                jogadoras.ValueKind != JsonValueKind.Array || jogadoras.GetArrayLength() == 0)
                return new ResultadoFifa(false,
                    $"A FIFA ainda não publicou a lista de inscritas de {time.Nome}.");

            // Nacionalidade: a seleção é do país, então vale para o elenco inteiro. O
            // IdCountry das jogadoras é o código de três letras da FIFA ("BRA"), que o
            // CountryHelper não conhece — o nome do time, sem a categoria, ele conhece.
            var nacionalidade = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(
                context, SemCategoria(time.Nome), ct);

            var elenco = await EspnEscalacaoService.ElencoAsync(context, time.Id, ct);
            var criados = 0;
            var completados = 0;

            foreach (var j in jogadoras.EnumerateArray())
            {
                var nomeBruto = Localizado(j, "PlayerName");
                if (string.IsNullOrWhiteSpace(nomeBruto)) continue;

                var nome = FifaEscalacaoService.NormalizarNome(nomeBruto);
                var idPlayer = Texto(j, "IdPlayer");
                var vinculo = idPlayer is { Length: > 0 } ? $"fifa:{idPlayer}" : null;
                var camisa = NumeroNulavel(j, "JerseyNum");

                // Já cadastrada? Primeiro pelo vínculo com a FIFA, depois pelo mesmo
                // casamento de nome e camisa que a importação de escalação usa — é ele
                // que evita duplicar quem entrou no elenco por um jogo anterior.
                var existente = elenco.FirstOrDefault(e =>
                                    vinculo != null && e.LinkTransfermarket == vinculo)
                             ?? EspnEscalacaoService.Casar(elenco, nome, camisa)?.Jogador;

                if (existente != null)
                {
                    var mexeu = false;

                    if (string.IsNullOrWhiteSpace(existente.LinkTransfermarket) && vinculo != null)
                    { existente.LinkTransfermarket = vinculo; mexeu = true; }

                    // Completa o que falta, sem sobrescrever: dado corrigido à mão vale
                    // mais que o da fonte.
                    if (existente.NumeroCamisa == null && camisa != null)
                    { existente.NumeroCamisa = camisa; mexeu = true; }
                    if (existente.DataNascimento == null && Nascimento(j) is { } nasc)
                    { existente.DataNascimento = nasc; mexeu = true; }
                    if (existente.Altura == null && Altura(j) is { } alt)
                    { existente.Altura = alt; mexeu = true; }
                    if (string.IsNullOrWhiteSpace(existente.Posicao))
                    { existente.Posicao = Posicao(j); mexeu = true; }
                    if (existente.NacionalidadeId == null && nacionalidade != null)
                    { existente.NacionalidadeId = nacionalidade.Id; mexeu = true; }

                    if (mexeu)
                    {
                        existente.DtAlt = DateTime.UtcNow;
                        completados++;
                    }

                    continue;
                }

                var jogadora = new Jogador
                {
                    Nome               = nome,
                    Posicao            = Posicao(j),
                    NumeroCamisa       = camisa,
                    DataNascimento     = Nascimento(j),
                    Altura             = Altura(j),
                    NacionalidadeId    = nacionalidade?.Id,
                    TimeId             = time.Id,
                    // Mesma regra da importação da api-football: numa seleção o vínculo
                    // também vai para SelecaoId, que é o que separa "joga no clube X" de
                    // "está na seleção Y".
                    SelecaoId          = time.EhSelecao ? time.Id : null,
                    // IdApi fica nulo de propósito: aquele campo é o id da api-football,
                    // e o id da FIFA vive em outro espaço numérico.
                    LinkTransfermarket = vinculo,
                    DtInc              = DateTime.UtcNow,
                };

                context.Jogadores.Add(jogadora);
                elenco.Add(jogadora);
                criados++;
            }

            // O treinador vem na MESMA resposta (Officials), então não custa chamada
            // nenhuma — e é o que o pré-jogo pede junto do elenco.
            var treinador = await AplicarTreinadorAsync(context, time, doc.RootElement, nacionalidade, ct);

            if (criados > 0 || completados > 0 || treinador != null) await context.SaveChangesAsync(ct);

            var partes = new List<string>();
            if (criados > 0) partes.Add($"{criados} jogador(es) cadastrado(s)");
            if (completados > 0) partes.Add($"{completados} já existente(s) completado(s)");
            if (treinador != null) partes.Add(treinador);

            return new ResultadoFifa(true, partes.Count > 0
                ? $"Elenco de {time.Nome} pela FIFA: {string.Join(", ", partes)}."
                : $"O elenco de {time.Nome} já estava completo — nada a cadastrar.");
        }

        /// <summary>
        /// Cadastra (ou atualiza) o treinador da seleção a partir da comissão técnica
        /// que veio junto do elenco. Devolve a frase para a mensagem da tela, ou null
        /// quando não havia nada a fazer.
        ///
        /// O time tem UM treinador atual, e é esse registro que carrega as anotações de
        /// treinador — por isso a troca de comando renomeia o registro em vez de criar
        /// outro, exatamente como AtualizarTreinadorTime faz com a api-football. Criar
        /// um segundo deixaria as anotações penduradas num técnico que não dirige mais
        /// o time.
        /// </summary>
        private static async Task<string?> AplicarTreinadorAsync(
            FutebolContext context, Time time, JsonElement raiz,
            Nacionalidade? nacionalidade, CancellationToken ct)
        {
            if (!raiz.TryGetProperty("Officials", out var comissao) ||
                comissao.ValueKind != JsonValueKind.Array) return null;

            // Role 0 é o treinador principal; 1 são os auxiliares (a Inglaterra inscreveu
            // dois). Conferido nas partidas, onde o nó Coaches traz o técnico com Role 0.
            var oficial = comissao.EnumerateArray().FirstOrDefault(o => Numero(o, "Role") == 0);
            if (oficial.ValueKind != JsonValueKind.Object) return null;

            var nomeBruto = Localizado(oficial, "Name");
            if (string.IsNullOrWhiteSpace(nomeBruto)) return null;

            var nome = FifaEscalacaoService.NormalizarNome(nomeBruto);
            var nascimento = NascimentoTreinador(oficial);

            var atual = await context.Treinadores.FirstOrDefaultAsync(t => t.TimeId == time.Id, ct);

            if (atual == null)
            {
                context.Treinadores.Add(new Treinador
                {
                    TimeId          = time.Id,
                    Nome            = nome,
                    DataNascimento  = nascimento,
                    NacionalidadeId = nacionalidade?.Id,
                    DtInc           = DateTime.UtcNow,
                });

                return $"treinador {nome} cadastrado";
            }

            var trocou = !string.Equals(atual.Nome, nome, StringComparison.OrdinalIgnoreCase);
            var mexeu = trocou;

            if (trocou)
            {
                atual.Nome = nome;
                // Dados do técnico anterior não valem para o novo.
                atual.DataNascimento = nascimento;
                atual.NacionalidadeId = nacionalidade?.Id;
            }
            else
            {
                if (atual.DataNascimento == null && nascimento != null)
                { atual.DataNascimento = nascimento; mexeu = true; }
                if (atual.NacionalidadeId == null && nacionalidade != null)
                { atual.NacionalidadeId = nacionalidade.Id; mexeu = true; }
            }

            if (!mexeu) return null;

            atual.DtAlt = DateTime.UtcNow;
            return trocou ? $"treinador atualizado para {nome}" : $"dados do treinador {nome} completados";
        }

        /// <summary>
        /// Data de nascimento como DATA, sem fuso: a coluna é "timestamp without time
        /// zone" e o Npgsql recusa um DateTime com Kind=Utc nela. A FIFA manda
        /// "2008-04-11T00:00:00Z", que só tem o dia de informação útil.
        /// </summary>
        private static DateTime? Nascimento(JsonElement j) =>
            Data(j, "BirthDate") is { } d
                ? DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified)
                : null;

        /// <summary>
        /// A mesma data de nascimento, ancorada ao meio-dia UTC — a convenção que
        /// Times/CriarTreinador já usa para o técnico cadastrado à mão.
        ///
        /// Treinador.DataNascimento, ao contrário de Jogador.DataNascimento, não tem
        /// tipo de coluna declarado no FutebolContext: o EF a trata como "timestamp with
        /// time zone" e aplica o conversor que chama ToUniversalTime(). Uma data
        /// Unspecified à meia-noite perderia três horas nesse caminho e voltaria como o
        /// dia anterior. Com Kind=Utc e meio-dia, a conversão não tem o que mover.
        /// </summary>
        private static DateTime? NascimentoTreinador(JsonElement o) =>
            Data(o, "BirthDate") is { } d
                ? DateTime.SpecifyKind(d.Date.AddHours(12), DateTimeKind.Utc)
                : null;

        /// <summary>A FIFA manda a altura em centímetros com casa decimal (178.0).</summary>
        private static int? Altura(JsonElement j) =>
            j.TryGetProperty("Height", out var h) && h.ValueKind == JsonValueKind.Number &&
            h.TryGetDouble(out var cm) && cm > 0 ? (int)Math.Round(cm) : null;

        /// <summary>
        /// Posição no vocabulário do cadastro ("Goleiro", "Defensor", "Meia",
        /// "Atacante") — o mesmo que ApiFootballService.MapearPosicao produz, para o
        /// elenco vindo da FIFA se comportar como qualquer outro nas telas.
        /// </summary>
        private static string Posicao(JsonElement j) => Numero(j, "Position") switch
        {
            0 => "Goleiro",
            1 => "Defensor",
            2 => "Meia",
            3 => "Atacante",
            _ => Localizado(j, "PositionLocalized") switch
            {
                "Goalkeeper" => "Goleiro",
                "Defender" => "Defensor",
                "Midfielder" => "Meia",
                "Forward" => "Atacante",
                _ => "",
            },
        };

        // ── Partida: escalação e lances ──────────────────────────────────────

        /// <summary>
        /// Abre o documento da partida (live/football), que é onde estão escalação,
        /// técnicos e posse de bola. Devolve o jogo carregado junto porque quem chama
        /// precisa dos dois lados para casar os times.
        /// </summary>
        internal async Task<(Jogo? Jogo, JsonDocument? Doc, string? IdMatch, ResultadoFifa? Erro)>
            AbrirPartidaAsync(FutebolContext context, int jogoId, CancellationToken ct)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            if (jogo == null)
                return (null, null, null, new ResultadoFifa(false, "Jogo não encontrado."));

            var referencia = RefDaPartida(jogo.LinkDetalhes);
            if (referencia == null)
                return (jogo, null, null, new ResultadoFifa(false,
                    "Este jogo não veio da FIFA (sem referência fifa: em LinkDetalhes)."));

            // TTL curto pelo mesmo motivo do calendário: a escalação sai perto do
            // apito inicial e é justamente aí que o analista aperta o botão.
            var doc = await BuscarJsonAsync(
                $"live/football/{referencia.Competicao}/{referencia.Season}/" +
                $"{referencia.Stage}/{referencia.Match}?language={Lingua}",
                TimeSpan.FromMinutes(2), ct);

            if (doc == null)
                return (jogo, null, referencia.Match, new ResultadoFifa(false,
                    "A FIFA não respondeu os dados desta partida.", referencia.Match));

            return (jogo, doc, referencia.Match, null);
        }

        /// <summary>A narração lance a lance (timelines). Null quando a FIFA não publicou.</summary>
        internal async Task<JsonDocument?> AbrirTimelineAsync(PartidaRef referencia, CancellationToken ct) =>
            await BuscarJsonAsync(
                $"timelines/{referencia.Competicao}/{referencia.Season}/" +
                $"{referencia.Stage}/{referencia.Match}?language={Lingua}",
                TimeSpan.FromMinutes(2), ct);

        // ── HTTP ─────────────────────────────────────────────────────────────

        private async Task<JsonDocument?> BuscarJsonAsync(string rota, TimeSpan ttl, CancellationToken ct)
        {
            var chave = "fifa:" + rota;
            if (_cache.TryGetValue(chave, out string? emCache) && emCache != null)
                return Parse(emCache, rota);

            try
            {
                await AguardarPortaAsync(ct);

                var json = await _http.GetStringAsync(rota, ct);

                // A FIFA responde 200 com o corpo "null" nos endpoints que existem mas
                // não têm dado para aquela partida — não é erro, é ausência.
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") return null;

                _cache.Set(chave, json, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    // O MemoryCache global tem SizeLimit em bytes (ver Program.cs):
                    // ~2 bytes por char.
                    Size = json.Length * 2L,
                });

                return Parse(json, rota);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Fifa] Falha ao consultar {Rota}.", rota);
                return null;
            }
        }

        private JsonDocument? Parse(string json, string rota)
        {
            try { return JsonDocument.Parse(json); }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[Fifa] Resposta ilegível em {Rota}.", rota);
                return null;
            }
        }

        private static async Task AguardarPortaAsync(CancellationToken ct)
        {
            await _porta.WaitAsync(ct);
            try
            {
                var desde = DateTimeOffset.UtcNow - _ultimaChamada;
                if (desde < IntervaloMinimo)
                    await Task.Delay(IntervaloMinimo - desde, ct);
                _ultimaChamada = DateTimeOffset.UtcNow;
            }
            finally { _porta.Release(); }
        }

        // ── Leitura do JSON da FIFA ──────────────────────────────────────────

        /// <summary>
        /// Texto localizado da FIFA: todo nome vem como lista
        /// [{ Locale, Description }]. Pega en-GB e, na falta, o primeiro que houver.
        /// </summary>
        internal static string? Localizado(JsonElement el, params string[] caminho)
        {
            var atual = el;
            foreach (var passo in caminho)
            {
                if (atual.ValueKind != JsonValueKind.Object ||
                    !atual.TryGetProperty(passo, out atual)) return null;
            }

            if (atual.ValueKind == JsonValueKind.String) return atual.GetString();
            if (atual.ValueKind != JsonValueKind.Array) return null;

            string? primeiro = null;
            foreach (var item in atual.EnumerateArray())
            {
                var descricao = item.TryGetProperty("Description", out var d) ? d.GetString() : null;
                if (string.IsNullOrWhiteSpace(descricao)) continue;

                primeiro ??= descricao;
                var locale = item.TryGetProperty("Locale", out var l) ? l.GetString() : null;
                if (locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true) return descricao;
            }

            return primeiro;
        }

        internal static string? Texto(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.GetRawText(),
                    _ => null,
                }
                : null;

        internal static int? NumeroNulavel(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetInt32(out var n) ? n : null;

        internal static int Numero(JsonElement el, string prop) => NumeroNulavel(el, prop) ?? -1;

        internal static DateTime? Data(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
                ? d
                : null;

        private static void Log(
            FutebolContext context, Guid cicloId, string tipo, string acao,
            string? competicaoNome = null, string? timeNome = null,
            string? jogoDescricao = null, string? detalhes = null)
        {
            context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId        = cicloId,
                Data           = DateTime.UtcNow,
                Tipo           = tipo,
                Acao           = acao,
                CompeticaoNome = competicaoNome,
                TimeNome       = timeNome,
                JogoDescricao  = jogoDescricao,
                Detalhes       = detalhes,
            });
        }
    }
}
