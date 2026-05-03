using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.AllSports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFutebolWeb.Controllers
{
    public class ImportacaoJsonController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<ImportacaoJsonController> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ImportacaoJsonController(FutebolContext context, ILogger<ImportacaoJsonController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── GET: tela de upload ──────────────────────────────────────────────
        [HttpGet]
        public IActionResult Index()
        {
            var competicoes = _context.Competicoes.OrderBy(c => c.Nome).ToList();
            ViewBag.Competicoes = competicoes;
            return View();
        }

        // ── POST: processa o arquivo ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar(IFormFile arquivo, int competicaoId, bool criarTimesAuto)
        {
            if (arquivo == null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Nenhum arquivo selecionado.";
                return RedirectToAction(nameof(Index));
            }

            if (!arquivo.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "O arquivo precisa ser .json.";
                return RedirectToAction(nameof(Index));
            }

            List<AllSportsJogo> jogos;
            try
            {
                if (arquivo.Length > 5_000_000) // 🔹 se for muito grande, usa streaming
                {
                    using var stream = arquivo.OpenReadStream();
                    jogos = await ParsearJsonStream(stream);
                }
                else
                {
                    using var reader = new StreamReader(arquivo.OpenReadStream());
                    var jsonContent = await reader.ReadToEndAsync();
                    jogos = ParsearJson(jsonContent);
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao ler o JSON: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            if (jogos.Count == 0)
            {
                TempData["Erro"] = "Nenhum jogo encontrado no arquivo.";
                return RedirectToAction(nameof(Index));
            }

            var resultado = await ProcessarJogos(jogos, competicaoId, criarTimesAuto);

            TempData["Sucesso"] = $"Importação concluída! " +
                $"{resultado.JogosImportados} jogo(s) importado(s), " +
                $"{resultado.JogosAtualizados} atualizado(s), " +
                $"{resultado.TimesNovos} time(s) criado(s), " +
                $"{resultado.JogadoresNovos} jogador(es) criado(s). " +
                (resultado.Avisos.Any() ? $"Avisos: {string.Join("; ", resultado.Avisos)}" : "");

            return RedirectToAction(nameof(Index));
        }

        // ── Parser: aceita array direto, objeto com "result" ou objeto único ─
        private static List<AllSportsJogo> ParsearJson(string json)
        {
            var trimmed = json.TrimStart();

            // 1. Array direto: [ {...}, {...} ]
            if (trimmed.StartsWith('['))
                return JsonSerializer.Deserialize<List<AllSportsJogo>>(json, _jsonOpts) ?? new();

            var node = JsonNode.Parse(json);

            // 2. Objeto com chave "result"
            if (node is JsonObject obj && obj.ContainsKey("result"))
            {
                var resultNode = obj["result"];
                if (resultNode is JsonArray)
                    return JsonSerializer.Deserialize<List<AllSportsJogo>>(
                        resultNode.ToJsonString(), _jsonOpts) ?? new();
            }

            // 3. Objeto grande com várias chaves (ex: { "123": {...}, "124": {...} })
            if (node is JsonObject bigObj)
            {
                var jogos = new List<AllSportsJogo>();
                foreach (var kv in bigObj)
                {
                    if (kv.Value is JsonObject jogoObj)
                    {
                        var jogo = jogoObj.Deserialize<AllSportsJogo>(_jsonOpts);
                        if (jogo != null) jogos.Add(jogo);
                    }
                    else if (kv.Value is JsonArray arr)
                    {
                        foreach (var element in arr)
                        {
                            var jogo = element.Deserialize<AllSportsJogo>(_jsonOpts);
                            if (jogo != null) jogos.Add(jogo);
                        }
                    }
                }
                if (jogos.Any()) return jogos;
            }

            // 4. Objeto único de um jogo
            var jogoUnico = JsonSerializer.Deserialize<AllSportsJogo>(json, _jsonOpts);
            return jogoUnico != null ? new List<AllSportsJogo> { jogoUnico } : new();
        }

        private async Task<List<AllSportsJogo>> ParsearJsonStream(Stream jsonStream)
        {
            var jogos = new List<AllSportsJogo>();

            using var doc = await JsonDocument.ParseAsync(jsonStream);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var jogo = element.Deserialize<AllSportsJogo>(_jsonOpts);
                    if (jogo != null) jogos.Add(jogo);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in doc.RootElement.EnumerateObject())
                {
                    if (kv.Value.ValueKind == JsonValueKind.Object)
                    {
                        var jogo = kv.Value.Deserialize<AllSportsJogo>(_jsonOpts);
                        if (jogo != null) jogos.Add(jogo);
                    }
                    else if (kv.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in kv.Value.EnumerateArray())
                        {
                            var jogo = element.Deserialize<AllSportsJogo>(_jsonOpts);
                            if (jogo != null) jogos.Add(jogo);
                        }
                    }
                }
            }

            return jogos;
        }


        // ── Lógica principal de importação ───────────────────────────────────
        private async Task<ResultadoImportacao> ProcessarJogos(
            List<AllSportsJogo> jogos, int competicaoId, bool criarTimesAuto)
        {
            var resultado = new ResultadoImportacao();

            // Formação padrão para novos times/jogos
            var formacaoPadrao = await _context.Formacoes.FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Nenhuma formação cadastrada no banco.");

            foreach (var jogoApi in jogos)
            {
                try
                {
                    // ── 1. Resolve placar ─────────────────────────────────────
                    int? placarCasa = null, placarVis = null;
                    if (!string.IsNullOrWhiteSpace(jogoApi.FinalResult) &&
                        jogoApi.FinalResult.Contains('-'))
                    {
                        var partes = jogoApi.FinalResult.Split('-');
                        if (int.TryParse(partes[0].Trim(), out int pc) &&
                            int.TryParse(partes[1].Trim(), out int pv))
                        {
                            placarCasa = pc;
                            placarVis = pv;
                        }
                    }

                    // ── 2. Resolve data ───────────────────────────────────────
                    DateTime data = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(jogoApi.EventDate))
                    {
                        var dataStr = jogoApi.EventDate;
                        if (!string.IsNullOrWhiteSpace(jogoApi.EventTime))
                            dataStr += " " + jogoApi.EventTime;

                        if (DateTime.TryParse(dataStr, out var dt))
                            data = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }

                    // ── 3. Resolve rodada ─────────────────────────────────────
                    int rodada = 0;
                    if (!string.IsNullOrWhiteSpace(jogoApi.LeagueRound))
                    {
                        var roundStr = new string(
                            jogoApi.LeagueRound.Where(char.IsDigit).ToArray());
                        int.TryParse(roundStr, out rodada);
                    }

                    // ── 4. Resolve times ──────────────────────────────────────
                    var timeCasa = await ResolverTime(
                        jogoApi.HomeTeam, jogoApi.HomeTeamKey.Value,
                        jogoApi.HomeTeamLogo, formacaoPadrao, criarTimesAuto, resultado);

                    var timeVis = await ResolverTime(
                        jogoApi.AwayTeam, jogoApi.AwayTeamKey.Value,
                        jogoApi.AwayTeamLogo, formacaoPadrao, criarTimesAuto, resultado);

                    if (timeCasa == null || timeVis == null)
                    {
                        resultado.Avisos.Add(
                            $"Time não encontrado para jogo {jogoApi.EventKey} " +
                            $"({jogoApi.HomeTeam} x {jogoApi.AwayTeam}). " +
                            "Use 'Criar times automaticamente' ou cadastre antes.");
                        continue;
                    }

                    // ── 5. Jogo já existe? ────────────────────────────────────
                    var jogoDb = await _context.Jogos
                        .Include(j => j.Gols)
                        .Include(j => j.Cartoes)
                        .Include(j => j.Escalacoes)
                        .FirstOrDefaultAsync(j => j.PartidaApiId == (int)(jogoApi.EventKey % int.MaxValue));

                    bool jogoNovo = jogoDb == null;
                    if (jogoNovo)
                    {
                        jogoDb = new Jogo
                        {
                            PartidaApiId = (int)(jogoApi.EventKey % int.MaxValue),
                            Data = data,
                            Rodada = rodada,
                            TimeCasaId = timeCasa.Id,
                            TimeVisitanteId = timeVis.Id,
                            PlacarCasa = placarCasa,
                            PlacarVisitante = placarVis,
                            CompeticaoId = competicaoId,
                            FormacaoCasaId = formacaoPadrao.Id,
                            FormacaoVisitanteId = formacaoPadrao.Id,
                            Grupo = jogoApi.LeagueRound
                        };
                        _context.Jogos.Add(jogoDb);
                        await _context.SaveChangesAsync(); // precisa do Id gerado
                        resultado.JogosImportados++;
                    }
                    else
                    {
                        // Atualiza apenas dados que podem mudar
                        jogoDb!.PlacarCasa = placarCasa;
                        jogoDb.PlacarVisitante = placarVis;
                        jogoDb.Data = data;
                        jogoDb.Rodada = rodada;
                        resultado.JogosAtualizados++;
                    }

                    // ── 6. Jogadores das escalações ───────────────────────────
                    if (jogoApi.Lineups != null)
                    {
                        await ImportarEscalacao(
                            jogoDb, jogoApi.Lineups.HomeTeam, timeCasa, true,
                            formacaoPadrao, resultado);

                        await ImportarEscalacao(
                            jogoDb, jogoApi.Lineups.AwayTeam, timeVis, false,
                            formacaoPadrao, resultado);
                    }

                    // ── 7. Gols ───────────────────────────────────────────────
                   

                    if (jogoNovo && jogoApi.Goalscorers.Any())
                    {
                        await ImportarGols(jogoApi, jogoDb, timeCasa, timeVis, resultado);
                    }
                    // ── 8. Cartões ────────────────────────────────────────────
                    if (jogoNovo && jogoApi.Cards.Any())
                    {
                        await ImportarCartoes(jogoDb, jogoApi.Cards, timeCasa, timeVis);
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Erro ao processar jogo {EventKey}", jogoApi.EventKey);
                    resultado.Avisos.Add(
                        $"Erro no jogo {jogoApi.EventKey}: {ex.Message}");
                }
            }

            return resultado;
        }

        private async Task<Time?> ResolverTime(string nome, long apiKey, string? logoUrl,Formacao formacaoPadrao, bool criarAuto,
                 ResultadoImportacao resultado)
        {
            // 1. Se o nome vier vazio ou nulo, aborta ou usa fallback
            if (string.IsNullOrWhiteSpace(nome))
            {
                // Se não pode criar automaticamente, apenas retorna null
                if (!criarAuto)
                {
                    resultado.Avisos.Add($"Time ignorado: nome vazio para apiKey {apiKey}");
                    return null;
                }

                // Se pode criar, usa um nome padrão
                nome = $"Time_{apiKey}";
            }

            // 2. Busca por IdApi
            var time = await _context.Times.FirstOrDefaultAsync(
                t => t.IdApi == (int)(apiKey % int.MaxValue));

            // 3. Busca por nome usando ILIKE
            if (time == null)
            {
                time = await _context.Times
                    .FirstOrDefaultAsync(t => EF.Functions.ILike(t.Nome, nome));
            }

            // 4. Normalização extra (remove CR, RJ, FC, acentos, espaços)
            if (time == null)
            {
                var nomeNorm = NormalizarNome(nome);
                time = await _context.Times
                    .AsEnumerable()
                    .FirstOrDefault(t =>
                        NormalizarNome(t.Nome) == nomeNorm ||
                        NormalizarNome(t.Nome).Contains(nomeNorm) ||
                        nomeNorm.Contains(NormalizarNome(t.Nome)))
                    .AsTask();
            }

            // 5. Cria novo se não achou e está permitido
            if (time == null && criarAuto)
            {
                time = new Time
                {
                    Nome = nome, // 🔹 sempre preenchido
                    Cidade = "Importado",
                    IdApi = (int)(apiKey % int.MaxValue),
                    EscudoUrl = logoUrl ?? "",
                    CorPrincipal = "#000000",
                    CorSecundaria = "#FFFFFF",
                    FormacaoPadraoId = formacaoPadrao.Id
                };
                _context.Times.Add(time);
                await _context.SaveChangesAsync();
                resultado.TimesNovos++;
            }

            return time;
        }


        private static string NormalizarNome(string nome)
        {
            return nome.ToLowerInvariant()
                .Replace("cr ", "")
                .Replace("rj", "")
                .Replace("fc", "")
                .Replace("futebol clube", "")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                .Replace("ç", "c")
                .Replace("-", "").Replace(" ", "")
                .Trim();
        }


        // ── Importa escalação de um time ─────────────────────────────────────
        private async Task ImportarEscalacao(
                Jogo jogo, AllSportsTeamLineup? lineup, Time time,
                bool isTimeCasa, Formacao formacaoPadrao, ResultadoImportacao resultado)
        {
            if (lineup == null) return;

            // Se o time já tem jogadores cadastrados, não incluir novamente
            bool timeJaTemJogadores = await _context.Jogadores.AnyAsync(j => j.TimeId == time.Id);
            if (timeJaTemJogadores)
            {
                _logger.LogInformation("Time {Time} já possui jogadores cadastrados. Não serão incluídos novamente.", time.Nome);
            }

            // Remove escalações antigas deste time/fase neste jogo
            var antigas = jogo.Escalacoes?
                .Where(e => e.IsTimeCasa == isTimeCasa &&
                            (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .ToList() ?? new();

            if (antigas.Any())
                _context.Escalacoes.RemoveRange(antigas);

            // Posições da formação padrão para distribuir visualmente
            var posicoesForm = await _context.PosicoesFormacao
                .Where(p => p.FormacaoId == formacaoPadrao.Id)
                .OrderBy(p => p.Ordem)
                .ToListAsync();

            int posIdx = 0;

            // Titulares
            foreach (var p in lineup.StartingLineups)
            {
                Jogador? jogador = null;

                if (!timeJaTemJogadores)
                    jogador = await ResolverJogador(p, time, resultado);
                else
                    jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Nome == p.Name && j.TimeId == time.Id);

                double posX = posIdx < posicoesForm.Count ? posicoesForm[posIdx].PosicaoX : 50;
                double posY = posIdx < posicoesForm.Count ? posicoesForm[posIdx].PosicaoY : 50;

                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogador?.Id,
                    Titular = true,
                    IsTimeCasa = isTimeCasa,
                    Posicao = MapearPosicao(p.Position ?? 0),
                    PosicaoX = posX,
                    PosicaoY = posY,
                    FaseEscalacao = "INICIAL"
                });
                posIdx++;
            }

            // Reservas
            foreach (var p in lineup.Substitutes)
            {
                Jogador? jogador = null;

                if (!timeJaTemJogadores)
                    jogador = await ResolverJogador(p, time, resultado);
                else
                    jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Nome == p.Name && j.TimeId == time.Id);

                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogador?.Id,
                    Titular = false,
                    IsTimeCasa = isTimeCasa,
                    Posicao = "RES",
                    PosicaoX = 0,
                    PosicaoY = 0,
                    FaseEscalacao = "INICIAL"
                });
            }
        }



        // ── Resolve ou cria jogador ───────────────────────────────────────────
        private async Task<Jogador?> ResolverJogador(AllSportsPlayer p, Time time, ResultadoImportacao resultado)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                resultado.Avisos.Add($"Jogador ignorado: nome vazio no time {time.Nome}");
                return null;
            }

            // 🔹 Busca por IdApi primeiro
            var jogador = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.IdApi == p.PlayerKey && j.TimeId == time.Id);

            // 🔹 Fallback por nome
            if (jogador == null)
            {
                jogador = await _context.Jogadores
                    .FirstOrDefaultAsync(j => j.Nome == p.Name && j.TimeId == time.Id);
            }

            if (jogador == null)
            {
                jogador = new Jogador
                {
                    Nome = p.Name,
                    NumeroCamisa = p.Number,
                    Posicao = MapearPosicao(p.Position ?? 0),
                    TimeId = time.Id,
                    DtInc = DateTime.UtcNow,
                    DtAlt = null,
                    IdApi = p.PlayerKey // 🔹 salva identificador único
                };

                _logger.LogInformation("Incluindo Jogador: Nome={Nome}, Numero={Numero}, Posicao={Posicao}, Time={Time}, IdApi={IdApi}",
                    jogador.Nome, jogador.NumeroCamisa, jogador.Posicao, time.Nome, jogador.IdApi);

                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();

                resultado.JogadoresNovos++;
            }

            return jogador;
        }


        private int ConverterMinuto(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return 0;

            // Exemplo: "45+2" → pega 45 e soma 2
            if (timeStr.Contains("+"))
            {
                var partes = timeStr.Split('+');
                if (int.TryParse(partes[0], out int baseMin) &&
                    int.TryParse(partes[1], out int acrescimo))
                {
                    return baseMin + acrescimo;
                }
                return baseMin;
            }

            // Exemplo: "12" → converte direto
            if (int.TryParse(timeStr, out int minuto))
                return minuto;

            return 0; // fallback
        }

        // ── Importa gols ──────────────────────────────────────────────────────
        private async Task ImportarGols(AllSportsJogo jogo, Jogo jogoDb, Time timeCasa, Time timeVisitante, ResultadoImportacao resultado)
        {
            foreach (var gol in jogo.Goalscorers)
            {
                long? playerKey = gol.PlayerKey; // 🔹 agora vem direto da propriedade auxiliar
                int? timeId = gol.HomeScorer != null ? timeCasa.Id : timeVisitante.Id;

                if (!playerKey.HasValue || playerKey.Value == 0)
                {
                    resultado.Avisos.Add($"Gol ignorado: player_key inválido no jogo {jogo.EventKey}");
                    continue;
                }

                var jogador = await _context.Jogadores
                    .FirstOrDefaultAsync(j => j.IdApi == playerKey && j.TimeId == timeId);

                if (jogador == null)
                {
                    resultado.Avisos.Add($"Gol ignorado: jogador com IdApi '{playerKey}' não encontrado no time {timeId}");
                    continue;
                }

                _logger.LogInformation("Incluindo gol: Jogo={JogoId}, Jogador={Jogador}, Minuto={Minuto}, Score={Score}, IdApi={IdApi}",
                    jogoDb.Id, jogador.Nome, gol.Time, gol.Score, jogador.IdApi);

                _context.Gols.Add(new Gol
                {
                    JogoId = jogoDb.Id,
                    JogadorId = jogador.Id,
                    Minuto = ConverterMinuto(gol.Time)
                });
            }

            await _context.SaveChangesAsync();
        }




        // ── Importa cartões ───────────────────────────────────────────────────
        private async Task ImportarCartoes(Jogo jogo, List<AllSportsCard> cards, Time timeCasa, Time timeVis)
        {
            foreach (var c in cards)
            {
                int.TryParse(c.Time.Replace("+", "").Trim(), out int minuto);
                string tipo = c.CardType.Contains("red", StringComparison.OrdinalIgnoreCase)
                    ? "Vermelho" : "Amarelo";

                // 🔹 HomeFault
                if (!string.IsNullOrWhiteSpace(c.HomeFault))
                {
                    var jogador = await _context.Jogadores
                        .FirstOrDefaultAsync(j => j.IdApi == c.PlayerKey && j.TimeId == timeCasa.Id);

                    if (jogador == null)
                    {
                        jogador = await _context.Jogadores
                            .FirstOrDefaultAsync(j => j.Nome.Contains(c.HomeFault) && j.TimeId == timeCasa.Id);
                    }

                    if (jogador != null)
                    {
                        _context.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id,
                            JogadorId = jogador.Id,
                            Minuto = minuto,
                            Tipo = tipo
                        });
                    }
                }

                // 🔹 AwayFault
                if (!string.IsNullOrWhiteSpace(c.AwayFault))
                {
                    var jogador = await _context.Jogadores
                        .FirstOrDefaultAsync(j => j.IdApi == c.PlayerKey && j.TimeId == timeVis.Id);

                    if (jogador == null)
                    {
                        jogador = await _context.Jogadores
                            .FirstOrDefaultAsync(j => j.Nome.Contains(c.AwayFault) && j.TimeId == timeVis.Id);
                    }

                    if (jogador != null)
                    {
                        _context.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id,
                            JogadorId = jogador.Id,
                            Minuto = minuto,
                            Tipo = tipo
                        });
                    }
                }
            }
        }



        // ── Helpers ───────────────────────────────────────────────────────────

        private static string MapearPosicao(int pos) => pos switch
        {
            1 => "GL",
            2 or 3 or 4 or 5 => "ZG",
            6 or 7 or 8 => "MC",
            9 or 10 or 11 => "AT",
            _ => "MC"
        };

     

        private static string LimparNomeGol(string nome)
        {
            // Remove "(pen.)", "(o.g.)" etc.
            var idx = nome.IndexOf('(');
            return idx >= 0 ? nome[..idx].Trim() : nome.Trim();
        }

        // Extensão para Task de null (evitar erro de compilação)
        private class ResultadoImportacao
        {
            public int JogosImportados { get; set; }
            public int JogosAtualizados { get; set; }
            public int TimesNovos { get; set; }
            public int JogadoresNovos { get; set; }
            public List<string> Avisos { get; } = new();
        }
    }

    // Extensão para tornar FirstOrDefault<T> awaitable em IEnumerable
    internal static class EnumerableExtensions
    {
        internal static Task<T?> AsTask<T>(this T? value) =>
            Task.FromResult(value);
    }
}