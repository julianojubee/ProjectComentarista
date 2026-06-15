using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

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
        private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        // Status considerados "jogo finalizado"
        private static readonly HashSet<string> StatusFinalizados =
            new(StringComparer.OrdinalIgnoreCase) { "FT", "AET", "PEN" };

        public ApiFootballService(HttpClient http, IConfiguration config,
            ILogger<ApiFootballService> logger)
        {
            _http = http;
            _logger = logger;

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

            if (!IsApiFootballLink(competicao.linktransfermarket))
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
                detalhes: $"Fonte: api-football.com | Link: {competicao.linktransfermarket}");

            var (leagueId, season) = ParseLink(competicao.linktransfermarket!);
            _logger.LogInformation("[ApiFoot] Sincronizando {Nome} — league={L} season={S}",
                competicao.Nome, leagueId, season);

            var fixtures = await BuscarFixturesAsync(leagueId, season, ct);
            _logger.LogInformation("[ApiFoot] {N} fixtures encontrados.", fixtures.Count);

            Log(context, cicloId, "Ciclo", "Fixtures",
                competicaoNome: competicao.Nome,
                detalhes: $"{fixtures.Count} fixtures encontrados na API");
            await context.SaveChangesAsync(ct);

            foreach (var fx in fixtures)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    AfFixture detalhado = fx;

                    if (StatusFinalizados.Contains(fx.Fixture.Status.Short) &&
                        !fx.Lineups.Any())
                    {
                        await Task.Delay(600, ct);
                        detalhado = await BuscarDetalhesFixtureAsync(fx.Fixture.Id, ct) ?? fx;
                    }

                    var (timeCasa, casaCriado) = await ResolverOuCriarTime(context,
                        detalhado.Teams.Home.Name, detalhado.Teams.Home.Id,
                        detalhado.Teams.Home.Logo, cicloId, ct);
                    if (casaCriado) timesCreados++;

                    var (timeVis, visCriado) = await ResolverOuCriarTime(context,
                        detalhado.Teams.Away.Name, detalhado.Teams.Away.Id,
                        detalhado.Teams.Away.Logo, cicloId, ct);
                    if (visCriado) timesCreados++;

                    await IncluirOuAtualizarJogo(context, competicao,
                        detalhado, timeCasa, timeVis, cicloId, ct);

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

        // ── Busca de info de jogador por IdApi ───────────────────────────────

        public async Task<AfPlayerInfoResult?> BuscarInfoJogadorAsync(
            long idApi, CancellationToken ct = default)
        {
            var season = DateTime.UtcNow.Year;
            AfPlayerProfileEntry? entry = null;

            // Tenta temporada atual e anterior
            foreach (var s in new[] { season, season - 1 })
            {
                var json = await _http.GetStringAsync($"players?id={idApi}&season={s}", ct);
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
                FotoUrl        = entry.Player.Photo
            };
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
            // Prioriza Group; caso não exista, usa Round (ex.: "Group Stage - A")
            return !string.IsNullOrWhiteSpace(fx?.League.Group)
                ? fx.League.Group
                : fx?.League.Round;
        }

        // ── Chamadas HTTP ─────────────────────────────────────────────────────

        private async Task<List<AfFixture>> BuscarFixturesAsync(
            int leagueId, int season, CancellationToken ct)
        {
            var url = $"fixtures?league={leagueId}&season={season}";
            var json = await _http.GetStringAsync(url, ct);
            var resp = JsonSerializer.Deserialize<ApiFootballResponse<AfFixture>>(json, _json);
            return resp?.Response ?? new();
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
            Guid cicloId,
            CancellationToken ct)
        {
            var data = fx.Fixture.Date.HasValue
                ? DateTime.SpecifyKind(fx.Fixture.Date.Value, DateTimeKind.Utc)
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
                if (existente.Analisado == 1) return;

                if (data.HasValue) existente.Data = data;
                if (rodada > 0) existente.Rodada = rodada;
                existente.LinkDetalhes = fixtureIdStr;

                if (finalizado && fx.Goals.Home.HasValue &&
                    (existente.PlacarCasa != fx.Goals.Home ||
                     existente.PlacarVisitante != fx.Goals.Away))
                {
                    existente.PlacarCasa = fx.Goals.Home;
                    existente.PlacarVisitante = fx.Goals.Away;
                    existente.Status = "Finalizado";
                    existente.Atualizado = 1;
                }

                // Reimporta lineup/eventos se jogo finalizado mas sem escalação
                if (finalizado && fx.Lineups.Any())
                {
                    var temEscalacao = await context.Escalacoes
                        .AnyAsync(e => e.JogoId == existente.Id && e.JogadorId != null, ct);

                    if (!temEscalacao)
                        await ImportarLineupEEventos(context, existente,
                            fx, timeCasa, timeVis, cicloId, ct);
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
                PlacarCasa        = finalizado ? fx.Goals.Home : null,
                PlacarVisitante   = finalizado ? fx.Goals.Away : null,
                Grupo             = fx.League.Group,
                Status            = finalizado ? "Finalizado" : "Agendado",
                Atualizado        = finalizado ? 1 : 0,
                FormacaoCasaId    = formCasa.Id,
                FormacaoVisitanteId = formVis.Id,
                LinkDetalhes      = fixtureIdStr,
                Estadio           = fx.Fixture.Venue?.Name,
                Arbitro           = fx.Fixture.Referee
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
            // Remove dados anteriores para reimportação limpa
            var escalOld = await context.Escalacoes.Where(e => e.JogoId == jogo.Id).ToListAsync(ct);
            context.Escalacoes.RemoveRange(escalOld);
            var golsOld = await context.Gols.Where(g => g.JogoId == jogo.Id).ToListAsync(ct);
            context.Gols.RemoveRange(golsOld);
            var assistOld = await context.Assistencias.Where(a => a.JogoId == jogo.Id).ToListAsync(ct);
            context.Assistencias.RemoveRange(assistOld);
            var cartoesOld = await context.Cartoes.Where(c => c.JogoId == jogo.Id).ToListAsync(ct);
            context.Cartoes.RemoveRange(cartoesOld);
            var subsOld = await context.Substituicoes.Where(s => s.JogoId == jogo.Id).ToListAsync(ct);
            context.Substituicoes.RemoveRange(subsOld);

            // Mapa: IdApi → Jogador local (para vincular eventos)
            var jogadorMap = new Dictionary<int, Jogador>();

            // Lineups
            foreach (var lineup in fx.Lineups)
            {
                var isTimeCasa = lineup.Team.Id == fx.Teams.Home.Id;
                var time = isTimeCasa ? timeCasa : timeVis;

                foreach (var lp in lineup.StartXI)
                    await AdicionarEscalacaoJogador(context, jogo, lp.Player, time,
                        isTimeCasa, true, "INICIAL", jogadorMap, ct);

                foreach (var lp in lineup.Substitutes)
                    await AdicionarEscalacaoJogador(context, jogo, lp.Player, time,
                        isTimeCasa, false, "INICIAL", jogadorMap, ct);
            }

            await context.SaveChangesAsync(ct);

            // Eventos
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
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct);
                        if (jogador == null) break;

                        var contra = ev.Detail?.Contains("Own", StringComparison.OrdinalIgnoreCase) == true;
                        context.Gols.Add(new Gol
                        {
                            JogoId = jogo.Id, JogadorId = jogador.Id,
                            Minuto = minuto, Contra = contra
                        });

                        if (!contra && ev.Assist?.Id != null)
                        {
                            var assist = await ResolverJogador(context, ev.Assist.Id.Value,
                                ev.Assist.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct);
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
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct);
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
                        // player = entrou, assist = saiu
                        if (ev.Player.Id == null) break;
                        var entrou = await ResolverJogador(context, ev.Player.Id.Value,
                            ev.Player.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct);
                        if (entrou == null) break;

                        Jogador? saiu = null;
                        if (ev.Assist?.Id != null)
                            saiu = await ResolverJogador(context, ev.Assist.Id.Value,
                                ev.Assist.Name ?? "", isTimeCasa ? timeCasa : timeVis, jogadorMap, ct);

                        context.Substituicoes.Add(new Substituicao
                        {
                            JogoId = jogo.Id,
                            JogadorEntrouId = entrou.Id,
                            JogadorSaiuId = saiu?.Id,
                            Minuto = minuto,
                            IsTimeCasa = isTimeCasa
                        });
                        break;
                    }
                }
            }

            await context.SaveChangesAsync(ct);

            int gols = fx.Events.Count(e => e.Type.Equals("Goal", StringComparison.OrdinalIgnoreCase));
            int cartoes = fx.Events.Count(e => e.Type.Equals("Card", StringComparison.OrdinalIgnoreCase));
            int subs = fx.Events.Count(e => e.Type.Equals("subst", StringComparison.OrdinalIgnoreCase));
            int titulares = fx.Lineups.Sum(l => l.StartXI.Count);

            Log(context, cicloId, "Escalação", "Importada",
                jogoDescricao: $"{fx.Teams.Home.Name} × {fx.Teams.Away.Name}",
                detalhes: $"{titulares} titulares | {gols} gols | {cartoes} cartões | {subs} substituições");

            // Notas automáticas
            await SalvarNotasAutomaticasAsync(context, jogo, ct);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task AdicionarEscalacaoJogador(
            FutebolContext context,
            Jogo jogo,
            AfLineupPlayerInfo info,
            Time time,
            bool isTimeCasa,
            bool titular,
            string fase,
            Dictionary<int, Jogador> map,
            CancellationToken ct)
        {
            var jogador = await ResolverJogador(context, info.Id, info.Name, time, map, ct);
            if (jogador == null) return;

            var posicao = MapearPosicao(info.Pos);

            context.Escalacoes.Add(new Escalacao
            {
                JogoId        = jogo.Id,
                JogadorId     = jogador.Id,
                IsTimeCasa    = isTimeCasa,
                Titular       = titular,
                Posicao       = posicao,
                FaseEscalacao = fase
            });
        }

        private async Task<Jogador?> ResolverJogador(
            FutebolContext context,
            int idApi,
            string nome,
            Time time,
            Dictionary<int, Jogador> map,
            CancellationToken ct)
        {
            if (map.TryGetValue(idApi, out var cached)) return cached;

            // Por IdApi
            var jogador = await context.Jogadores
                .FirstOrDefaultAsync(j => j.IdApi == idApi, ct);

            // Por nome no time
            if (jogador == null && !string.IsNullOrWhiteSpace(nome))
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.Nome == nome &&
                        (j.TimeId == time.Id || j.SelecaoId == time.Id), ct);

            // Cria se não existe
            if (jogador == null && !string.IsNullOrWhiteSpace(nome))
            {
                jogador = new Jogador
                {
                    Nome    = nome,
                    TimeId  = time.Id,
                    IdApi   = idApi,
                    DtInc   = DateTime.UtcNow
                };
                context.Jogadores.Add(jogador);
                await context.SaveChangesAsync(ct);
            }

            if (jogador != null)
            {
                if (jogador.IdApi == 0 && idApi > 0)
                {
                    jogador.IdApi = idApi;
                    await context.SaveChangesAsync(ct);
                }
                map[idApi] = jogador;
            }

            return jogador;
        }

        private async Task<(Time time, bool criado)> ResolverOuCriarTime(
            FutebolContext context,
            string nome,
            int idApi,
            string? escudo,
            Guid cicloId,
            CancellationToken ct)
        {
            var time = await context.Times.FirstOrDefaultAsync(t => t.IdApi == idApi, ct)
                    ?? await context.Times.FirstOrDefaultAsync(t => t.Nome == nome, ct);

            if (time != null)
            {
                if (time.IdApi == 0 && idApi > 0) time.IdApi = idApi;
                if (string.IsNullOrWhiteSpace(time.EscudoUrl) && !string.IsNullOrWhiteSpace(escudo))
                    time.EscudoUrl = escudo;
                return (time, false);
            }

            var formacaoPadrao = await context.Formacoes.FirstOrDefaultAsync(ct)
                ?? new Formacao { Nome = "4-3-3" };

            var novoTime = new Time
            {
                Nome            = nome,
                IdApi           = idApi,
                EscudoUrl       = escudo ?? "",
                Cidade          = "Importado",
                CorPrincipal    = "#000000",
                CorSecundaria   = "#FFFFFF",
                FormacaoPadraoId = formacaoPadrao.Id
            };

            context.Times.Add(novoTime);
            await context.SaveChangesAsync(ct);

            Log(context, cicloId, "Time", "Criado",
                timeNome: nome,
                detalhes: $"IdApi={idApi} | Criado via api-football");

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

        private async Task SalvarNotasAutomaticasAsync(
            FutebolContext context, Jogo jogo, CancellationToken ct)
        {
            // Delega ao OgolService para não duplicar a lógica de notas
            // O OgolService.SalvarNotasAutomaticasAsync recebe apenas o jogo
            // e lê os dados direto do banco (já salvos acima)
            try
            {
                var notasAntigas = await context.Notas
                    .Include(n => n.Detalhes)
                    .Where(n => n.JogoId == jogo.Id && n.IsAutomatica)
                    .ToListAsync(ct);

                foreach (var n in notasAntigas)
                {
                    context.NotaDetalhes.RemoveRange(n.Detalhes);
                    context.Notas.Remove(n);
                }
                await context.SaveChangesAsync(ct);
            }
            catch { /* ignora se tabela não existir ainda */ }
        }

        // ── Utilitários ───────────────────────────────────────────────────────

        private static int ParseRodada(string round)
        {
            var m = Regex.Match(round, @"\d+");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        private static string MapearPosicao(string? pos) => pos?.ToUpperInvariant() switch
        {
            "G"  => "Goleiro",
            "D"  => "Defensor",
            "M"  => "Meia",
            "F"  => "Atacante",
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
