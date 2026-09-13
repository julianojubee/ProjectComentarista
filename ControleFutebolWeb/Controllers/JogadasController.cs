using System.Diagnostics;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Prancheta tática de jogadas: o usuário desenha, passo a passo, como o time
    /// constrói um lance (goleiro → lateral → cruzamento → finalização) e o player
    /// da tela anima a sequência interpolando as posições entre os passos.
    ///
    /// Cada jogada nasce dentro da análise de um jogo (modal "Jogadas" de
    /// /Jogos/Analisar) mas guarda também o TimeId, o que a devolve no arsenal de
    /// /Times/Details — o acervo de jogadas daquele time em todas as análises.
    ///
    /// Só JSON: o desenho e a animação são inteiramente client-side (jogadas.js).
    /// </summary>
    public class JogadasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<JogadasController> _log;

        public JogadasController(FutebolContext context, UserManager<ApplicationUser> userManager,
            IConfiguration config, ILogger<JogadasController> log)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _log = log;
        }

        // Tetos de tamanho do storyboard. Não são regra de negócio: são o limite do
        // que faz sentido animar (e o que impede um POST forjado de gravar um blob
        // gigante numa coluna text).
        private const int MaxPassos = 40;
        private const int MaxPecas = 36;   // os 22 em campo, com folga para reservas
        private const int MaxSetas = 20;
        private const int MaxGrupos = 6;   // mesmo teto do editor (jogadas.js)

        // Vocabulários do motor v2: qualquer outro valor vira o padrão, para um
        // POST forjado não gravar tipo de bola ou ritmo que o player não conhece.
        private static readonly HashSet<string> TiposDeBola =
            new() { "toque", "passe", "conducao", "cruzamento", "lancamento", "chute" };

        private static readonly HashSet<string> Ritmos = new() { "andar", "trote", "sprint" };
        private const int MaxLegenda = 120;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // ── DTOs do storyboard (formato documentado em Models/Jogada.cs) ──────

        public class PontoDto
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class PecaDto : PontoDto
        {
            public int Id { get; set; }

            // Ritmo do deslocamento e fração do passo que a peça espera antes de
            // sair. São do motor v2 e vinham sendo descartados na gravação, o que
            // devolvia toda jogada salva em trote e sem atraso.
            public string? Modo { get; set; }
            public double Atraso { get; set; }
        }

        public class SetaDto
        {
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
        }

        public class PassoDto
        {
            public string? Legenda { get; set; }

            // Tipo de bola do trecho deste passo para o próximo (toque, passe,
            // condução, cruzamento, lançamento, chute).
            public string? Passe { get; set; }

            public PontoDto? Bola { get; set; }
            public List<PecaDto> Pecas { get; set; } = new();
            public List<SetaDto> Setas { get; set; } = new();
        }

        /// <summary>
        /// Bloco tático: jogadores que se movem juntos na prancheta (a linha de
        /// quatro, o triângulo do meio). Vale a jogada inteira, e não um passo —
        /// o que muda de passo para passo é onde o bloco está, não quem o compõe.
        /// </summary>
        public class GrupoDto
        {
            public string? Id { get; set; }
            public string? Nome { get; set; }
            public List<int> Ids { get; set; } = new();
        }

        public class ElencoDto
        {
            public int Id { get; set; }
            public string? Num { get; set; }
            public string? Nome { get; set; }
            public string? Sigla { get; set; }

            // Peça da marcação adversária (o outro time da partida). Ausente nas
            // jogadas gravadas antes disso existir, quando só o time atacante
            // entrava em campo — false é a leitura certa para elas.
            public bool Adv { get; set; }

            // Foto do jogador (URL do MediaProxy), copiada junto com o nome pelo
            // mesmo motivo: o arsenal do time redesenha a jogada sem voltar ao
            // elenco do jogo de origem.
            public string? Foto { get; set; }
        }

        /// <summary>Cores das camisas na prancheta, escolhidas pelo usuário.</summary>
        public class CoresDto
        {
            public string? Nos { get; set; }
            public string? Adv { get; set; }
        }

        public class StoryboardDto
        {
            public int V { get; set; } = 1;
            public int Ms { get; set; } = 900;
            public List<ElencoDto> Elenco { get; set; } = new();
            public CoresDto? Cores { get; set; }
            public List<GrupoDto> Grupos { get; set; } = new();
            public List<PassoDto> Passos { get; set; } = new();
        }

        public class SalvarJogadaRequest
        {
            public int? Id { get; set; }
            public int JogoId { get; set; }
            public int TimeId { get; set; }
            public string? Nome { get; set; }
            public string? Descricao { get; set; }
            public StoryboardDto? Storyboard { get; set; }
        }

        public class ExcluirJogadaRequest
        {
            public int Id { get; set; }
        }

        // ── Leitura ──────────────────────────────────────────────────────────

        // GET: /Jogadas/DoJogo/5 — abre o modal de jogadas da análise: os dois times
        // com elenco arrastável, a escalação atual (semente do primeiro passo) e as
        // jogadas já salvas.
        [HttpGet]
        public async Task<IActionResult> DoJogo(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var jogadas = await _context.Jogadas
                .AsNoTracking()
                .Where(j => j.JogoId == id && j.UsuarioId == uid)
                .OrderBy(j => j.Ordem).ThenBy(j => j.Id)
                .ToListAsync();

            return Json(new
            {
                casa = await MontarLadoAsync(jogo, ehCasa: true, uid, jogadas),
                visitante = await MontarLadoAsync(jogo, ehCasa: false, uid, jogadas)
            });
        }

        // GET: /Jogadas/DoTime/5 — arsenal do time em /Times/Details: todas as jogadas
        // que o usuário desenhou para ele, em qualquer análise. Só leitura (o storyboard
        // carrega o próprio elenco, ver Models/Jogada.cs).
        [HttpGet]
        public async Task<IActionResult> DoTime(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogadas = await _context.Jogadas
                .AsNoTracking()
                .Include(j => j.Jogo).ThenInclude(g => g.TimeCasa)
                .Include(j => j.Jogo).ThenInclude(g => g.TimeVisitante)
                .Where(j => j.TimeId == id && j.UsuarioId == uid)
                .OrderByDescending(j => j.AtualizadoEm)
                .ToListAsync();

            return Json(jogadas.Select(j =>
            {
                var adversario = j.Jogo.TimeCasaId == id ? j.Jogo.TimeVisitante?.Nome : j.Jogo.TimeCasa?.Nome;
                return new
                {
                    id = j.Id,
                    nome = j.Nome,
                    descricao = j.Descricao,
                    jogoId = j.JogoId,
                    adversario,
                    data = j.Jogo.Data?.ToString("dd/MM/yyyy"),
                    atualizadoEm = j.AtualizadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    storyboard = LerStoryboard(j.PassosJson)
                };
            }));
        }

        // ── Escrita ──────────────────────────────────────────────────────────

        // POST: /Jogadas/Salvar — cria ou sobrescreve a jogada inteira (o editor
        // manda sempre o storyboard completo, não um diff).
        [HttpPost]
        public async Task<IActionResult> Salvar([FromBody] SalvarJogadaRequest req)
        {
            if (req?.Storyboard == null) return BadRequest("Dados inválidos.");

            var uid = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == req.JogoId);
            if (jogo == null) return NotFound();

            // O time tem que ser um dos dois da partida: a jogada é o lance de quem
            // está em campo, e é isso que faz o arsenal do time bater com o jogo.
            if (req.TimeId != jogo.TimeCasaId && req.TimeId != jogo.TimeVisitanteId)
                return BadRequest("O time não participa deste jogo.");

            var storyboard = Normalizar(req.Storyboard);
            if (storyboard.Passos.Count == 0)
                return BadRequest("A jogada precisa de pelo menos um passo.");

            var nome = string.IsNullOrWhiteSpace(req.Nome) ? "Jogada sem nome" : req.Nome.Trim();
            if (nome.Length > 80) nome = nome[..80];
            var descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
            if (descricao?.Length > 400) descricao = descricao[..400];

            var agora = DateTime.UtcNow;
            Jogada jogada;

            if (req.Id is > 0)
            {
                jogada = (await _context.Jogadas
                    .FirstOrDefaultAsync(j => j.Id == req.Id && j.UsuarioId == uid))!;
                if (jogada == null) return NotFound();
            }
            else
            {
                var ordemMax = await _context.Jogadas
                    .Where(j => j.JogoId == req.JogoId && j.TimeId == req.TimeId && j.UsuarioId == uid)
                    .Select(j => (int?)j.Ordem).MaxAsync() ?? 0;

                jogada = new Jogada
                {
                    JogoId = req.JogoId,
                    TimeId = req.TimeId,
                    UsuarioId = uid,
                    Ordem = ordemMax + 1,
                    CriadoEm = agora
                };
                _context.Jogadas.Add(jogada);
            }

            jogada.Nome = nome;
            jogada.Descricao = descricao;
            jogada.PassosJson = JsonSerializer.Serialize(storyboard, JsonOpts);
            jogada.AtualizadoEm = agora;

            await _context.SaveChangesAsync();

            return Ok(new { id = jogada.Id, nome = jogada.Nome, descricao = jogada.Descricao, storyboard });
        }

        // POST: /Jogadas/Excluir
        [HttpPost]
        public async Task<IActionResult> Excluir([FromBody] ExcluirJogadaRequest req)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogada = await _context.Jogadas
                .FirstOrDefaultAsync(j => j.Id == req.Id && j.UsuarioId == uid);
            if (jogada == null) return NotFound();

            _context.Jogadas.Remove(jogada);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }

        // ── Vídeo: WebM → MP4 ────────────────────────────────────────────────
        //
        // O editor grava a jogada com o MediaRecorder do navegador, e só o Chrome
        // e o Edge gravam MP4 direto; no Firefox sai WebM (VP8/VP9), que o
        // Instagram, o TikTok e o WhatsApp recusam no upload. Como o vídeo existe
        // justamente para sair do sistema, o servidor faz o remux/transcode com o
        // ffmpeg e devolve sempre MP4 — o navegador do usuário deixa de decidir o
        // formato do arquivo final.
        //
        // Quem já gravou em MP4 não passa por aqui: o cliente só chama esta rota
        // quando o que ele tem nas mãos é WebM.

        private const long MaxVideoBytes = 80L * 1024 * 1024;

        [HttpPost]
        [RequestSizeLimit(MaxVideoBytes + 1024 * 1024)]
        public async Task<IActionResult> ConverterMp4(IFormFile? arquivo)
        {
            if (arquivo == null || arquivo.Length == 0) return BadRequest("Nenhum vídeo enviado.");
            if (arquivo.Length > MaxVideoBytes) return BadRequest("Vídeo grande demais para converter.");

            var ffmpeg = CaminhoFfmpeg();
            if (ffmpeg == null)
            {
                // 503 e não 500: não é erro do pedido, é uma capacidade que este
                // servidor não tem. O cliente usa isso para baixar o WebM mesmo.
                return StatusCode(503, "O servidor não tem ffmpeg instalado para converter o vídeo.");
            }

            // Os temporários levam um nome aleatório e são apagados sempre: são
            // vídeos de análise de um usuário, não podem sobrar no disco nem
            // colidir entre duas exportações simultâneas.
            var baseTmp = Path.Combine(Path.GetTempPath(), "jgd-" + Guid.NewGuid().ToString("N"));
            var entrada = baseTmp + ".webm";
            var saida = baseTmp + ".mp4";

            try
            {
                await using (var fs = System.IO.File.Create(entrada))
                    await arquivo.CopyToAsync(fs);

                // -movflags +faststart põe o índice no começo do arquivo: sem isso
                // o vídeo só começa a tocar depois de baixado inteiro, o que as
                // redes sociais e o preview do WhatsApp odeiam.
                // yuv420p é o pixel format que os players não-Chrome aceitam.
                var args = new[]
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-i", entrada,
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-movflags", "+faststart",
                    "-an",
                    saida
                };

                var psi = new ProcessStartInfo(ffmpeg)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var a in args) psi.ArgumentList.Add(a);

                using var proc = Process.Start(psi);
                if (proc == null) return StatusCode(503, "Não foi possível iniciar o ffmpeg.");

                var erroTask = proc.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(true); } catch { /* já morreu */ }
                    return StatusCode(504, "A conversão demorou demais e foi cancelada.");
                }

                if (proc.ExitCode != 0 || !System.IO.File.Exists(saida))
                {
                    _log.LogWarning("ffmpeg falhou ({Codigo}): {Erro}", proc.ExitCode, await erroTask);
                    return StatusCode(500, "O ffmpeg não conseguiu converter este vídeo.");
                }

                // Lido para memória porque o arquivo temporário some no finally: um
                // FileStream devolvido aqui ainda estaria sendo enviado quando o
                // arquivo fosse apagado.
                var mp4 = await System.IO.File.ReadAllBytesAsync(saida);
                return File(mp4, "video/mp4");
            }
            finally
            {
                try { if (System.IO.File.Exists(entrada)) System.IO.File.Delete(entrada); } catch { }
                try { if (System.IO.File.Exists(saida)) System.IO.File.Delete(saida); } catch { }
            }
        }

        // Caminho do ffmpeg: o do appsettings ("Ffmpeg:Caminho") vence, senão o do
        // PATH. Devolve null quando não há nenhum — é o que faz a rota responder
        // "este servidor não converte" em vez de estourar.
        private string? CaminhoFfmpeg()
        {
            var configurado = _config["Ffmpeg:Caminho"];
            if (!string.IsNullOrWhiteSpace(configurado))
                return System.IO.File.Exists(configurado) ? configurado : null;

            var nome = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var alvo = Path.Combine(dir.Trim(), nome);
                    if (System.IO.File.Exists(alvo)) return alvo;
                }
                catch (ArgumentException) { /* diretório inválido no PATH */ }
            }
            return null;
        }

        // ── Montagem do payload por lado ─────────────────────────────────────

        // Foto e escudo saem pelo MediaProxy, como no match-up: a origem é externa
        // e o proxy é quem resolve cache e hotlink.
        private string? ImagemUrl(string? url) =>
            string.IsNullOrEmpty(url) ? null : Url.Action("Imagem", "MediaProxy", new { url });

        private async Task<object> MontarLadoAsync(Jogo jogo, bool ehCasa, string uid, List<Jogada> jogadas)
        {
            var time = ehCasa ? jogo.TimeCasa : jogo.TimeVisitante;
            var timeId = ehCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;

            // Elenco arrastável: todo mundo do time (em seleção o vínculo é SelecaoId,
            // TimeId aponta pro clube — mesmo critério do Match-up).
            bool ehSelecao = time?.EhSelecao == true;
            var elenco = await _context.Jogadores
                .AsNoTracking()
                .Where(j => j.TimeId == timeId || (ehSelecao && j.SelecaoId == timeId))
                .OrderBy(j => j.NumeroCamisa == null).ThenBy(j => j.NumeroCamisa).ThenBy(j => j.Nome)
                .Select(j => new
                {
                    id = j.Id,
                    num = j.NumeroCamisa != null ? j.NumeroCamisa.ToString() : "",
                    nome = j.Nome,
                    sigla = j.Posicao,
                    fotoUrl = j.FotoUrl
                })
                .ToListAsync();

            // Escalação titular deste jogo (a do usuário vence a importada da API),
            // convertida para o campo horizontal: é a semente do primeiro passo, para
            // a jogada já começar com o time posicionado.
            var escalacoes = await _context.Escalacoes
                .AsNoTracking()
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == jogo.Id && e.Titular && e.IsTimeCasa == ehCasa
                         && e.JogadorId != null
                         && (e.UsuarioId == uid || e.UsuarioId == null)
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .ToListAsync();

            var escalacao = escalacoes
                .GroupBy(e => e.JogadorId)
                .Select(g => g.OrderBy(e => e.UsuarioId == null ? 1 : 0).First())
                .Select(e =>
                {
                    var (x, y) = ParaCampoHorizontal(e.PosicaoX, e.PosicaoY);
                    return new
                    {
                        id = e.Jogador!.Id,
                        num = e.Jogador.NumeroCamisa?.ToString() ?? "",
                        nome = e.Jogador.Nome,
                        sigla = PosicaoJogadorHelper.Sigla(e.Posicao),
                        foto = ImagemUrl(e.Jogador.FotoUrl),
                        x,
                        y
                    };
                })
                .OrderBy(e => e.x)
                .ToList();

            return new
            {
                timeId,
                nome = time?.Nome ?? "",
                escudo = string.IsNullOrEmpty(time?.EscudoUrl)
                    ? null
                    : Url.Action("Imagem", "MediaProxy", new { url = time!.EscudoUrl }),
                elenco = elenco.Select(j => new
                {
                    j.id,
                    j.num,
                    j.nome,
                    sigla = PosicaoJogadorHelper.Sigla(j.sigla),
                    foto = ImagemUrl(j.fotoUrl)
                }),
                escalacao,
                jogadas = jogadas.Where(j => j.TimeId == timeId).Select(j => new
                {
                    id = j.Id,
                    nome = j.Nome,
                    descricao = j.Descricao,
                    storyboard = LerStoryboard(j.PassosJson)
                })
            };
        }

        // Campo da tela Analisar (vertical: X é a lateral, Y vai de 0 no ataque a 100
        // no próprio gol — ver MatchUpHelper) → campo da prancheta (horizontal, o time
        // sempre atacando da esquerda para a direita).
        //
        // As duas faixas são comprimidas de propósito: a peça é ancorada pelo centro e
        // leva sigla acima e nome abaixo do círculo, então encostar na borda cortaria o
        // texto no overflow do campo.
        private static (double X, double Y) ParaCampoHorizontal(double posicaoX, double posicaoY)
        {
            double x = 5 + (100 - Math.Clamp(posicaoY, 0, 100)) * 0.90;
            double y = 8 + Math.Clamp(posicaoX, 0, 100) * 0.84;
            return (Math.Round(x, 2), Math.Round(y, 2));
        }

        // ── Storyboard: leitura tolerante e normalização na gravação ─────────

        // Jogada gravada por uma versão anterior (ou linha corrompida na mão) não pode
        // derrubar a listagem inteira — devolve um storyboard vazio e a jogada aparece
        // sem passos em vez de estourar 500.
        private static StoryboardDto LerStoryboard(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new StoryboardDto();
            try
            {
                return JsonSerializer.Deserialize<StoryboardDto>(json, JsonOpts) ?? new StoryboardDto();
            }
            catch (JsonException)
            {
                return new StoryboardDto();
            }
        }

        // Só cor hexadecimal: o valor vai direto para o style da peça, e é a única
        // forma dele de um POST forjado não virar CSS arbitrário.
        private static string? Cor(string? v) =>
            v != null && System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9a-fA-F]{6}$") ? v : null;

        private static List<GrupoDto> NormalizarGrupos(StoryboardDto s)
        {
            var noElenco = s.Elenco.Take(MaxPecas).Select(e => e.Id).ToHashSet();
            var usado = new HashSet<int>();

            return s.Grupos.Take(MaxGrupos).Select((g, i) => new GrupoDto
            {
                Id = Cortar(g.Id, 20) ?? $"g{i}",
                Nome = Cortar(g.Nome, 40),
                Ids = g.Ids.Where(id => noElenco.Contains(id) && usado.Add(id)).Take(MaxPecas).ToList()
            }).Where(g => g.Ids.Count >= 2).ToList();
        }

        private static StoryboardDto Normalizar(StoryboardDto s)
        {
            static double C(double v) => Math.Round(Math.Clamp(v, 0, 100), 2);

            return new StoryboardDto
            {
                V = 1,
                Ms = Math.Clamp(s.Ms == 0 ? 900 : s.Ms, 200, 5000),
                Elenco = s.Elenco.Take(MaxPecas).Select(e => new ElencoDto
                {
                    Id = e.Id,
                    Num = Cortar(e.Num, 3),
                    Nome = Cortar(e.Nome, 60),
                    Sigla = Cortar(e.Sigla, 6),
                    Adv = e.Adv,
                    Foto = Cortar(e.Foto, 400)
                }).ToList(),
                Cores = s.Cores == null ? null : new CoresDto
                {
                    Nos = Cor(s.Cores.Nos),
                    Adv = Cor(s.Cores.Adv)
                },
                // Um jogador só entra num bloco: blocos sobrepostos deixariam o
                // arrasto sem resposta certa. Bloco com menos de dois membros não
                // é bloco (o outro pode ter saído da jogada).
                Grupos = NormalizarGrupos(s),
                Passos = s.Passos.Take(MaxPassos).Select(p => new PassoDto
                {
                    Legenda = Cortar(p.Legenda, MaxLegenda),
                    Passe = TiposDeBola.Contains(p.Passe ?? "") ? p.Passe : "passe",
                    Bola = p.Bola == null ? null : new PontoDto { X = C(p.Bola.X), Y = C(p.Bola.Y) },
                    Pecas = p.Pecas.Take(MaxPecas)
                        .Select(c => new PecaDto
                        {
                            Id = c.Id,
                            X = C(c.X),
                            Y = C(c.Y),
                            Modo = Ritmos.Contains(c.Modo ?? "") ? c.Modo : "trote",
                            Atraso = Math.Round(Math.Clamp(c.Atraso, 0, 0.6), 2)
                        }).ToList(),
                    Setas = p.Setas.Take(MaxSetas)
                        .Select(t => new SetaDto { X1 = C(t.X1), Y1 = C(t.Y1), X2 = C(t.X2), Y2 = C(t.Y2) }).ToList()
                }).ToList()
            };
        }

        private static string? Cortar(string? v, int max)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            v = v.Trim();
            return v.Length > max ? v[..max] : v;
        }
    }
}
