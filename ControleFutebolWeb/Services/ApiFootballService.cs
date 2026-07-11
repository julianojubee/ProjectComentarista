using System.Text;
using System.Text.Json;
using System.Globalization;
using ControleFutebolWeb.Helpers;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Integração com api-football.com (v3.football.api-sports.io).
    /// Link da competição no banco: "apifoot:LEAGUE_ID:SEASON"  ex.: "apifoot:71:2026"
    /// </summary>
    public class ApiFootballService
    {
        private readonly HttpClient _http;
        private readonly ILogger<ApiFootballService> _logger;
        private readonly IMemoryCache _cache;
        private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        // Legado — entradas extras não cobertas pelo CountryHelper centralizado.
        // Ao adicionar novos países, edite Helpers/CountryHelper.cs em vez daqui.
        private static readonly Dictionary<string, string> _traducaoTimes =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["South Africa"]    = "África do Sul",
            ["Egypt"]           = "Egito",
            ["Morocco"]         = "Marrocos",
            ["Nigeria"]         = "Nigéria",
            ["Cameroon"]        = "Camarões",
            ["Ivory Coast"]     = "Costa do Marfim",
            ["Senegal"]         = "Senegal",
            ["Ghana"]           = "Gana",
            ["Algeria"]         = "Argélia",
            ["Tunisia"]         = "Tunísia",
            ["Mali"]            = "Mali",
            ["DR Congo"]        = "Rep. Dem. do Congo",
            ["Zambia"]          = "Zâmbia",
            ["Zimbabwe"]        = "Zimbábue",
            ["Uganda"]          = "Uganda",
            ["Tanzania"]        = "Tanzânia",
            ["Kenya"]           = "Quênia",
            ["Ethiopia"]        = "Etiópia",
            ["Angola"]          = "Angola",
            ["Mozambique"]      = "Moçambique",
            ["Germany"]         = "Alemanha",
            ["France"]          = "França",
            ["Spain"]           = "Espanha",
            ["Italy"]           = "Itália",
            ["England"]         = "Inglaterra",
            ["Portugal"]        = "Portugal",
            ["Netherlands"]     = "Holanda",
            ["Belgium"]         = "Bélgica",
            ["Switzerland"]     = "Suíça",
            ["Croatia"]         = "Croácia",
            ["Denmark"]         = "Dinamarca",
            ["Sweden"]          = "Suécia",
            ["Norway"]          = "Noruega",
            ["Poland"]          = "Polônia",
            ["Czech Republic"]  = "República Tcheca",
            ["Czechia"]         = "República Tcheca",
            ["Slovakia"]        = "Eslováquia",
            ["Hungary"]         = "Hungria",
            ["Romania"]         = "Romênia",
            ["Serbia"]          = "Sérvia",
            ["Ukraine"]         = "Ucrânia",
            ["Austria"]         = "Áustria",
            ["Greece"]          = "Grécia",
            ["Turkey"]          = "Turquia",
            ["Türkiye"]         = "Turquia",
            ["Turkiye"]         = "Turquia",
            ["Russia"]          = "Rússia",
            ["Scotland"]        = "Escócia",
            ["Wales"]           = "País de Gales",
            ["Ireland"]         = "Irlanda",
            ["Northern Ireland"]= "Irlanda do Norte",
            ["Brazil"]          = "Brasil",
            ["Argentina"]       = "Argentina",
            ["Uruguay"]         = "Uruguai",
            ["Colombia"]        = "Colômbia",
            ["Chile"]           = "Chile",
            ["Peru"]            = "Peru",
            ["Ecuador"]         = "Equador",
            ["Bolivia"]         = "Bolívia",
            ["Venezuela"]       = "Venezuela",
            ["Paraguay"]        = "Paraguai",
            ["Mexico"]          = "México",
            ["United States"]   = "Estados Unidos",
            ["USA"]             = "Estados Unidos",
            ["Canada"]          = "Canadá",
            ["Costa Rica"]      = "Costa Rica",
            ["Honduras"]        = "Honduras",
            ["Panama"]          = "Panamá",
            ["Jamaica"]         = "Jamaica",
            ["Japan"]           = "Japão",
            ["South Korea"]     = "Coreia do Sul",
            ["Korea Republic"]  = "Coreia do Sul",
            ["China"]           = "China",
            ["Australia"]       = "Austrália",
            ["New Zealand"]     = "Nova Zelândia",
            ["Saudi Arabia"]    = "Arábia Saudita",
            ["Iran"]            = "Irã",
            ["Iraq"]            = "Iraque",
            ["Qatar"]           = "Catar",
            ["United Arab Emirates"] = "Emirados Árabes",
            ["UAE"]             = "Emirados Árabes",
            ["Israel"]          = "Israel",
            ["Jordan"]          = "Jordânia",
            ["Cape Verde"]      = "Cabo Verde",
            ["Cape Verde Islands"] = "Cabo Verde",
            ["Curacao"]         = "Curaçao",
            ["Curaçao"]         = "Curaçao",
            ["Haiti"]           = "Haiti",
            ["Suriname"]        = "Suriname",
            ["New Caledonia"]   = "Nova Caledônia",
            ["Uzbekistan"]      = "Uzbequistão",
        };

        // Status considerados "jogo finalizado"
        private static readonly HashSet<string> StatusFinalizados =
            new(StringComparer.OrdinalIgnoreCase) { "FT", "AET", "PEN" };

        public ApiFootballService(HttpClient http, IConfiguration config,
            ILogger<ApiFootballService> logger, IMemoryCache cache)
        {
            _http = http;
            _logger = logger;
            _cache = cache;

            _http.BaseAddress = new Uri("https://v3.football.api-sports.io/");
            _http.DefaultRequestHeaders.Add("x-apisports-key",
                config["ApiFootball:Key"] ?? throw new InvalidOperationException(
                    "ApiFootball:Key não configurada em appsettings.json"));
        }

        // ── Parse do link "apifoot:71:2026" ──────────────────────────────────

        public static bool IsApiFootballLink(string? link) =>
            link?.StartsWith("apifoot:", StringComparison.OrdinalIgnoreCase) == true;

        public static (int leagueId, int season) ParseLink(string link)
        {
            var parts = link.Split(':');
            if (parts.Length < 3 ||
                !int.TryParse(parts[1], out var leagueId) ||
                !int.TryParse(parts[2], out var season))
                throw new ArgumentException($"Link inválido: {link}. Formato: apifoot:LEAGUE_ID:SEASON");
            return (leagueId, season);
        }

        // ── Sincronização de uma competição ──────────────────────────────────

        public async Task<(int jogosProcessados, int timesCreados, int erros, List<string> avisos)>
            SincronizarCompeticaoAsync(
                FutebolContext context,
                Competicao competicao,
                CancellationToken ct = default)
        {
            int jogosProcessados = 0, timesCreados = 0, erros = 0;
            var avisos = new List<string>();

            if (!IsApiFootballLink(competicao.LinkTransfermarket))
            {
                avisos.Add("Link não é do formato apifoot:LEAGUE_ID:SEASON.");
                return (0, 0, 0, avisos);
            }

            // Limpa logs anteriores desta competição e inicia novo ciclo
            var logsAntigos = await context.TransfermarktSincronizacaoLogs
                .Where(l => l.CompeticaoNome == competicao.Nome)
                .ToListAsync(ct);
            context.TransfermarktSincronizacaoLogs.RemoveRange(logsAntigos);
            await context.SaveChangesAsync(ct);

            var cicloId = Guid.NewGuid();
            Log(context, cicloId, "Ciclo", "Iniciado",
                competicaoNome: competicao.Nome,
                detalhes: $"Fonte: api-football.com | Link: {competicao.LinkTransfermarket}");

            var (leagueId, season) = ParseLink(competicao.LinkTransfermarket!);
            _logger.LogInformation("[ApiFoot] Sincronizando {Nome} — league={L} season={S}",
                competicao.Nome, leagueId, season);

            var fixtures = await BuscarFixturesAsync(leagueId, season, ct);
            _logger.LogInformation("[ApiFoot] {N} fixtures encontrados.", fixtures.Count);

            Log(context, cicloId, "Ciclo", "Fixtures",
                competicaoNome: competicao.Nome,
                detalhes: $"{fixtures.Count} fixtures encontrados na API");
            await context.SaveChangesAsync(ct);

            // Grupos: a api-football costuma só preencher fixture.league.group depois que
            // os jogos da fase de grupos já existem; o endpoint de standings tem o grupo
            // de cada time desde o sorteio.
            var gruposPorTime = await BuscarGruposPorTimeAsync(leagueId, season, ct);

            foreach (var fx in fixtures)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    AfFixture detalhado = fx;

                    if (StatusFinalizados.Contains(fx.Fixture.Status.Short) &&
                        (!fx.Lineups.Any() || !fx.Statistics.Any() || !fx.Players.Any()))
                    {
                        await Task.Delay(600, ct);
                        detalhado = await BuscarDetalhesFixtureAsync(fx.Fixture.Id, ct) ?? fx;
                    }

                    var (timeCasa, casaCriado) = await ResolverOuCriarTime(context,
                        detalhado.Teams.Home.Name, detalhado.Teams.Home.Id,
                        detalhado.Teams.Home.Logo, cicloId, ct, competicao.EhSelecaoNacional);
                    if (casaCriado) timesCreados++;

                    var (timeVis, visCriado) = await ResolverOuCriarTime(context,
                        detalhado.Teams.Away.Name, detalhado.Teams.Away.Id,
                        detalhado.Teams.Away.Logo, cicloId, ct, competicao.EhSelecaoNacional);
                    if (visCriado) timesCreados++;

                    // "Group Stage" é um texto genérico que a api-football às vezes devolve
                    // em fixture.league.group quando o grupo específico (A, B, C...) ainda
                    // não foi vinculado àquele fixture — não confiável, ignora e usa standings.
                    var grupoFixture = !string.IsNullOrWhiteSpace(detalhado.League.Group) &&
                        !detalhado.League.Group.Equals("Group Stage", StringComparison.OrdinalIgnoreCase)
                        ? detalhado.League.Group
                        : null;

                    // Só a fase de grupos resolve a letra do grupo (A, B, C...) via standings.
                    // Em qualquer outra fase (preliminares ou mata-mata: "Round of 32",
                    // "Round of 16", "Quarter-finals"...) o próprio Round é o identificador —
                    // senão um time que jogou a fase de grupos carregaria seu grupo antigo
                    // (ex.: "Group E") para o mata-mata.
                    var grupo = grupoFixture
                        ?? (EhFaseDeGrupos(detalhado.League.Round)
                            ? ((gruposPorTime.TryGetValue(detalhado.Teams.Home.Id, out var gCasa) ? gCasa : null)
                                ?? (gruposPorTime.TryGetValue(detalhado.Teams.Away.Id, out var gVis) ? gVis : null))
                            : detalhado.League.Round);

                    await IncluirOuAtualizarJogo(context, competicao,
                        detalhado, timeCasa, timeVis, grupo, season, cicloId, ct);

                    await context.SaveChangesAsync(ct);
                    jogosProcessados++;
                }
                catch (Exception ex)
                {
                    erros++;
                    var desc = $"{fx.Teams.Home.Name} x {fx.Teams.Away.Name}";
                    avisos.Add($"{desc}: {ex.Message}");
                    Log(context, cicloId, "Erro", "Fixture",
                        competicaoNome: competicao.Nome,
                        jogoDescricao: desc,
                        detalhes: ex.Message);
                    _logger.LogWarning(ex, "[ApiFoot] Erro ao processar fixture {Id}.", fx.Fixture.Id);
                }
            }

            Log(context, cicloId, "Ciclo", "Concluído",
                competicaoNome: competicao.Nome,
                detalhes: $"{jogosProcessados} jogos | {timesCreados} times criados | {erros} erros");
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[ApiFoot] Concluído: {J} jogos, {T} times criados, {E} erros.",
                jogosProcessados, timesCreados, erros);

            return (jogosProcessados, timesCreados, erros, avisos);
        }

        // ── Cache de respostas da API ─────────────────────────────────────────
        // Usado só nos endpoints "read-mostly" (dados de jogador/treinador/
        // estatísticas), consultados a cada carga de tela — economiza a cota
        // diária do plano e acelera as páginas. Os endpoints do fluxo de
        // sincronização (fixtures, standings, detalhes de fixture) NÃO passam
        // por aqui: lá o dado precisa estar fresco.
        private async Task<string> GetStringCachedAsync(string url, TimeSpan ttl, CancellationToken ct)
        {
            var chave = "apifoot:" + url;
            if (_cache.TryGetValue(chave, out string? emCache) && emCache != null)
                return emCache;

            var json = await _http.GetStringAsync(url, ct);
            _cache.Set(chave, json, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                // O MemoryCache global tem SizeLimit em bytes (ver Program.cs),
                // então toda entrada precisa declarar Size: ~2 bytes por char.
                Size = json.Length * 2L
            });
            return json;
        }

        // ── Busca de info de jogador por IdApi ───────────────────────────────

        public async Task<AfPlayerInfoResult?> BuscarInfoJogadorAsync(
            long idApi, CancellationToken ct = default)
        {
            var season = DateTime.UtcNow.Year;
            AfPlayerProfileEntry? entry = null;

            // Tenta temporada atual e anterior
            foreach (var s in new[] { season, season - 1 })
            {
                var json = await GetStringCachedAsync($"players?id={idApi}&season={s}", TimeSpan.FromHours(6), ct);
                var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfPlayerProfileEntry>>(json, _json);
                entry = resp?.Response.FirstOrDefault();
                if (entry != null) break;
            }

            if (entry?.Player == null) return null;

            DateTime? dataNasc = null;
            if (!string.IsNullOrEmpty(entry.Player.Birth?.Date) &&
                DateTime.TryParse(entry.Player.Birth.Date,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var d))
                dataNasc = d;

            return new AfPlayerInfoResult
            {
                DataNascimento = dataNasc,
                Nacionalidade  = entry.Player.Nationality,
                FotoUrl        = entry.Player.Photo,
                PrimeiroNome   = entry.Player.Firstname,
                UltimoNome     = entry.Player.Lastname,
                Altura         = ExtrairNumero(entry.Player.Height),
                Peso           = ExtrairNumero(entry.Player.Weight)
            };
        }

        // ── Perfil do jogador (players/profiles) — nacionalidade e nascimento ──
        // Não depende de temporada, ideal para atualizar dados cadastrais.
        public async Task<AfPlayerInfoResult?> BuscarPerfilJogadorAsync(
            long idApi, CancellationToken ct = default)
        {
            var json = await GetStringCachedAsync($"players/profiles?player={idApi}", TimeSpan.FromHours(24), ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfPlayerProfileEntry>>(json, _json);
            var player = resp?.Response.FirstOrDefault()?.Player;
            if (player == null) return null;

            DateTime? dataNasc = null;
            if (!string.IsNullOrEmpty(player.Birth?.Date) &&
                DateTime.TryParse(player.Birth.Date,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var d))
            {
                dataNasc = d;
            }
            else if (player.Age is int idade && idade > 0 && idade < 120)
            {
                // Sem data de nascimento na API: estima 01/01 do ano que resulta na idade informada.
                dataNasc = new DateTime(DateTime.Today.Year - idade, 1, 1);
            }

            return new AfPlayerInfoResult
            {
                DataNascimento = dataNasc,
                Nacionalidade  = player.Nationality,
                FotoUrl        = player.Photo,
                PrimeiroNome   = player.Firstname,
                UltimoNome     = player.Lastname,
                Altura         = ExtrairNumero(player.Height),
                Peso           = ExtrairNumero(player.Weight)
            };
        }

        // A api-football retorna altura/peso como string (ex.: "181", "72 kg") — extrai só os dígitos.
        private static int? ExtrairNumero(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            var digitos = new string(valor.Where(char.IsDigit).ToArray());
            return int.TryParse(digitos, out var n) && n > 0 ? n : null;
        }

        // Resolve (ou cria) a Nacionalidade a partir do nome retornado pela API.
        public static Task<Nacionalidade?> ResolverOuCriarNacionalidadePublicAsync(
            FutebolContext context, string nomeRaw, CancellationToken ct = default) =>
            ResolverOuCriarNacionalidade(context, nomeRaw, ct);

        public async Task<List<AfPlayerSeasonStats>> BuscarEstatisticasTemporadaAsync(
            long idApi, int season, CancellationToken ct = default)
        {
            var json = await GetStringCachedAsync($"players?id={idApi}&season={season}", TimeSpan.FromHours(6), ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfPlayerSeasonEntry>>(json, _json);
            return resp?.Response.FirstOrDefault()?.Statistics ?? new();
        }

        // ── Importa o elenco completo de um time (para jogos futuros, sem lineup) ─

        /// <summary>
        /// Busca o elenco do time na api-football (players/squads) e cria os
        /// jogadores que ainda não existem no banco. Usado para listar jogadores
        /// disponíveis em jogos futuros, que ainda não têm escalação publicada
        /// (e portanto nenhum jogador foi descoberto via importação de lineup).
        /// </summary>
        public async Task<int> ImportarElencoAsync(
            FutebolContext context, Time time, CancellationToken ct = default)
        {
            if (time.IdApi <= 0) return 0;

            try
            {
                // TTL curto: importar elenco é ação explícita do usuário — o cache
                // aqui só deduplica cliques repetidos, sem esconder mudança de elenco.
                var json = await GetStringCachedAsync($"players/squads?team={time.IdApi}", TimeSpan.FromMinutes(15), ct);
                var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfSquadEntry>>(json, _json);
                var jogadoresApi = resp?.Response.FirstOrDefault()?.Players ?? new();

                var idsApi = jogadoresApi.Select(p => (long)p.Id).ToList();
                var existentes = await context.Jogadores
                    .Where(j => j.IdApi != null && idsApi.Contains(j.IdApi.Value))
                    .ToListAsync(ct);
                var idsExistentes = existentes.Select(j => j.IdApi!.Value).ToHashSet();

                var criados = 0;
                var semAlturaPeso = new List<Jogador>();
                foreach (var p in jogadoresApi)
                {
                    if (idsExistentes.Contains(p.Id)) continue;

                    var jogador = new Jogador
                    {
                        Nome         = p.Name,
                        PrimeiroNome = p.Firstname,
                        UltimoNome   = p.Lastname,
                        Posicao      = MapearPosicao(p.Position),
                        NumeroCamisa = p.Number,
                        FotoUrl      = p.Photo,
                        TimeId       = time.Id,
                        SelecaoId    = time.EhSelecao ? time.Id : null,
                        IdApi        = p.Id,
                        Atualizado   = true,
                        DtInc        = DateTime.UtcNow
                    };
                    context.Jogadores.Add(jogador);
                    semAlturaPeso.Add(jogador);
                    criados++;
                }

                if (criados > 0) await context.SaveChangesAsync(ct);

                // Jogadores já cadastrados que ainda não têm altura/peso também entram na busca.
                semAlturaPeso.AddRange(existentes.Where(j => j.Altura == null || j.Peso == null));

                // players/squads não traz altura/peso — busca em players/profiles só para
                // quem ainda não tem os dados, evitando gastar requisições à toa.
                var alterados = false;
                foreach (var jogador in semAlturaPeso)
                {
                    try
                    {
                        var info = await BuscarPerfilJogadorAsync(jogador.IdApi!.Value, ct);
                        if (info != null && (info.Altura.HasValue || info.Peso.HasValue))
                        {
                            if (info.Altura.HasValue) jogador.Altura = info.Altura;
                            if (info.Peso.HasValue) jogador.Peso = info.Peso;
                            alterados = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[ApiFoot] Não foi possível buscar altura/peso do jogador {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                    }
                }

                if (alterados) await context.SaveChangesAsync(ct);
                return criados;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ApiFoot] Falha ao importar elenco do time IdApi={IdApi}", time.IdApi);
                return 0;
            }
        }

        // ── Reimporta escalação de um jogo ───────────────────────────────────

        public async Task<(bool ok, string msg)> ForcarReimportarEscalacaoAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            if (jogo == null) return (false, "Jogo não encontrado.");

            if (string.IsNullOrEmpty(jogo.LinkDetalhes) ||
                !jogo.LinkDetalhes.StartsWith("apifoot:", StringComparison.OrdinalIgnoreCase))
                return (false, "Este jogo não tem link da api-football (LinkDetalhes deve ser apifoot:ID).");

            var parts = jogo.LinkDetalhes.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out var fixtureId))
                return (false, "Link inválido.");

            var fx = await BuscarDetalhesFixtureAsync(fixtureId, ct);
            if (fx == null)
                return (false, "Não foi possível obter os dados da api-football.");

            var cicloId = Guid.NewGuid();
            await ImportarLineupEEventos(context, jogo,
                fx, jogo.TimeCasa, jogo.TimeVisitante, cicloId, ct);

            // Atualiza as formações do jogo a partir da API — antes só o fluxo de
            // sincronização fazia isso; a reimportação deixava os selects de
            // formação (Inicial/Final) sem preencher.
            var formCasaStr = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Home.Id)?.Formation;
            var formVisStr  = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Away.Id)?.Formation;
            if (!string.IsNullOrWhiteSpace(formCasaStr))
                jogo.FormacaoCasaId = (await ObterOuCriarFormacao(context, formCasaStr, ct)).Id;
            if (!string.IsNullOrWhiteSpace(formVisStr))
                jogo.FormacaoVisitanteId = (await ObterOuCriarFormacao(context, formVisStr, ct)).Id;

            if (fx.Statistics.Any())
                jogo.EstatisticasJson = MontarEstatisticasJson(fx);

            await context.SaveChangesAsync(ct);

            return (true, "Escalação reimportada com sucesso.");
        }

        // ── Busca grupo de um jogo ────────────────────────────────────────────

        public async Task<string?> BuscarGrupoDoJogoAsync(
            string? linkDetalhes, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(linkDetalhes) ||
                !linkDetalhes.StartsWith("apifoot:", StringComparison.OrdinalIgnoreCase))
                return null;

            var parts = linkDetalhes.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out var fixtureId))
                return null;

            var fx = await BuscarDetalhesFixtureAsync(fixtureId, ct);
            if (fx == null) return null;

            // "Group Stage" é um texto genérico que a api-football às vezes devolve em
            // league.group/league.round antes do grupo específico (A, B, C...) ser
            // vinculado ao fixture — não confiável, ignora e busca via standings.
            if (!string.IsNullOrWhiteSpace(fx.League.Group) &&
                !fx.League.Group.Equals("Group Stage", StringComparison.OrdinalIgnoreCase))
                return fx.League.Group;

            // Fora da fase de grupos (preliminares e mata-mata) o Round é o identificador;
            // nunca buscar standings, pois o time pode ter jogado a fase de grupos e
            // carregaria seu grupo antigo (ex.: "Group E") para o mata-mata.
            if (!EhFaseDeGrupos(fx.League.Round))
                return fx.League.Round;

            var gruposPorTime = await BuscarGruposPorTimeAsync(fx.League.Id, fx.League.Season, ct);
            if (gruposPorTime.TryGetValue(fx.Teams.Home.Id, out var gCasa)) return gCasa;
            if (gruposPorTime.TryGetValue(fx.Teams.Away.Id, out var gVis)) return gVis;

            return !string.IsNullOrWhiteSpace(fx.League.Round) &&
                !fx.League.Round.Equals("Group Stage", StringComparison.OrdinalIgnoreCase)
                ? fx.League.Round
                : null;
        }

        // ── Atualiza apenas jogos agendados (sem placar) ─────────────────────

        public async Task<(int atualizados, int erros)> AtualizarJogosAgendadosAsync(
            FutebolContext context,
            int? competicaoId = null,
            CancellationToken ct = default)
        {
            var agora = DateTime.UtcNow;

            var query = context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j =>
                    (j.PlacarCasa == null || j.PlacarVisitante == null) &&
                    j.Data.HasValue && j.Data.Value <= agora &&
                    j.LinkDetalhes != null && j.LinkDetalhes.StartsWith("apifoot:"));

            if (competicaoId.HasValue)
                query = query.Where(j => j.CompeticaoId == competicaoId.Value);

            var jogos = await query.ToListAsync(ct);

            _logger.LogInformation("[ApiFoot] {N} jogos agendados passados para verificar.", jogos.Count);

            int atualizados = 0, erros = 0;

            foreach (var jogo in jogos)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var parts = jogo.LinkDetalhes!.Split(':');
                    if (parts.Length < 2 || !long.TryParse(parts[1], out var fixtureId))
                        continue;

                    var fx = await BuscarDetalhesFixtureAsync(fixtureId, ct);
                    if (fx == null) continue;

                    var finalizado = StatusFinalizados.Contains(fx.Fixture.Status.Short);
                    if (!finalizado) continue;

                    if (fx.Goals.Home.HasValue && fx.Goals.Away.HasValue)
                    {
                        jogo.PlacarCasa = fx.Goals.Home;
                        jogo.PlacarVisitante = fx.Goals.Away;
                        jogo.Status = "Finalizado";
                        jogo.Atualizado = 1;

                        if (fx.Statistics.Any())
                            jogo.EstatisticasJson = MontarEstatisticasJson(fx);

                        if (fx.Lineups.Any())
                        {
                            var temEscalacao = await context.Escalacoes
                                .AnyAsync(e => e.JogoId == jogo.Id && e.JogadorId != null, ct);
                            if (!temEscalacao)
                                await ImportarLineupEEventos(context, jogo,
                                    fx, jogo.TimeCasa, jogo.TimeVisitante, Guid.NewGuid(), ct);
                        }

                        await context.SaveChangesAsync(ct);
                        atualizados++;

                        _logger.LogInformation("[ApiFoot] Placar atualizado: {C} {PC}x{PV} {V}",
                            jogo.TimeCasa?.Nome, fx.Goals.Home, fx.Goals.Away, jogo.TimeVisitante?.Nome);
                    }

                    // Pausa para respeitar rate-limit da API
                    await Task.Delay(700, ct);
                }
                catch (Exception ex)
                {
                    erros++;
                    _logger.LogWarning(ex, "[ApiFoot] Erro ao atualizar jogo id={Id}.", jogo.Id);
                }
            }

            _logger.LogInformation("[ApiFoot] Atualização concluída: {A} atualizados, {E} erros.", atualizados, erros);
            return (atualizados, erros);
        }

        // ── Chamadas HTTP ─────────────────────────────────────────────────────

        public async Task<List<AfCoachFull>> BuscarTreinadorApiAsync(
            string nome, long? teamId = null, CancellationToken ct = default)
        {
            long? team = teamId is long t && t > 0 ? t : null;

            async Task<List<AfCoachFull>> Buscar(string termo)
            {
                var url = $"coachs?search={Uri.EscapeDataString(termo)}";
                if (team is long tt) url += $"&team={tt}";
                var json = await GetStringCachedAsync(url, TimeSpan.FromHours(6), ct);
                var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfCoachFull>>(json, _json);
                return resp?.Response ?? new();
            }

            var nomeTrim = (nome ?? "").Trim();

            // A api-football casa o termo de busca pelos campos name/lastname (não firstname),
            // então o nome completo ou um nome do meio às vezes não acha. Com o time informado
            // (resultado já desambiguado pelo /team), tenta o nome inteiro e depois cada parte
            // (>= 3 chars) até encontrar — ex.: "Francisco Zubeldia Luis" falha em "Luis" e
            // "Francisco", mas acha em "Zubeldia".
            var termos = new List<string>();
            if (nomeTrim.Length >= 3) termos.Add(nomeTrim);
            if (team is not null)
                foreach (var parte in nomeTrim.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (parte.Length >= 3 &&
                        !termos.Contains(parte, StringComparer.OrdinalIgnoreCase))
                        termos.Add(parte);

            foreach (var termo in termos)
            {
                var resultado = await Buscar(termo);
                if (resultado.Count > 0) return resultado;
            }
            return new();
        }

        // ── Resolução treinador "stub" → registro completo ───────────────────

        // A api-football às vezes mantém DOIS registros para o mesmo treinador: um completo
        // (firstname/lastname/age/nationality/birth.date e career com todas as passagens),
        // vinculado a um clube antigo, e um "stub" criado quando ele assume um clube novo
        // (só name/photo/team/career com a passagem atual — demais campos null). Um registro
        // é considerado stub quando não tem nenhum desses três dados pessoais.
        private static bool EhStubTreinador(AfCoachFull c) =>
            string.IsNullOrEmpty(c.Birth?.Date) && string.IsNullOrEmpty(c.Nationality) && c.Age == null;

        /// <summary>
        /// Busca o treinador na api-football e resolve o caso de registro "stub" (ver
        /// <see cref="EhStubTreinador"/>), tentando localizar o registro completo do mesmo
        /// técnico via casamento de nome por tokens. Reaproveitado por BuscarFoto e pela
        /// importação de histórico via API, para os dois nunca travarem no stub por engano.
        /// </summary>
        /// <returns>
        /// Escolhido: registro a usar (completo, se resolvido; senão o melhor encontrado).
        /// RegistrosDoMesmoTecnico: registros a unir para montar o histórico de carreira
        /// (completo + stub, quando o stub foi resolvido; só o Escolhido, caso contrário).
        /// Ambiguo: true quando o escolhido é um stub e há mais de um candidato completo
        /// compatível pelo nome — nesse caso não arriscamos escolher o técnico errado.
        /// </returns>
        public async Task<(AfCoachFull? Escolhido, List<AfCoachFull> RegistrosDoMesmoTecnico, bool Ambiguo)>
            ResolverTreinadorApiAsync(
                string nome, long? teamApiId, long? idApiAtual, CancellationToken ct = default)
        {
            var resultados = await BuscarTreinadorApiAsync(nome, teamApiId, ct);

            // Fallback: se nada vier com o time, busca só pelo nome.
            if (!resultados.Any() && teamApiId is long)
                resultados = await BuscarTreinadorApiAsync(nome, null, ct);

            if (!resultados.Any())
                return (null, new List<AfCoachFull>(), false);

            // Se o treinador já tem IdApi, trava no técnico com aquele id. Senão, prefere o
            // resultado cujo time atual bate com o time cadastrado, depois o que tiver mais dados.
            var melhor = resultados
                .OrderByDescending(r => (idApiAtual != null && r.Id == idApiAtual) ? 100 : 0)
                .ThenByDescending(r => r.Team?.Id == teamApiId ? 10 : 0)
                .ThenByDescending(r => r.Age.HasValue ? 1 : 0)
                .First();

            if (!EhStubTreinador(melhor))
                return (melhor, new List<AfCoachFull> { melhor }, false);

            // O escolhido é um stub — mesmo tendo vencido pela trava do IdApi, ela não pode
            // cimentar um stub. Refaz a busca sem filtro de time e procura, entre os
            // candidatos NÃO-stub, o(s) que casam pelo nome (por tokens, sem acento).
            // A API casa o termo pelos campos name/lastname, então o nome do stub inteiro
            // às vezes não acha o registro completo (ex.: stub "Ceni Rogerio" — o completo
            // "Rogério Ceni" só aparece buscando "ceni"). Busca o nome inteiro e também
            // cada token significativo, unindo os resultados por id.
            var tokensBusca = TokensSignificativos(melhor.Name, nome);
            var candidatos = new Dictionary<int, AfCoachFull>();
            foreach (var termo in new[] { nome.Trim() }.Concat(tokensBusca)
                         .Where(t => t.Length >= 3)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var c in await BuscarTreinadorApiAsync(termo, null, ct))
                    if (c.Id is int cid) candidatos[cid] = c;
            }

            // Compatível = candidato que contém TODOS os tokens do nome do stub. Exigir todos
            // (e não qualquer um) evita que a busca por um token de primeiro nome (ex.: "rogerio")
            // marque como compatível cada "Rogério" da API — {ceni, rogerio} casa com
            // "Rogério Mücke Ceni", mas não com "Rogério Micale".
            var tokensStub = TokensSignificativos(melhor.Name);
            if (tokensStub.Count == 0) tokensStub = TokensSignificativos(nome);

            var compativeis = candidatos.Values
                .Where(c => c.Id != melhor.Id && !EhStubTreinador(c))
                .Where(c =>
                {
                    var tokensCandidato = TokensSignificativos(c.Name, c.Firstname, c.Lastname);
                    return tokensStub.All(tokensCandidato.Contains);
                })
                .ToList();

            if (compativeis.Count == 1)
            {
                var completo = compativeis[0];
                return (completo, new List<AfCoachFull> { completo, melhor }, false);
            }

            if (compativeis.Count > 1)
            {
                // Homônimos: não dá para saber qual é o técnico certo — mantém o stub e
                // sinaliza a ambiguidade para quem chamou decidir a mensagem ao usuário.
                return (melhor, new List<AfCoachFull> { melhor }, true);
            }

            // Nenhum candidato completo compatível encontrado — mantém o stub mesmo.
            return (melhor, new List<AfCoachFull> { melhor }, false);
        }

        /// <summary>
        /// True quando dois nomes têm exatamente os mesmos tokens significativos, em qualquer
        /// ordem e ignorando acentos — ex.: "Ceni Rogerio" (nome invertido herdado de um stub
        /// da api-football) equivale a "Rogério Ceni". Usado para decidir se o nome local pode
        /// ser corrigido pelo nome canônico da API sem sobrescrever um nome escolhido à mão.
        /// </summary>
        public static bool NomesEquivalentes(string? a, string? b)
        {
            var tokensA = TokensSignificativos(a);
            var tokensB = TokensSignificativos(b);
            return tokensA.Count > 0 && tokensA.Count == tokensB.Count &&
                   tokensA.All(tokensB.Contains);
        }

        // Tokens "significativos" de um ou mais nomes: minúsculas, sem acento, descartando
        // iniciais abreviadas (1–2 chars ou terminando em '.'). Usado para casar o nome de um
        // registro stub (ex.: "Paulo Pezzolano") com o do registro completo (ex.: "P. Pezzolano",
        // firstname/lastname "Pezzolano Suárez").
        private static List<string> TokensSignificativos(params string?[] nomes)
        {
            var tokens = new List<string>();
            foreach (var nome in nomes)
            {
                if (string.IsNullOrWhiteSpace(nome)) continue;
                foreach (var parte in nome.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (parte.EndsWith(".")) continue; // inicial abreviada, ex.: "P."
                    var normalizado = RemoverAcentos(parte).ToLowerInvariant();
                    if (normalizado.Length <= 2) continue; // inicial sem ponto, ex.: "P"
                    if (!tokens.Contains(normalizado)) tokens.Add(normalizado);
                }
            }
            return tokens;
        }

        private static string RemoverAcentos(string texto)
        {
            var decomposto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // ── União do histórico de carreira (para importação via API) ─────────

        // Uma única data "yyyy-MM-dd" (ou null = passagem atual).
        private static DateTime? ParseDataCarreira(string? data) =>
            DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt : null;

        /// <summary>
        /// Une o career de todos os registros do mesmo técnico (ver ResolverTreinadorApiAsync),
        /// deduplicando por time + mês/ano de início (o stub costuma repetir, com o mesmo mês,
        /// a passagem atual que às vezes falta no career do registro completo) e ordenando da
        /// passagem mais recente para a mais antiga.
        /// </summary>
        public static List<AfCoachCareerItem> UnirCarreiras(IEnumerable<AfCoachFull> registros)
        {
            var vistos = new HashSet<(int TeamId, int Ano, int Mes)>();
            var unidas = new List<AfCoachCareerItem>();

            foreach (var registro in registros)
            {
                foreach (var item in registro.Career ?? new List<AfCoachCareerItem>())
                {
                    if (item.Team == null) continue;
                    var inicio = ParseDataCarreira(item.Start);
                    // Team.Id pode vir null (seleções/clubes fora da base da API) —
                    // esses itens são só exibidos na pré-visualização, nunca salvos.
                    var chave = (item.Team.Id ?? 0, inicio?.Year ?? 0, inicio?.Month ?? 0);
                    if (!vistos.Add(chave)) continue;
                    unidas.Add(item);
                }
            }

            return unidas
                .OrderByDescending(i => ParseDataCarreira(i.Start) ?? DateTime.MinValue)
                .ToList();
        }

        public async Task<AfTeamSeasonStats?> BuscarEstatisticasTimeAsync(
            int teamId, int leagueId, int season, CancellationToken ct = default)
        {
            var url = $"teams/statistics?league={leagueId}&season={season}&team={teamId}";
            var json = await GetStringCachedAsync(url, TimeSpan.FromHours(1), ct);
            var resp = JsonSerializer.Deserialize<ApiFootballSingleResponse<AfTeamSeasonStats>>(json, _json);
            return resp?.Response;
        }

        public async Task<List<AfFixture>> BuscarH2HAsync(
            int teamId1, int teamId2, int last = 5, CancellationToken ct = default)
        {
            var url = $"fixtures/headtohead?h2h={teamId1}-{teamId2}&last={last}";
            var json = await GetStringCachedAsync(url, TimeSpan.FromHours(1), ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfFixture>>(json, _json);
            return resp?.Response ?? new();
        }

        private async Task<List<AfFixture>> BuscarFixturesAsync(
            int leagueId, int season, CancellationToken ct)
        {
            var url = $"fixtures?league={leagueId}&season={season}";
            var json = await _http.GetStringAsync(url, ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfFixture>>(json, _json);
            return resp?.Response ?? new();
        }

        // Identifica se o Round pertence à fase de grupos. Só nesse caso a letra do grupo
        // (A, B, C...) é resolvida via standings; em qualquer outra fase (preliminares ou
        // mata-mata) o próprio Round é o identificador. Round vazio é tratado como fase de
        // grupos para preservar o fallback via standings de jogos sem round preenchido.
        private static bool EhFaseDeGrupos(string? round) =>
            string.IsNullOrWhiteSpace(round) ||
            round.Contains("Group", StringComparison.OrdinalIgnoreCase);

        // Mapa TeamId → nome do grupo (ex.: "Group A"), a partir do endpoint /standings.
        // Necessário porque fixtures só trazem o grupo depois que os jogos da fase
        // de grupos existem; standings tem o grupo de cada time desde o sorteio.
        private async Task<Dictionary<int, string>> BuscarGruposPorTimeAsync(
            int leagueId, int season, CancellationToken ct)
        {
            var mapa = new Dictionary<int, string>();
            try
            {
                var url = $"standings?league={leagueId}&season={season}";
                var json = await _http.GetStringAsync(url, ct);
                var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfStandingsEntry>>(json, _json);
                var grupos = resp?.Response.FirstOrDefault()?.League.Standings ?? new();

                foreach (var grupo in grupos)
                    foreach (var entrada in grupo)
                        if (entrada.Team.Id > 0 &&
                            !string.IsNullOrWhiteSpace(entrada.Group) &&
                            !entrada.Group.Equals("Group Stage", StringComparison.OrdinalIgnoreCase))
                            mapa[entrada.Team.Id] = entrada.Group!;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ApiFoot] Falha ao buscar standings/grupos league={L} season={S}", leagueId, season);
            }
            return mapa;
        }

        // Serializa fx.Statistics (posse, finalizações, etc.) num JSON simples
        // [{ "TimeId": 22, "Stats": { "Ball Possession": "48%", ... } }, ...] guardado em Jogo.EstatisticasJson.
        private static string? MontarEstatisticasJson(AfFixture fx)
        {
            if (!fx.Statistics.Any()) return null;

            var lista = fx.Statistics.Select(ts => new
            {
                TimeId = ts.Team.Id,
                Stats = ts.Statistics
                    .Where(s => !string.IsNullOrWhiteSpace(s.Type))
                    .GroupBy(s => s.Type)
                    .ToDictionary(g => g.Key, g => g.First().Value)
            }).ToList();

            return JsonSerializer.Serialize(lista);
        }

        private async Task<AfFixture?> BuscarDetalhesFixtureAsync(long fixtureId, CancellationToken ct)
        {
            var url = $"fixtures?id={fixtureId}";
            var json = await _http.GetStringAsync(url, ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfFixture>>(json, _json);
            return resp?.Response.FirstOrDefault();
        }

        // ── Persistência ──────────────────────────────────────────────────────

        private async Task IncluirOuAtualizarJogo(
            FutebolContext context,
            Competicao competicao,
            AfFixture fx,
            Time timeCasa,
            Time timeVis,
            string? grupo,
            int season,
            Guid cicloId,
            CancellationToken ct)
        {
            var data = fx.Fixture.Date.HasValue
                ? fx.Fixture.Date.Value.UtcDateTime
                : (DateTime?)null;

            var rodada = ParseRodada(fx.League.Round);
            var fixtureIdStr = $"apifoot:{fx.Fixture.Id}";
            var finalizado = StatusFinalizados.Contains(fx.Fixture.Status.Short);

            // Anti-duplicata
            var existente = await context.Jogos
                .FirstOrDefaultAsync(j =>
                    j.CompeticaoId == competicao.Id &&
                    j.TimeCasaId == timeCasa.Id &&
                    j.TimeVisitanteId == timeVis.Id &&
                    (j.Rodada == rodada || j.LinkDetalhes == fixtureIdStr), ct);

            if (existente != null)
            {
                // Atualiza os dados da partida mesmo que o jogo já tenha sido analisado.
                // As posições em campo (escalação) e as observações nunca são tocadas aqui.
                if (data.HasValue) existente.Data = data;
                if (rodada > 0) existente.Rodada = rodada;
                existente.Temporada = season;
                existente.LinkDetalhes = fixtureIdStr;
                // Jogo de mata-mata que ficou com a letra de grupo antiga ("Group E") porque
                // a resolução anterior caía no standings da fase de grupos — auto-corrige.
                var grupoStaleDeGrupos = !EhFaseDeGrupos(fx.League.Round) &&
                    existente.Grupo != null &&
                    existente.Grupo.StartsWith("Group", StringComparison.OrdinalIgnoreCase);
                var grupoDesatualizado = string.IsNullOrWhiteSpace(existente.Grupo) ||
                    existente.Grupo.Equals("Group Stage", StringComparison.OrdinalIgnoreCase) ||
                    grupoStaleDeGrupos;
                if (grupoDesatualizado && !string.IsNullOrWhiteSpace(grupo))
                    existente.Grupo = grupo;

                if (fx.Statistics.Any())
                    existente.EstatisticasJson = MontarEstatisticasJson(fx);

                if (finalizado && fx.Goals.Home.HasValue &&
                    (existente.PlacarCasa != fx.Goals.Home ||
                     existente.PlacarVisitante != fx.Goals.Away))
                {
                    existente.PlacarCasa = fx.Goals.Home;
                    existente.PlacarVisitante = fx.Goals.Away;
                    existente.Status = "Finalizado";
                    existente.Atualizado = 1;
                }

                if (finalizado)
                {
                    // Disputa de pênaltis (mata-mata): só vem preenchida quando houve.
                    existente.PenaltisCasa = fx.Score.Penalty.Home;
                    existente.PenaltisVisitante = fx.Score.Penalty.Away;

                    // Jogo criado como "Agendado" nasceu com a formação padrão (a 1ª da tabela).
                    // Agora que a lineup real chegou, corrige a formação do jogo para bater com o
                    // campo (fonte de verdade da API). Só atualiza a formação do jogo — as posições
                    // em campo (escalação) e observações continuam preservadas.
                    if (fx.Lineups.Any())
                    {
                        var formCasaStr = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Home.Id)?.Formation;
                        var formVisStr  = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Away.Id)?.Formation;
                        if (!string.IsNullOrWhiteSpace(formCasaStr))
                            existente.FormacaoCasaId = (await ObterOuCriarFormacao(context, formCasaStr, ct)).Id;
                        if (!string.IsNullOrWhiteSpace(formVisStr))
                            existente.FormacaoVisitanteId = (await ObterOuCriarFormacao(context, formVisStr, ct)).Id;
                    }

                    var temEscalacao = await context.Escalacoes
                        .AnyAsync(e => e.JogoId == existente.Id && e.JogadorId != null, ct);

                    if (!temEscalacao && fx.Lineups.Any())
                    {
                        // Primeira importação: traz escalação, eventos e estatísticas.
                        await ImportarLineupEEventos(context, existente,
                            fx, timeCasa, timeVis, cicloId, ct);
                    }
                    else if (fx.Players.Any())
                    {
                        // Jogo já escalado/analisado: atualiza só as estatísticas reais dos
                        // jogadores, preservando as posições em campo e as observações.
                        await SalvarEstatisticasJogadoresAsync(context, existente, fx, ct);
                    }
                }

                return;
            }

            // Novo jogo
            var formacaoCasaStr = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Home.Id)?.Formation;
            var formacaoVisStr  = fx.Lineups.FirstOrDefault(l => l.Team.Id == fx.Teams.Away.Id)?.Formation;

            var formCasa = await ObterOuCriarFormacao(context, formacaoCasaStr, ct);
            var formVis  = await ObterOuCriarFormacao(context, formacaoVisStr, ct);

            var jogo = new Jogo
            {
                CompeticaoId      = competicao.Id,
                TimeCasa          = timeCasa,
                TimeVisitante     = timeVis,
                Data              = data,
                Rodada            = rodada,
                Temporada         = season,
                PlacarCasa        = finalizado ? fx.Goals.Home : null,
                PlacarVisitante   = finalizado ? fx.Goals.Away : null,
                PenaltisCasa      = finalizado ? fx.Score.Penalty.Home : null,
                PenaltisVisitante = finalizado ? fx.Score.Penalty.Away : null,
                Grupo             = grupo,
                Status            = finalizado ? "Finalizado" : "Agendado",
                Atualizado        = finalizado ? 1 : 0,
                FormacaoCasaId    = formCasa.Id,
                FormacaoVisitanteId = formVis.Id,
                LinkDetalhes      = fixtureIdStr,
                Estadio           = fx.Fixture.Venue?.Name,
                Arbitro           = fx.Fixture.Referee,
                EstatisticasJson  = fx.Statistics.Any() ? MontarEstatisticasJson(fx) : null
            };

            context.Jogos.Add(jogo);
            await context.SaveChangesAsync(ct);

            Log(context, cicloId, "Jogo", "Criado",
                competicaoNome: competicao.Nome,
                timeNome: $"{timeCasa.Nome} × {timeVis.Nome}",
                jogoDescricao: rodada > 0
                    ? $"Rodada {rodada} | {data?.ToString("dd/MM/yyyy") ?? "sem data"}"
                    : data?.ToString("dd/MM/yyyy") ?? "sem data",
                detalhes: finalizado
                    ? $"Placar: {fx.Goals.Home}×{fx.Goals.Away}"
                    : "Agendado");

            if (finalizado && fx.Lineups.Any())
                await ImportarLineupEEventos(context, jogo, fx, timeCasa, timeVis, cicloId, ct);
            else
                AdicionarEscalacaoVazia(context, jogo, formCasa, formVis);
        }

        private async Task ImportarLineupEEventos(
            FutebolContext context,
            Jogo jogo,
            AfFixture fx,
            Time timeCasa,
            Time timeVis,
            Guid cicloId,
            CancellationToken ct)
        {
            // Remove apenas escalações compartilhadas (UsuarioId == null) — as personalizadas por usuário são preservadas
            var escalOld = await context.Escalacoes.Where(e => e.JogoId == jogo.Id && e.UsuarioId == null).ToListAsync(ct);
            context.Escalacoes.RemoveRange(escalOld);
            var golsOld = await context.Gols.Where(g => g.JogoId == jogo.Id).ToListAsync(ct);
            context.Gols.RemoveRange(golsOld);
            var assistOld = await context.Assistencias.Where(a => a.JogoId == jogo.Id).ToListAsync(ct);
            context.Assistencias.RemoveRange(assistOld);
            var cartoesOld = await context.Cartoes.Where(c => c.JogoId == jogo.Id).ToListAsync(ct);
            context.Cartoes.RemoveRange(cartoesOld);
            var subsOld = await context.Substituicoes.Where(s => s.JogoId == jogo.Id).ToListAsync(ct);
            context.Substituicoes.RemoveRange(subsOld);
            var penPerdOld = await context.PenaltisPerdidos.Where(p => p.JogoId == jogo.Id).ToListAsync(ct);
            context.PenaltisPerdidos.RemoveRange(penPerdOld);
            var penDispOld = await context.PenaltisDisputa.Where(p => p.JogoId == jogo.Id).ToListAsync(ct);
            context.PenaltisDisputa.RemoveRange(penDispOld);

            // Mapa: IdApi → Jogador local (para vincular eventos)
            var jogadorMap = new Dictionary<int, Jogador>();

            // Salva as cores dos uniformes vindas da lineup
            foreach (var lineup in fx.Lineups)
            {
                var cor = lineup.Team.Colors?.Player;
                if (cor != null && !string.IsNullOrWhiteSpace(cor.Primary))
                {
                    if (lineup.Team.Id == fx.Teams.Home.Id)
                    {
                        jogo.CorCamisaCasa   = cor.Primary;
                        jogo.CorNumeroCasa   = cor.Number;
                    }
                    else
                    {
                        jogo.CorCamisaVisitante   = cor.Primary;
                        jogo.CorNumeroVisitante   = cor.Number;
                    }
                }
            }

            // Lineups
            foreach (var lineup in fx.Lineups)
            {
                var isTimeCasa = lineup.Team.Id == fx.Teams.Home.Id;
                var time = isTimeCasa ? timeCasa : timeVis;

                await AtualizarTreinadorTime(context, time, lineup.Coach, ct);

                var formacaoNome = lineup.Formation ?? "não informada";
                _logger.LogInformation(
                    "[ApiFoot] Formação {Time}: {Formacao} (jogo: {Casa} x {Vis})",
                    lineup.Team.Name, formacaoNome,
                    fx.Teams.Home.Name, fx.Teams.Away.Name);

                Log(context, cicloId, "Formação", "Detectada",
                    jogoDescricao: $"{fx.Teams.Home.Name} × {fx.Teams.Away.Name}",
                    timeNome: lineup.Team.Name,
                    detalhes: $"Formação JSON: {formacaoNome}");

                // Carrega posições da formação cadastrada (se existir) — é o layout
                // customizado pelo usuário em /Formacoes, que deve ser respeitado.
                var formacao = !string.IsNullOrWhiteSpace(lineup.Formation)
                    ? await context.Formacoes
                        .Include(f => f.Posicoes)
                        .FirstOrDefaultAsync(f => f.Nome == lineup.Formation, ct)
                    : null;

                var posicoes = formacao?.Posicoes?
                    .OrderBy(p => p.Ordem)
                    .ToList() ?? new();

                if (formacao == null && !string.IsNullOrWhiteSpace(lineup.Formation))
                    Log(context, cicloId, "Formação", "NãoCadastrada",
                        jogoDescricao: $"{fx.Teams.Home.Name} × {fx.Teams.Away.Name}",
                        timeNome: lineup.Team.Name,
                        detalhes: $"Formação '{lineup.Formation}' não encontrada na tabela formacoes — posições X/Y serão 0");

                // O "grid" (linha:coluna) que a api-football devolve pode ter um número de
                // linhas/colunas diferente do cadastrado em /Formacoes para a mesma formação
                // nominal (ex.: zagueiros chegando na "linha 3" em vez da "linha 2"). Por isso
                // o mapeamento linha/coluna → posição cadastrada é proporcional (não por índice
                // exato), evitando jogadores caindo fora do campo quando os grids não coincidem.
                var gridsValidos = lineup.StartXI
                    .Select(lp => ParseGrid(lp.Player.Grid))
                    .Where(g => g != null)
                    .Select(g => g!.Value)
                    .ToList();

                var totalLinhasGrid = gridsValidos.Count == 0 ? 0 : gridsValidos.Max(g => g.linha);
                var colunasPorLinhaGrid = gridsValidos
                    .GroupBy(g => g.linha)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.coluna));

                foreach (var lp in lineup.StartXI)
                {
                    var posFormacao = ResolverPosicaoFormacao(
                        lp.Player.Grid, posicoes, totalLinhasGrid, colunasPorLinhaGrid);
                    await AdicionarEscalacaoJogador(context, jogo, lp.Player, time,
                        isTimeCasa, true, "INICIAL", jogadorMap, posFormacao, ct);
                }

                foreach (var lp in lineup.Substitutes)
                    await AdicionarEscalacaoJogador(context, jogo, lp.Player, time,
                        isTimeCasa, false, "INICIAL", jogadorMap, null, ct);
            }

            await context.SaveChangesAsync(ct);

            // Eventos
            var ordemPenaltiDisputa = 0;
            var penaltisDisputaNovos = new List<PenaltiDisputa>();
            foreach (var ev in fx.Events)
            {
                var isTimeCasa = ev.Team.Id == fx.Teams.Home.Id;
                var minuto = ev.Time.Elapsed;

                switch (ev.Type.ToLowerInvariant())
                {
                    case "goal":
                    {
                        if (ev.Player.Id == null) break;
                        var jogador = await ResolverJogador(context, ev.Player.Id.Value,
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct, jogo: jogo);
                        if (jogador == null) break;

                        // Disputa de pênaltis (mata-mata): a api-football marca cada cobrança
                        // com comments "Penalty Shootout". Convertido = detail "Penalty",
                        // perdido/defendido = detail "Missed Penalty". Registra a cobrança
                        // (com a ordem) sem somar ao placar do tempo normal nem aos gols.
                        if (ev.Comments?.Contains("Shootout", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var penDisputa = new PenaltiDisputa
                            {
                                JogoId = jogo.Id, JogadorId = jogador.Id,
                                IsTimeCasa = isTimeCasa,
                                Convertido = ev.Detail?.Contains("Missed", StringComparison.OrdinalIgnoreCase) != true,
                                Ordem = ++ordemPenaltiDisputa
                            };
                            context.PenaltisDisputa.Add(penDisputa);
                            penaltisDisputaNovos.Add(penDisputa);
                            break;
                        }

                        // A api-football marca pênalti perdido/defendido como type "Goal"
                        // com detail "Missed Penalty" — NÃO é gol. Registra como evento
                        // próprio (com minuto), sem somar ao placar nem à contagem de gols.
                        if (ev.Detail?.Contains("Missed", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            context.PenaltisPerdidos.Add(new PenaltiPerdido
                            {
                                JogoId = jogo.Id, JogadorId = jogador.Id,
                                Minuto = minuto, IsTimeCasa = isTimeCasa
                            });
                            break;
                        }

                        var contra = ev.Detail?.Contains("Own", StringComparison.OrdinalIgnoreCase) == true;
                        context.Gols.Add(new Gol
                        {
                            JogoId = jogo.Id, JogadorId = jogador.Id,
                            Minuto = minuto, Contra = contra
                        });

                        if (!contra && ev.Assist?.Id != null)
                        {
                            var assist = await ResolverJogador(context, ev.Assist.Id.Value,
                                ev.Assist.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct, jogo: jogo);
                            if (assist != null)
                                context.Assistencias.Add(new Assistencia
                                {
                                    JogoId = jogo.Id, JogadorId = assist.Id, Minuto = minuto
                                });
                        }
                        break;
                    }

                    case "card":
                    {
                        if (ev.Player.Id == null) break;
                        var jogador = await ResolverJogador(context, ev.Player.Id.Value,
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct, jogo: jogo);
                        if (jogador == null) break;

                        var tipo = ev.Detail?.Contains("Yellow", StringComparison.OrdinalIgnoreCase) == true
                            ? "Amarelo" : "Vermelho";
                        context.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id, JogadorId = jogador.Id,
                            Minuto = minuto, Tipo = tipo
                        });
                        break;
                    }

                    case "subst":
                    {
                        // Convenção da api-football: player = quem SAIU, assist = quem ENTROU
                        if (ev.Player.Id == null) break;
                        var saiu = await ResolverJogador(context, ev.Player.Id.Value,
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct, jogo: jogo);
                        if (saiu == null) break;

                        Jogador? entrou = null;
                        if (ev.Assist?.Id != null)
                            entrou = await ResolverJogador(context, ev.Assist.Id.Value,
                                ev.Assist.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct, jogo: jogo);

                        // Se quem entrou não foi resolvido, fica null — jamais usar o
                        // jogador que saiu como fallback: isso registrava "fulano entrou
                        // no lugar de fulano" e quebrava as setas ↑/↓ da tela Analisar.
                        context.Substituicoes.Add(new Substituicao
                        {
                            JogoId = jogo.Id,
                            JogadorEntrouId = entrou?.Id,
                            JogadorSaiuId = saiu.Id,
                            Minuto = minuto,
                            IsTimeCasa = isTimeCasa
                        });
                        break;
                    }
                }
            }

            // Placar da disputa de pênaltis = cobranças convertidas por lado. Sem isso,
            // Jogo.PenaltisCasa/PenaltisVisitante ficavam sempre null (nada os
            // preenchia), quebrando quem depende deles pra saber quem avançou/venceu
            // nos pênaltis (lista de Jogos, chaveamento da Copa, resumo do placar em
            // Analisar) — mesmo com as cobranças already certinhas em PenaltisDisputa.
            if (penaltisDisputaNovos.Count > 0)
            {
                jogo.PenaltisCasa = penaltisDisputaNovos.Count(p => p.IsTimeCasa && p.Convertido);
                jogo.PenaltisVisitante = penaltisDisputaNovos.Count(p => !p.IsTimeCasa && p.Convertido);
            }
            else
            {
                jogo.PenaltisCasa = null;
                jogo.PenaltisVisitante = null;
            }

            await context.SaveChangesAsync(ct);

            int gols = fx.Events.Count(e => e.Type.Equals("Goal", StringComparison.OrdinalIgnoreCase)
                && e.Detail?.Contains("Missed", StringComparison.OrdinalIgnoreCase) != true
                && e.Comments?.Contains("Shootout", StringComparison.OrdinalIgnoreCase) != true);
            int cartoes = fx.Events.Count(e => e.Type.Equals("Card", StringComparison.OrdinalIgnoreCase));
            int subs = fx.Events.Count(e => e.Type.Equals("subst", StringComparison.OrdinalIgnoreCase));
            int titulares = fx.Lineups.Sum(l => l.StartXI.Count);

            Log(context, cicloId, "Escalação", "Importada",
                jogoDescricao: $"{fx.Teams.Home.Name} × {fx.Teams.Away.Name}",
                detalhes: $"{titulares} titulares | {gols} gols | {cartoes} cartões | {subs} substituições");

            // Estatísticas individuais (para pré-preencher a nota manual)
            await SalvarEstatisticasJogadoresAsync(context, jogo, fx, ct);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task AtualizarTreinadorTime(
            FutebolContext context, Time time, AfCoach? coach, CancellationToken ct)
        {
            if (coach == null || string.IsNullOrWhiteSpace(coach.Name))
                return;

            var treinador = await context.Treinadores
                .FirstOrDefaultAsync(t => t.TimeId == time.Id, ct);

            if (treinador == null)
            {
                context.Treinadores.Add(new Treinador
                {
                    TimeId = time.Id,
                    Nome = coach.Name,
                    FotoUrl = coach.Photo,
                    DtInc = DateTime.UtcNow
                });
            }
            else if (treinador.Nome != coach.Name)
            {
                treinador.Nome = coach.Name;
                if (string.IsNullOrWhiteSpace(treinador.FotoUrl))
                    treinador.FotoUrl = coach.Photo;
                treinador.DtAlt = DateTime.UtcNow;
            }
            else if (string.IsNullOrWhiteSpace(treinador.FotoUrl) && !string.IsNullOrWhiteSpace(coach.Photo))
            {
                treinador.FotoUrl = coach.Photo;
                treinador.DtAlt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);
        }

        private async Task AdicionarEscalacaoJogador(
            FutebolContext context,
            Jogo jogo,
            AfLineupPlayerInfo info,
            Time time,
            bool isTimeCasa,
            bool titular,
            string fase,
            Dictionary<int, Jogador> map,
            PosicaoFormacao? posFormacao,
            CancellationToken ct)
        {
            if (info.Id == null) return;

            var posicao = MapearPosicao(info.Pos);

            var jogador = await ResolverJogador(context, info.Id.Value, info.Name, time, map, ct, info.Number, posicao, jogo);
            if (jogador == null) return;

            context.Escalacoes.Add(new Escalacao
            {
                JogoId        = jogo.Id,
                JogadorId     = jogador.Id,
                IsTimeCasa    = isTimeCasa,
                Titular       = titular,
                Posicao       = posicao,
                FaseEscalacao = fase,
                PosicaoX      = posFormacao?.PosicaoX ?? 0,
                PosicaoY      = posFormacao?.PosicaoY ?? 0
            });
        }

        // Converte "linha:coluna" (ex.: "2:3") em valores numéricos, ignorando formatos inválidos.
        private static (int linha, int coluna)? ParseGrid(string? grid)
        {
            if (string.IsNullOrWhiteSpace(grid)) return null;

            var partes = grid.Split(':');
            if (partes.Length < 2 ||
                !int.TryParse(partes[0], out var linha) ||
                !int.TryParse(partes[1], out var coluna) ||
                linha < 1 || coluna < 1)
                return null;

            return (linha, coluna);
        }

        // Converte "linha:coluna" do grid da api-football para a posição cadastrada em
        // /Formacoes. As posições cadastradas são ordenadas por profundidade (PosicaoY desc)
        // e divididas em "linhas" usando o tamanho real de cada linha do grid devolvido pela
        // API (quantos jogadores ela colocou na linha 1, na linha 2, etc.) — isso é mais
        // confiável do que agrupar por valor de Y arredondado, porque o usuário pode cadastrar
        // jogadores da mesma linha tática com Y levemente diferente (ex.: laterais em Y=75 e
        // zagueiros em Y=80, que ainda são "a mesma linha de defesa"), e também tolera o grid
        // da API numerar as linhas de forma diferente do esperado para a formação nominal.
        private static PosicaoFormacao? ResolverPosicaoFormacao(
            string? grid, List<PosicaoFormacao> posicoes,
            int totalLinhasGrid, Dictionary<int, int> colunasPorLinhaGrid)
        {
            var parsed = ParseGrid(grid);
            if (parsed == null || posicoes.Count == 0) return null;
            var (linha, coluna) = parsed.Value;

            var tamanhosLinha = Enumerable.Range(1, Math.Max(totalLinhasGrid, 1))
                .Select(l => colunasPorLinhaGrid.TryGetValue(l, out var c) ? c : 0)
                .ToList();

            List<List<PosicaoFormacao>> posicoesPorLinha;

            if (tamanhosLinha.Count > 0 && tamanhosLinha.All(t => t > 0) &&
                tamanhosLinha.Sum() == posicoes.Count)
            {
                var ordenadasPorY = posicoes.OrderByDescending(p => p.PosicaoY).ToList();
                posicoesPorLinha = new List<List<PosicaoFormacao>>();
                var idx = 0;
                foreach (var tamanho in tamanhosLinha)
                {
                    posicoesPorLinha.Add(ordenadasPorY.Skip(idx).Take(tamanho)
                        .OrderBy(p => p.PosicaoX).ToList());
                    idx += tamanho;
                }
            }
            else
            {
                // Fallback: agrupamento por Y arredondado, caso os totais não combinem.
                posicoesPorLinha = posicoes
                    .GroupBy(p => Math.Round(p.PosicaoY, 0))
                    .OrderByDescending(g => g.Key)
                    .Select(g => g.OrderBy(p => p.PosicaoX).ToList())
                    .ToList();
            }

            if (posicoesPorLinha.Count == 0) return null;

            var linhaIdx = Math.Clamp(linha - 1, 0, posicoesPorLinha.Count - 1);
            var posicoesLinha = posicoesPorLinha[linhaIdx];
            if (posicoesLinha.Count == 0) return null;

            var colunaIdx = Math.Clamp(coluna - 1, 0, posicoesLinha.Count - 1);
            return posicoesLinha[colunaIdx];
        }

        private async Task<Jogador?> ResolverJogador(
            FutebolContext context,
            int idApi,
            string nome,
            Time time,
            Dictionary<int, Jogador> map,
            CancellationToken ct,
            int? numeroCamisa = null,
            string? posicao = null,
            Jogo? jogo = null)
        {
            if (map.TryGetValue(idApi, out var cached)) return cached;

            // Por IdApi
            var jogador = await context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.IdApi == idApi, ct);

            // Por nome no time
            if (jogador == null && !string.IsNullOrWhiteSpace(nome))
                jogador = await context.Jogadores
                    .Include(j => j.Time)
                    .FirstOrDefaultAsync(j => j.Nome == nome &&
                        (j.TimeId == time.Id || j.SelecaoId == time.Id), ct);

            // Cria se não existe e busca dados completos imediatamente
            if (jogador == null && !string.IsNullOrWhiteSpace(nome))
            {
                jogador = new Jogador
                {
                    Nome         = nome,
                    Posicao      = posicao ?? "",
                    TimeId       = time.Id,
                    SelecaoId    = time.EhSelecao ? time.Id : null,
                    IdApi        = idApi,
                    NumeroCamisa = numeroCamisa,
                    DtInc        = DateTime.UtcNow
                };
                context.Jogadores.Add(jogador);
                await context.SaveChangesAsync(ct);

                // Busca foto, data de nascimento e nacionalidade na API no momento da importação
                try
                {
                    var info = await BuscarInfoJogadorAsync(idApi, ct);
                    if (info != null)
                    {
                        if (!string.IsNullOrEmpty(info.FotoUrl))
                            jogador.FotoUrl = info.FotoUrl;

                        if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                            jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);

                        if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                        {
                            var nac = await ResolverOuCriarNacionalidade(context, info.Nacionalidade, ct);
                            if (nac != null) jogador.NacionalidadeId = nac.Id;
                        }

                        if (!string.IsNullOrWhiteSpace(info.PrimeiroNome))
                            jogador.PrimeiroNome = info.PrimeiroNome;
                        if (!string.IsNullOrWhiteSpace(info.UltimoNome))
                            jogador.UltimoNome = info.UltimoNome;

                        if (info.Altura.HasValue) jogador.Altura = info.Altura;
                        if (info.Peso.HasValue) jogador.Peso = info.Peso;
                    }
                    jogador.Atualizado = true;
                    jogador.DtAlt = DateTime.UtcNow;
                    await context.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ApiFoot] Não foi possível buscar dados extras do jogador {Nome} (IdApi={Id})", nome, idApi);
                }
            }

            if (jogador != null)
            {
                var alterado = false;

                if (jogador.IdApi == 0 && idApi > 0)
                {
                    jogador.IdApi = idApi;
                    alterado = true;
                }

                if (numeroCamisa.HasValue && jogador.NumeroCamisa != numeroCamisa)
                {
                    jogador.NumeroCamisa = numeroCamisa;
                    alterado = true;
                }

                if (!string.IsNullOrWhiteSpace(posicao) && string.IsNullOrWhiteSpace(jogador.Posicao))
                {
                    jogador.Posicao = posicao;
                    alterado = true;
                }

                // Mantém TimeId = clube e SelecaoId = seleção, sem criar jogador duplicado
                // quando o mesmo IdApi aparece em competições de clube e de seleção. Uma
                // seleção nunca entra como origem/destino na Janela de Transferências —
                // só troca real de clube é registrada.
                if (jogador.TimeId != time.Id && jogador.SelecaoId != time.Id)
                {
                    // TimeId atual é a seleção quando: o time carregado está marcado como tal,
                    // ou (defesa extra, caso a competição da seleção não tenha o flag EhSelecao
                    // marcado) quando o SelecaoId já registrado é o próprio TimeId atual — sinal
                    // de que esse "clube" é só o placeholder criado antes de o clube ser conhecido.
                    var origemEhSelecao = (jogador.Time != null && jogador.Time.EhSelecao)
                        || (jogador.SelecaoId != null && jogador.TimeId == jogador.SelecaoId);

                    if (time.EhSelecao)
                    {
                        jogador.SelecaoId = time.Id;
                        alterado = true;
                    }
                    else if (origemEhSelecao)
                    {
                        // O TimeId atual na verdade é a seleção (criado antes do clube ser conhecido)
                        jogador.SelecaoId = jogador.TimeId;
                        jogador.TimeId = time.Id;
                        alterado = true;
                    }
                    else
                    {
                        // Transferência entre clubes: troca o clube e registra no
                        // histórico da Janela de Transferências.
                        context.Transferencias.Add(new Transferencia
                        {
                            JogadorId = jogador.Id,
                            TimeOrigemId = jogador.TimeId,
                            TimeDestinoId = time.Id,
                            JogoId = jogo?.Id,
                            Data = jogo?.Data ?? DateTime.UtcNow
                        });
                        _logger.LogInformation(
                            "[ApiFoot] Transferência detectada: {Jogador} — {Origem} → {Destino}",
                            jogador.Nome, jogador.Time?.Nome ?? jogador.TimeId.ToString(), time.Nome);

                        jogador.TimeId = time.Id;
                        alterado = true;
                    }
                }

                if (alterado)
                    await context.SaveChangesAsync(ct);

                map[idApi] = jogador;
            }

            return jogador;
        }

        private static async Task<Nacionalidade?> ResolverOuCriarNacionalidade(
            FutebolContext context, string nomeRaw, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeRaw)) return null;
            // A api-football devolve o país em inglês; traduz antes de buscar/criar
            // para não duplicar a nacionalidade ("Brazil" vs "Brasil") — o filtro por
            // nacionalidade em /Jogadores depende de um nome único por país.
            var nome = CountryHelper.Traduzir(nomeRaw.Trim());
            var nac = await context.Nacionalidades
                .FirstOrDefaultAsync(n => n.Nome.ToLower() == nome.ToLower(), ct);
            if (nac == null)
            {
                nac = new Nacionalidade { Nome = nome };
                context.Nacionalidades.Add(nac);
                await context.SaveChangesAsync(ct);
            }
            return nac;
        }

        private static string TraduzirNomeTIme(string nome)
        {
            // Remove duplicatas como "South Africa South Africa"
            var partes = nome.Trim().Split(' ');
            var metade = partes.Length / 2;
            if (partes.Length >= 2 && partes.Length % 2 == 0 &&
                string.Join(" ", partes[..metade]) == string.Join(" ", partes[metade..]))
                nome = string.Join(" ", partes[..metade]);

            return CountryHelper.Traduzir(nome);
        }

        private async Task<(Time time, bool criado)> ResolverOuCriarTime(
            FutebolContext context,
            string nomeOriginal,
            int idApi,
            string? escudo,
            Guid cicloId,
            CancellationToken ct,
            bool ehSelecao = false)
        {
            var nome = TraduzirNomeTIme(nomeOriginal);

            // O fallback por nome só pode casar com times ainda sem IdApi (cadastrados
            // manualmente, sem vínculo com a API) — nunca com um time já vinculado a OUTRO
            // IdApi, senão dois clubes reais de países diferentes com o mesmo nome (ex.:
            // "Athletic Club" na Espanha e no Brasil) acabam sendo tratados como o mesmo time.
            var time = await context.Times.FirstOrDefaultAsync(t => t.IdApi == idApi, ct)
                    ?? await context.Times.FirstOrDefaultAsync(t => t.IdApi == 0 && t.Nome == nome, ct)
                    ?? (nomeOriginal != nome
                        ? await context.Times.FirstOrDefaultAsync(t => t.IdApi == 0 && t.Nome == nomeOriginal, ct)
                        : null);

            if (time != null)
            {
                if (time.IdApi == 0 && idApi > 0) time.IdApi = idApi;
                if (string.IsNullOrWhiteSpace(time.EscudoUrl) && !string.IsNullOrWhiteSpace(escudo))
                    time.EscudoUrl = escudo;
                // Atualiza nome traduzido se ainda estiver em inglês
                if (nomeOriginal != nome && time.Nome == nomeOriginal)
                    time.Nome = nome;
                if (ehSelecao && !time.EhSelecao)
                    time.EhSelecao = true;
                await context.SaveChangesAsync(ct);
                return (time, false);
            }

            var formacaoPadrao = await context.Formacoes.FirstOrDefaultAsync(ct)
                ?? new Formacao { Nome = "4-3-3" };

            var novoTime = new Time
            {
                Nome             = nome,
                IdApi            = idApi,
                EscudoUrl        = escudo ?? "",
                Cidade           = "Importado",
                CorPrincipal     = "#000000",
                CorSecundaria    = "#FFFFFF",
                FormacaoPadraoId = formacaoPadrao.Id,
                EhSelecao        = ehSelecao
            };

            context.Times.Add(novoTime);
            await context.SaveChangesAsync(ct);

            Log(context, cicloId, "Time", "Criado",
                timeNome: nome,
                detalhes: $"IdApi={idApi} | Escudo={escudo ?? "sem escudo"} | NomeOriginal={nomeOriginal}");

            return (novoTime, true);
        }

        private async Task<Formacao> ObterOuCriarFormacao(
            FutebolContext context, string? nome, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var existente = await context.Formacoes
                    .FirstOrDefaultAsync(f => f.Nome == nome, ct);
                if (existente != null) return existente;

                var nova = new Formacao { Nome = nome };
                context.Formacoes.Add(nova);
                await context.SaveChangesAsync(ct);
                return nova;
            }

            return await context.Formacoes.FirstOrDefaultAsync(ct)
                ?? new Formacao { Nome = "4-3-3" };
        }

        private void AdicionarEscalacaoVazia(FutebolContext context,
            Jogo jogo, Formacao formCasa, Formacao formVis)
        {
            foreach (var (isTimeCasa, form) in new[] { (true, formCasa), (false, formVis) })
                for (int i = 0; i < 11; i++)
                    context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = jogo.Id,
                        IsTimeCasa = isTimeCasa,
                        Titular = true,
                        FaseEscalacao = "INICIAL"
                    });
        }

        /// <summary>
        /// Salva as estatísticas individuais de cada jogador (fx.Players) para a partida.
        /// Não gera nota nenhuma — a nota continua sendo dada manualmente pelo analista;
        /// essas estatísticas só servem para pré-preencher o formulário de avaliação com
        /// os números reais (finalizações, passes, desarmes, etc.) em vez de partir do zero.
        /// </summary>
        private async Task SalvarEstatisticasJogadoresAsync(
            FutebolContext context, Jogo jogo, AfFixture fx, CancellationToken ct)
        {
            try
            {
                // Limpa notas automáticas de versões anteriores do critério (descontinuado)
                var notasAutomaticasAntigas = await context.Notas
                    .Include(n => n.Detalhes)
                    .Where(n => n.JogoId == jogo.Id && n.IsAutomatica)
                    .ToListAsync(ct);
                foreach (var n in notasAutomaticasAntigas)
                {
                    context.NotaDetalhes.RemoveRange(n.Detalhes);
                    context.Notas.Remove(n);
                }

                var antigas = await context.EstatisticasJogador
                    .Where(e => e.JogoId == jogo.Id)
                    .ToListAsync(ct);
                context.EstatisticasJogador.RemoveRange(antigas);
                await context.SaveChangesAsync(ct);

                var todosJogadoresApi = fx.Players.SelectMany(t => t.Players).ToList();
                if (todosJogadoresApi.Count == 0) return;

                var idsApi = todosJogadoresApi.Select(p => (long)p.Player.Id).ToList();
                var jogadoresLocais = await context.Jogadores
                    .Where(j => j.IdApi != null && idsApi.Contains(j.IdApi.Value))
                    .ToListAsync(ct);

                foreach (var p in todosJogadoresApi)
                {
                    var jogador = jogadoresLocais.FirstOrDefault(j => j.IdApi == p.Player.Id);
                    if (jogador == null) continue;

                    var s = p.Statistics.FirstOrDefault();
                    if (s == null) continue;

                    double.TryParse(s.Games?.Rating, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var rating);

                    context.EstatisticasJogador.Add(new EstatisticaJogador
                    {
                        JogoId            = jogo.Id,
                        JogadorId         = jogador.Id,
                        Minutos           = s.Games?.Minutes,
                        Rating            = rating > 0 ? rating : null,
                        Offsides          = s.Offsides ?? 0,
                        FinalizacoesTotal = s.Shots?.Total ?? 0,
                        FinalizacoesNoGol = s.Shots?.On ?? 0,
                        Gols              = s.Goals?.Total ?? 0,
                        GolsSofridos      = s.Goals?.Conceded ?? 0,
                        Assistencias      = s.Goals?.Assists ?? 0,
                        Defesas           = s.Goals?.Saves ?? 0,
                        PassesTotal       = s.Passes?.Total ?? 0,
                        PassesChave       = s.Passes?.Key ?? 0,
                        Desarmes          = s.Tackles?.Total ?? 0,
                        Bloqueios         = s.Tackles?.Blocks ?? 0,
                        Interceptacoes    = s.Tackles?.Interceptions ?? 0,
                        DuelosTotal       = s.Duels?.Total ?? 0,
                        DuelosVencidos    = s.Duels?.Won ?? 0,
                        DriblesTentados   = s.Dribbles?.Attempts ?? 0,
                        DriblesCertos     = s.Dribbles?.Success ?? 0,
                        DriblesSofridos   = s.Dribbles?.Past ?? 0,
                        FaltasSofridas    = s.Fouls?.Drawn ?? 0,
                        FaltasCometidas   = s.Fouls?.Committed ?? 0,
                        CartoesAmarelos   = s.Cards?.Yellow ?? 0,
                        CartoesVermelhos  = s.Cards?.Red ?? 0,
                        PenaltiSofrido    = s.Penalty?.Won ?? 0,
                        PenaltiCometido   = s.Penalty?.Commited ?? 0,
                        PenaltiPerdido    = s.Penalty?.Missed ?? 0,
                        PenaltiDefendido  = s.Penalty?.Saved ?? 0
                    });
                }

                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ApiFoot] Falha ao salvar estatísticas dos jogadores do jogo {Id}", jogo.Id);
            }
        }

        // ── Utilitários ───────────────────────────────────────────────────────

        private static int ParseRodada(string round)
        {
            var m = Regex.Match(round, @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        private static string MapearPosicao(string? pos) => pos?.ToUpperInvariant() switch
        {
            "G" or "GOALKEEPER"  => "Goleiro",
            "D" or "DEFENDER"    => "Defensor",
            "M" or "MIDFIELDER"  => "Meia",
            "F" or "ATTACKER"    => "Atacante",
            _    => pos ?? ""
        };

        private static void Log(
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
                CicloId        = cicloId,
                Data           = DateTime.UtcNow,
                Tipo           = tipo,
                Acao           = acao,
                CompeticaoNome = competicaoNome,
                TimeNome       = timeNome,
                JogoDescricao  = jogoDescricao,
                Detalhes       = detalhes
            });
        }
    }
}
