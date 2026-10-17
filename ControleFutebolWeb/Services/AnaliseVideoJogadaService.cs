using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace ControleFutebolWeb.Services
{
    /// <summary>Jogador que pode aparecer no lance: id do banco + o que a IA precisa para reconhecê-lo.</summary>
    public record JogadorDoLance(int Id, string Num, string Nome, string Sigla, bool Adv);

    public record PecaAnalisada(int Id, double X, double Y, string Modo);

    public record PassoAnalisado(double Segundo, string Legenda, string Passe, double BolaX, double BolaY, List<PecaAnalisada> Pecas);

    public record LanceAnalisado(string Nome, string Descricao, List<PassoAnalisado> Passos);

    /// <summary>
    /// Transforma um vídeo de lance (o clipe da transmissão) na jogada da prancheta.
    ///
    /// O algoritmo tem duas metades:
    ///   1. ffmpeg recorta o trecho pedido e tira frames em intervalos regulares
    ///      (no máximo <see cref="MaxFrames"/>), cada um carimbado com o segundo do vídeo;
    ///   2. o Claude recebe os frames em ordem, junto com os titulares dos dois times, e
    ///      devolve os momentos-chave do lance (cada passe, condução, cruzamento,
    ///      finalização) com a posição de cada jogador e da bola em % do campo.
    ///
    /// A visão computacional "clássica" (detector + homografia pelas linhas do campo)
    /// foi descartada: a câmera da transmissão faz pan e zoom o tempo todo, metade dos
    /// jogadores sai do quadro e a homografia teria que ser recalculada frame a frame.
    /// O modelo de visão resolve as três coisas de uma vez — reconhece o pedaço do
    /// campo pelas marcações, separa os times pela camisa e lê o número nas costas.
    ///
    /// O resultado é um rascunho: a resposta já sai no referencial da prancheta
    /// (o time da jogada atacando da esquerda para a direita), mas o usuário revisa
    /// no editor antes de salvar.
    /// </summary>
    public class AnaliseVideoJogadaService
    {
        public const int MaxFrames = 24;
        private const int MaxPassos = 16;
        private const string Modelo = "claude-opus-5-5";

        private readonly IConfiguration _config;
        private readonly ILogger<AnaliseVideoJogadaService> _log;

        public AnaliseVideoJogadaService(IConfiguration config, ILogger<AnaliseVideoJogadaService> log)
        {
            _config = config;
            _log = log;
        }

        // Chave da API: user-secrets/appsettings ("Anthropic:ApiKey") ou a variável
        // de ambiente padrão do SDK. Sem nenhuma das duas a rota responde 503.
        public string? ChaveApi()
        {
            var k = _config["Anthropic:ApiKey"];
            if (!string.IsNullOrWhiteSpace(k)) return k;
            k = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            return string.IsNullOrWhiteSpace(k) ? null : k;
        }

        // ── 1. Frames ────────────────────────────────────────────────────────

        public record Frame(double Segundo, byte[] Jpeg);

        public async Task<List<Frame>> ExtrairFramesAsync(string ffmpeg, string video,
            double? inicio, double? fim, CancellationToken ct)
        {
            var duracao = await DuracaoAsync(ffmpeg, video, ct)
                ?? throw new InvalidOperationException("Não consegui ler a duração do vídeo.");

            double ini = Math.Clamp(inicio ?? 0, 0, duracao);
            double end = Math.Clamp(fim ?? duracao, ini, duracao);
            if (end - ini < 1) throw new InvalidOperationException("O trecho escolhido tem menos de 1 segundo.");

            // Um frame a cada ~0,75s é o bastante para não perder um passe; trechos
            // longos são espaçados para caber no teto de frames.
            double passo = Math.Max(0.75, (end - ini) / MaxFrames);
            int n = Math.Min(MaxFrames, (int)Math.Floor((end - ini) / passo) + 1);

            var dir = Path.Combine(Path.GetTempPath(), "jgdv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var frames = new List<Frame>();
                for (int i = 0; i < n; i++)
                {
                    double t = ini + i * passo;
                    var saida = Path.Combine(dir, $"f{i:D2}.jpg");
                    // -ss antes do -i = busca rápida pelo keyframe; 1024 px de largura é
                    // onde o número da camisa ainda é legível sem estourar tokens.
                    var (codigo, _) = await RodarAsync(ffmpeg, ct,
                        "-hide_banner", "-loglevel", "error", "-y",
                        "-ss", t.ToString("0.###", CultureInfo.InvariantCulture),
                        "-i", video, "-frames:v", "1", "-vf", "scale=1024:-2", "-q:v", "4", saida);
                    if (codigo == 0 && File.Exists(saida))
                        frames.Add(new Frame(Math.Round(t, 2), await File.ReadAllBytesAsync(saida, ct)));
                }
                if (frames.Count == 0) throw new InvalidOperationException("O ffmpeg não extraiu nenhum frame do vídeo.");
                return frames;
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* temporário */ }
            }
        }

        // "ffmpeg -i" sem saída falha de propósito, mas imprime a duração no stderr:
        // evita depender do ffprobe, que nem toda instalação traz.
        private static async Task<double?> DuracaoAsync(string ffmpeg, string video, CancellationToken ct)
        {
            var (_, erro) = await RodarAsync(ffmpeg, ct, "-hide_banner", "-i", video);
            var m = Regex.Match(erro, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return null;
            return int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        }

        private static async Task<(int Codigo, string Erro)> RodarAsync(string exe, CancellationToken ct, params string[] args)
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o ffmpeg.");
            var erroTask = proc.StandardError.ReadToEndAsync(ct);
            _ = proc.StandardOutput.ReadToEndAsync(ct);
            try
            {
                await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(true); } catch { /* já morreu */ }
                throw;
            }
            return (proc.ExitCode, await erroTask);
        }

        // ── 2. Leitura do lance pelo Claude ──────────────────────────────────

        public async Task<LanceAnalisado> AnalisarAsync(List<Frame> frames, string timeDaJogada, string adversario,
            List<JogadorDoLance> jogadores, string? contexto, CancellationToken ct)
        {
            var chave = ChaveApi() ?? throw new InvalidOperationException("Chave da API Anthropic não configurada.");
            var client = new AnthropicClient { ApiKey = chave };

            var conteudo = new List<BetaContentBlockParam>();
            foreach (var f in frames)
            {
                conteudo.Add(new BetaTextBlockParam { Text = $"Frame t={f.Segundo.ToString("0.0", CultureInfo.InvariantCulture)}s" });
                conteudo.Add(new BetaImageBlockParam
                {
                    Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(f.Jpeg), MediaType = "image/jpeg" }
                });
            }
            conteudo.Add(new BetaTextBlockParam { Text = MontarPedido(timeDaJogada, adversario, jogadores, contexto) });

            var parametros = new MessageCreateParams
            {
                Model = Modelo,
                MaxTokens = 64000,
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                OutputConfig = new BetaOutputConfig
                {
                    Effort = "high",
                    Format = new BetaJsonOutputFormat { Schema = Esquema(jogadores) }
                },
                System = SistemaPrompt,
                Messages = [new() { Role = Role.User, Content = conteudo }]
            };

            // Streaming porque a resposta (16 passos × 22 peças) e o raciocínio
            // passam fácil do que uma requisição síncrona aguenta sem timeout.
            var texto = new StringBuilder();
            string? parada = null;
            await foreach (var ev in client.Beta.Messages.CreateStreaming(parametros, ct))
            {
                if (ev.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var t))
                    texto.Append(t.Text);
                else if (ev.TryPickDelta(out var md) && md.Delta.StopReason is { } sr)
                    parada = sr.ToString();
            }

            if (parada != null && parada.Contains("refusal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A IA se recusou a analisar este vídeo.");
            if (parada != null && parada.Contains("max_tokens", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A análise ficou longa demais; tente um trecho menor do vídeo.");

            return Interpretar(texto.ToString(), jogadores);
        }

        private const string SistemaPrompt = """
            Você é um analista de desempenho de futebol. Recebe frames sequenciais de um lance
            filmado pela transmissão de TV e o reconstrói como uma jogada numa prancheta tática.

            SISTEMA DE COORDENADAS (sempre do ponto de vista do TIME DA JOGADA, nunca da tela):
            - x vai de 0 (linha de fundo do gol que o time da jogada DEFENDE) a 100 (linha de fundo
              do gol que ele ATACA). Meio-campo = 50; marca do pênalti do ataque ≈ 89; bico da
              grande área do ataque ≈ 83.
            - y vai de 0 (lateral do lado ESQUERDO de quem ataca) a 100 (lateral do lado DIREITO).
              Exemplo: se na imagem o time da jogada ataca para a direita da tela, a lateral do
              fundo da imagem (perto das placas de publicidade) é y=0 e a lateral mais próxima da
              câmera é y=100. Se ataca para a esquerda da tela, é o contrário.
            - Use as marcações do campo (círculo central, linha do meio, áreas, meia-lua, bandeirinha)
              e a perspectiva da câmera para estimar onde cada jogador está no campo inteiro, não
              no recorte da imagem.

            COMO MONTAR OS PASSOS:
            - Um passo por momento-chave: o início do lance e cada vez que a bola muda de dono ou de
              direção (passe, condução, cruzamento, lançamento, finalização). Ignore replays,
              comemorações e closes — só os frames com o lance em andamento contam.
            - "passe" descreve como a bola CHEGA à posição daquele passo: "toque" (curto),
              "passe", "conducao" (o jogador leva a bola correndo), "cruzamento", "lancamento"
              (bola longa) ou "chute". O primeiro passo usa "passe".
            - Em cada passo informe a bola e TODOS os jogadores que você consegue ver ou deduzir com
              segurança, dos dois times. Quem nunca aparece pode ficar de fora. Identifique pelo
              número da camisa, pela posição tática e pela continuidade entre frames.
            - "modo" é o ritmo com que o jogador chegou ali: "andar", "trote" ou "sprint".
            - Legendas curtas em português do Brasil, como um comentarista descreveria
              ("Vitinha inverte para Hakimi", "Dembélé finaliza de primeira").
            - Use apenas os ids da lista de jogadores fornecida.
            """;

        private static string MontarPedido(string timeDaJogada, string adversario, List<JogadorDoLance> jogadores, string? contexto)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Time da jogada (ataca para x=100): {timeDaJogada}.");
            sb.AppendLine($"Adversário (marcação, defende o gol de x=100): {adversario}.");
            sb.AppendLine();
            sb.AppendLine($"Titulares de {timeDaJogada} (id — número — nome — posição):");
            foreach (var j in jogadores.Where(j => !j.Adv))
                sb.AppendLine($"  {j.Id} — {j.Num} — {j.Nome} — {j.Sigla}");
            sb.AppendLine($"Titulares de {adversario}:");
            foreach (var j in jogadores.Where(j => j.Adv))
                sb.AppendLine($"  {j.Id} — {j.Num} — {j.Nome} — {j.Sigla}");
            if (!string.IsNullOrWhiteSpace(contexto))
            {
                sb.AppendLine();
                sb.AppendLine($"Contexto dado pelo usuário: {contexto.Trim()}");
            }
            sb.AppendLine();
            sb.AppendLine($"Reconstrua o lance em no máximo {MaxPassos} passos, em ordem cronológica.");
            return sb.ToString();
        }

        // Schema estrito: tipos de bola e ritmos fechados nos vocabulários do player, e
        // ids fechados nos titulares — a IA não consegue inventar jogador.
        private static Dictionary<string, JsonElement> Esquema(List<JogadorDoLance> jogadores)
        {
            var ponto = new { type = "number" };
            var esquema = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "nome", "descricao", "passos" },
                properties = new
                {
                    nome = new { type = "string", description = "Nome curto da jogada, ex.: 'Gol do PSG: troca de passes pela direita'" },
                    descricao = new { type = "string", description = "Resumo do lance em 1 ou 2 frases" },
                    passos = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "segundo", "legenda", "passe", "bola", "pecas" },
                            properties = new
                            {
                                segundo = new { type = "number", description = "t do frame que representa este momento" },
                                legenda = new { type = "string" },
                                passe = new { type = "string", @enum = new[] { "toque", "passe", "conducao", "cruzamento", "lancamento", "chute" } },
                                bola = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    required = new[] { "x", "y" },
                                    properties = new { x = ponto, y = ponto }
                                },
                                pecas = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        additionalProperties = false,
                                        required = new[] { "id", "x", "y", "modo" },
                                        properties = new
                                        {
                                            id = new { type = "integer", @enum = jogadores.Select(j => j.Id).ToArray() },
                                            x = ponto,
                                            y = ponto,
                                            modo = new { type = "string", @enum = new[] { "andar", "trote", "sprint" } }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var el = JsonSerializer.SerializeToElement(esquema);
            return el.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        }

        private LanceAnalisado Interpretar(string json, List<JogadorDoLance> jogadores)
        {
            var validos = jogadores.Select(j => j.Id).ToHashSet();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var passos = new List<PassoAnalisado>();
                foreach (var p in r.GetProperty("passos").EnumerateArray().Take(MaxPassos))
                {
                    var bola = p.GetProperty("bola");
                    var vistos = new HashSet<int>();
                    var pecas = p.GetProperty("pecas").EnumerateArray()
                        .Select(c => new PecaAnalisada(c.GetProperty("id").GetInt32(),
                            c.GetProperty("x").GetDouble(), c.GetProperty("y").GetDouble(),
                            c.GetProperty("modo").GetString() ?? "trote"))
                        // id repetido no mesmo passo: fica a primeira leitura
                        .Where(c => validos.Contains(c.Id) && vistos.Add(c.Id))
                        .ToList();
                    passos.Add(new PassoAnalisado(p.GetProperty("segundo").GetDouble(),
                        p.GetProperty("legenda").GetString() ?? "",
                        p.GetProperty("passe").GetString() ?? "passe",
                        bola.GetProperty("x").GetDouble(), bola.GetProperty("y").GetDouble(), pecas));
                }
                return new LanceAnalisado(r.GetProperty("nome").GetString() ?? "",
                    r.GetProperty("descricao").GetString() ?? "", passos);
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _log.LogWarning(e, "Resposta da IA fora do formato: {Json}", json.Length > 2000 ? json[..2000] : json);
                throw new InvalidOperationException("A IA devolveu uma resposta fora do formato esperado.");
            }
        }
    }
}
