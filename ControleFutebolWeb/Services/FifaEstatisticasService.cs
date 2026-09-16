using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Importa a estatística individual das jogadoras de uma partida da FIFA — a mesma
    /// que o match centre do fifa.com mostra na aba de estatísticas.
    ///
    /// Durante boa parte do Mundial Sub-20 Feminino 2026 isto não existia: a v3
    /// (api.fifa.com) não publica número por jogadora, e a competição dependia da
    /// marcação por vídeo. A FIFA passou a publicar num outro host, fdh-api.fifa.com,
    /// endereçado pelo IdIFES da partida (Properties.IdIFES no documento live/football)
    /// e indexado pelo mesmo IdPlayer da v3 — o que dispensa casar por nome quando a
    /// jogadora veio da importação de elenco.
    ///
    /// O que vem e o que não vem está em FonteEstatistica (CobertosPelaFifa): passes,
    /// finalizações, faltas, cartões e defesas sim; desarme, interceptação, bloqueio,
    /// duelo e passe-chave não. Estatística de TIME não é gravada: Jogo.EstatisticasJson
    /// é indexado pelo IdApi da api-football, e as seleções da FIFA ficam com IdApi 0.
    /// </summary>
    public class FifaEstatisticasService
    {
        /// <summary>
        /// Teto do lote de Serviços › Jogos sem estatísticas. Cada jogo custa duas
        /// chamadas (a partida e a estatística), então 40 cobre a fase de grupos de um
        /// Mundial de 16 seleções num clique sem virar rajada.
        /// </summary>
        public const int LimitePorLote = 40;

        private readonly FifaService _fifa;
        private readonly ILogger<FifaEstatisticasService> _logger;

        public FifaEstatisticasService(FifaService fifa, ILogger<FifaEstatisticasService> logger)
        {
            _fifa = fifa;
            _logger = logger;
        }

        public async Task<ResultadoFifa> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, idMatch, erro) = await _fifa.AbrirPartidaAsync(context, jogoId, ct);

            async Task<ResultadoFifa> RegistrarAsync(ResultadoFifa r)
            {
                await LogFonteExterna.RegistrarAsync(
                    context, LogFonteExterna.TipoFifa, "Importar estatísticas", r.Ok, jogo, r.Mensagem, ct);
                return r;
            }

            if (erro != null) return await RegistrarAsync(erro);

            using var _doc = doc!;
            var raiz = _doc.RootElement;

            var idIfes = raiz.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Object
                ? FifaService.Texto(props, "IdIFES")
                : null;

            if (string.IsNullOrWhiteSpace(idIfes))
                return await RegistrarAsync(new ResultadoFifa(false,
                    "A FIFA não informou o código de estatísticas (IdIFES) desta partida.", idMatch));

            using var stats = await _fifa.AbrirEstatisticasJogadorasAsync(idIfes, ct);
            if (stats == null || stats.RootElement.ValueKind != JsonValueKind.Object)
                return await RegistrarAsync(new ResultadoFifa(false,
                    "A FIFA ainda não publicou as estatísticas desta partida (só saem depois do jogo).", idMatch));

            var fichas = LerFichas(raiz, jogo!);
            if (fichas.Count == 0)
                return await RegistrarAsync(new ResultadoFifa(false,
                    "A FIFA publicou as estatísticas, mas não a relação de jogadoras da partida.", idMatch));

            var existentes = await context.EstatisticasJogador
                .Where(e => e.JogoId == jogo!.Id)
                .ToListAsync(ct);

            // Linha da api-football é a mais completa que existe e nunca é trocada — mesmo
            // critério do FotMob. Em jogo da FIFA ela não deveria existir; se existir,
            // alguém importou por outro caminho de propósito.
            if (existentes.Any(e => e.Fonte == FonteEstatistica.ApiFootball))
                return await RegistrarAsync(new ResultadoFifa(false,
                    "O jogo já tem estatísticas da api-football — não foram substituídas.", idMatch));

            // A marcação por vídeo é trabalho feito à mão, tecla a tecla, e cobre o bloco
            // defensivo que a FIFA não publica. Quem já foi consolidada pelo vídeo fica
            // como está; a importação preenche as demais.
            var marcadasPorVideo = existentes
                .Where(e => e.Fonte == FonteEstatistica.Video)
                .Select(e => e.JogadorId)
                .ToHashSet();

            var elencos = new Dictionary<bool, List<Jogador>>
            {
                [true] = await EspnEscalacaoService.ElencoAsync(context, jogo!.TimeCasaId, ct),
                [false] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeVisitanteId, ct),
            };

            var golsSofridos = GolsSofridosPorGoleira(raiz, fichas);

            var novas = new List<EstatisticaJogador>();
            var jaGravadas = new HashSet<int>();
            var naoCasaram = new List<string>();
            var preservadas = 0;

            foreach (var entrada in stats.RootElement.EnumerateObject())
            {
                if (!fichas.TryGetValue(entrada.Name, out var ficha)) continue;

                var v = LerValores(entrada.Value);

                // Sem minuto em campo ela não jogou: a FIFA lista o banco inteiro zerado.
                // Gravar linha para quem ficou sentada criaria uma partida disputada que
                // não existiu, poluindo média de jogos e o ranking.
                var minutos = (int)Math.Round(v.GetValueOrDefault("TimePlayed"));
                if (minutos <= 0) continue;

                // Primeiro pelo vínculo gravado na importação do elenco, que é exato;
                // depois pelo casamento de nome e camisa que a escalação e os lances usam.
                var elenco = elencos[ficha.EhCasa];
                var jogadora = elenco.FirstOrDefault(j => j.LinkTransfermarket == $"fifa:{entrada.Name}")
                            ?? EspnEscalacaoService.Casar(elenco, ficha.Nome, ficha.Camisa)?.Jogador;

                if (jogadora == null) { naoCasaram.Add(ficha.Nome); continue; }
                if (marcadasPorVideo.Contains(jogadora.Id)) { preservadas++; continue; }
                if (!jaGravadas.Add(jogadora.Id)) continue;

                int N(string chave) => (int)Math.Round(v.GetValueOrDefault(chave));

                var penaltis = N("Penalties");
                var convertidos = N("PenaltiesScored");

                novas.Add(new EstatisticaJogador
                {
                    JogoId            = jogo.Id,
                    JogadorId         = jogadora.Id,
                    Fonte             = FonteEstatistica.Fifa,
                    Minutos           = minutos,
                    // A FIFA não dá nota por jogadora. Null é "sem nota", que a tela
                    // mostra como "—".
                    Rating            = null,
                    Capitao           = ficha.Capita,

                    Gols              = N("Goals"),
                    Assistencias      = N("Assists"),
                    FinalizacoesTotal = N("AttemptAtGoal"),
                    FinalizacoesNoGol = N("AttemptAtGoalOnTarget"),
                    Offsides          = N("Offsides"),

                    PassesTotal       = N("Passes"),
                    PassesCertos      = N("PassesCompleted"),

                    // Só o completado vem — ver FonteEstatistica. Tentados ficam iguais
                    // aos certos para a linha não ter mais acerto que tentativa; o rating
                    // não lê "dribles" desta fonte.
                    DriblesCertos     = N("TakeOnsCompleted"),
                    DriblesTentados   = N("TakeOnsCompleted"),

                    // Conferido contra a timeline (BRA×TAN): o lance de falta aponta quem
                    // COMETEU, e é FoulsAgainst que acompanha essa contagem. FoulsFor é a
                    // falta sofrida.
                    FaltasCometidas   = N("FoulsAgainst"),
                    FaltasSofridas    = N("FoulsFor"),

                    CartoesAmarelos   = N("YellowCards"),
                    CartoesVermelhos  = N("RedCards"),

                    Defesas           = ficha.Goleira ? N("GoalkeeperSaves") : 0,
                    GolsSofridos      = ficha.Goleira ? golsSofridos.GetValueOrDefault(entrada.Name) : 0,

                    PenaltiConvertido = convertidos,
                    PenaltiPerdido    = Math.Max(0, penaltis - convertidos),

                    EntrouDoBanco     = !ficha.Titular,
                });
            }

            if (novas.Count == 0)
            {
                var motivo = preservadas > 0
                    ? $"Todas as {preservadas} jogadora(s) com estatística já estão consolidadas pela marcação por vídeo — nada foi substituído."
                    : naoCasaram.Count > 0
                        ? $"A FIFA publicou as estatísticas, mas nenhuma jogadora casou com o elenco ({naoCasaram.Count} sem correspondência)."
                        : "A FIFA publicou a partida sem minutos para nenhuma jogadora.";

                return await RegistrarAsync(new ResultadoFifa(false, motivo, idMatch));
            }

            // Troca atômica do que veio de fonte importada (FIFA de antes, ESPN, FotMob);
            // as linhas do vídeo ficam. O craque da partida de todos os analistas sai
            // junto porque a nota automática mudou de base — mesma regra do FotMob.
            context.EstatisticasJogador.RemoveRange(existentes.Where(e => e.Fonte != FonteEstatistica.Video));
            context.EstatisticasJogador.AddRange(novas);
            context.CraquesDaPartida.RemoveRange(
                await context.CraquesDaPartida.Where(c => c.JogoId == jogo.Id).ToListAsync(ct));

            await context.SaveChangesAsync(ct);

            if (naoCasaram.Count > 0)
                _logger.LogInformation(
                    "[FifaEstatisticas] Jogo {Id}: {N} jogadora(s) sem correspondência no elenco: {Nomes}",
                    jogo.Id, naoCasaram.Count, string.Join(", ", naoCasaram));

            var msg = $"Estatísticas da FIFA importadas: {novas.Count} jogadora(s).";
            if (preservadas > 0) msg += $" {preservadas} já marcada(s) por vídeo mantida(s).";
            if (naoCasaram.Count > 0) msg += $" {naoCasaram.Count} não casaram com o elenco e ficaram de fora.";

            return await RegistrarAsync(new ResultadoFifa(true, msg, idMatch));
        }

        private record Ficha(string Nome, int? Camisa, bool EhCasa, bool Titular, bool Goleira, bool Capita);

        /// <summary>
        /// De-para IdPlayer → nome, camisa, lado e papel, montado da relação de jogadoras
        /// da partida. O lado é conferido pelo IdTeam da FIFA (FifaService.ETimeDaFifa),
        /// não pela posição no documento.
        /// </summary>
        private static Dictionary<string, Ficha> LerFichas(JsonElement raiz, Jogo jogo)
        {
            var fichas = new Dictionary<string, Ficha>(StringComparer.Ordinal);

            foreach (var propriedade in new[] { "HomeTeam", "AwayTeam" })
            {
                if (!raiz.TryGetProperty(propriedade, out var time) ||
                    !time.TryGetProperty("Players", out var jogadoras) ||
                    jogadoras.ValueKind != JsonValueKind.Array) continue;

                bool ehCasa;
                if (FifaService.ETimeDaFifa(time, jogo.TimeCasa)) ehCasa = true;
                else if (FifaService.ETimeDaFifa(time, jogo.TimeVisitante)) ehCasa = false;
                else continue;

                foreach (var j in jogadoras.EnumerateArray())
                {
                    var id = FifaService.Texto(j, "IdPlayer");
                    var nome = FifaService.Localizado(j, "PlayerName");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nome)) continue;

                    fichas[id] = new Ficha(
                        FifaEscalacaoService.NormalizarNome(nome),
                        FifaService.NumeroNulavel(j, "ShirtNumber"),
                        ehCasa,
                        // Status 1 = titular, 2 = banco.
                        Titular: FifaService.Numero(j, "Status") == 1,
                        // Position 0 = goleira.
                        Goleira: FifaService.Numero(j, "Position") == 0,
                        Capita: j.TryGetProperty("Captain", out var c) && c.ValueKind == JsonValueKind.True);
                }
            }

            return fichas;
        }

        /// <summary>
        /// Métricas da jogadora: a FIFA manda cada uma como [nome, valor, publicado].
        /// </summary>
        private static Dictionary<string, double> LerValores(JsonElement lista)
        {
            var valores = new Dictionary<string, double>(StringComparer.Ordinal);
            if (lista.ValueKind != JsonValueKind.Array) return valores;

            foreach (var item in lista.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 2) continue;
                if (item[0].ValueKind != JsonValueKind.String || item[1].ValueKind != JsonValueKind.Number) continue;

                valores[item[0].GetString()!] = item[1].GetDouble();
            }

            return valores;
        }

        /// <summary>
        /// Gols sofridos por goleira. A FIFA não publica esse número por jogadora (as
        /// linhas de campo e de goleira trazem só defesas), então sai do placar.
        ///
        /// O caso comum — uma goleira o jogo inteiro — leva o placar do adversário. Com
        /// troca de goleira, cada gol vai para quem estava em campo no minuto dele,
        /// seguindo as substituições; se essa conta não fechar com o placar (minuto de
        /// troca em branco, expulsão de goleira), tudo fica com a titular em vez de
        /// arriscar dividir errado.
        /// </summary>
        private static Dictionary<string, int> GolsSofridosPorGoleira(
            JsonElement raiz, Dictionary<string, Ficha> fichas)
        {
            var resultado = new Dictionary<string, int>(StringComparer.Ordinal);
            var lados = new[] { "HomeTeam", "AwayTeam" };

            for (var i = 0; i < 2; i++)
            {
                if (!raiz.TryGetProperty(lados[i], out var time) ||
                    !raiz.TryGetProperty(lados[1 - i], out var adversario)) continue;

                var sofridos = FifaService.NumeroNulavel(adversario, "Score") ?? 0;

                // A goleira titular DESTE lado do documento (que pode ser o visitante do
                // nosso cadastro — o lado da ficha é o nosso, não o da FIFA).
                string? titular = null;
                if (time.TryGetProperty("Players", out var jogadoras) && jogadoras.ValueKind == JsonValueKind.Array)
                    titular = jogadoras.EnumerateArray()
                        .Select(p => FifaService.Texto(p, "IdPlayer"))
                        .FirstOrDefault(id => id != null && fichas.TryGetValue(id, out var f) && f.Goleira && f.Titular);
                if (titular == null) continue;

                resultado[titular] = sofridos;
                if (sofridos == 0) continue;

                // Quem estava no gol em cada momento: a titular até sair, depois quem
                // entrou no lugar dela (e assim por diante).
                var trocas = new List<(double Minuto, string Entrou, string Saiu)>();
                if (time.TryGetProperty("Substitutions", out var subs) && subs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in subs.EnumerateArray())
                    {
                        var saiu = FifaService.Texto(s, "IdPlayerOff");
                        var entrou = FifaService.Texto(s, "IdPlayerOn");
                        if (saiu == null || entrou == null) continue;

                        // Minuto em branco com Period 4 é troca no intervalo.
                        var minuto = MinutoOrdenavel(FifaService.Texto(s, "Minute"))
                                     ?? (FifaService.Numero(s, "Period") == 4 ? 45.5 : null);
                        if (minuto == null) continue;

                        trocas.Add((minuto.Value, entrou, saiu));
                    }
                }

                var atual = titular;
                var linhaDoTempo = new List<(double Desde, string Goleira)> { (0, titular) };
                foreach (var t in trocas.OrderBy(t => t.Minuto))
                {
                    if (t.Saiu != atual) continue;
                    atual = t.Entrou;
                    linhaDoTempo.Add((t.Minuto, atual));
                }

                if (linhaDoTempo.Count == 1) continue;

                // Gols a favor do adversário: os da lista dele. Gol contra fica na lista
                // do time beneficiado, então a lista resolve o lado nos dois casos.
                var porGoleira = new Dictionary<string, int>(StringComparer.Ordinal);
                var contados = 0;
                if (adversario.TryGetProperty("Goals", out var gols) && gols.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in gols.EnumerateArray())
                    {
                        var minuto = MinutoOrdenavel(FifaService.Texto(g, "Minute"));
                        if (minuto == null) continue;

                        var goleira = linhaDoTempo.Last(l => l.Desde < minuto.Value).Goleira;
                        porGoleira[goleira] = porGoleira.GetValueOrDefault(goleira) + 1;
                        contados++;
                    }
                }

                if (contados != sofridos) continue;

                resultado[titular] = 0;
                foreach (var (id, n) in porGoleira) resultado[id] = n;
            }

            return resultado;
        }

        /// <summary>"45'+3'" → 45.03, "71'" → 71. Null quando não há minuto.</summary>
        private static double? MinutoOrdenavel(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var numeros = Regex.Matches(texto, @"\d+").Select(m => int.Parse(m.Value)).ToList();
            if (numeros.Count == 0) return null;

            return numeros[0] + (numeros.Count > 1 ? numeros[1] / 100.0 : 0);
        }
    }
}
