using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Importa os lances de uma partida (gols, assistências, cartões, substituições e
    /// pênaltis perdidos) do resumo da ESPN.
    ///
    /// Existe pelo mesmo motivo do EspnEscalacaoService: quando a api-football devolve
    /// a partida vazia, não falta só a escalação — falta o jogo inteiro. Antes disso o
    /// analista importava a escalação da ESPN e ficava com um jogo sem um gol sequer,
    /// tendo de digitar tudo na mão.
    ///
    /// A ESPN publica isso em keyEvents, junto com o minuto e os atletas envolvidos.
    /// O que ela NÃO tem é a disputa de pênaltis separada dos gols do tempo normal
    /// (vem marcada com shootout=true e é ignorada aqui — PenaltiDisputa depende de
    /// ordem de cobrança, que exigiria outra leitura).
    /// </summary>
    public class EspnEventosService
    {
        private readonly EspnEstatisticasService _espn;
        private readonly ILogger<EspnEventosService> _logger;

        public EspnEventosService(EspnEstatisticasService espn, ILogger<EspnEventosService> logger)
        {
            _espn = espn;
            _logger = logger;
        }

        public async Task<ResultadoEspn> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, evento, erro) = await _espn.AbrirResumoAsync(context, jogoId, exigeIdApi: false, ct);
            if (erro != null) return erro;

            using var _doc = doc!;
            var raiz = _doc.RootElement;

            if (!raiz.TryGetProperty("keyEvents", out var lances) || lances.GetArrayLength() == 0)
                return new ResultadoEspn(false, "A ESPN não publicou os lances desta partida.", evento);

            var ladoPorTimeEspn = MapearLados(raiz, jogo!);
            if (ladoPorTimeEspn.Count == 0)
                return new ResultadoEspn(false, "Não foi possível casar os times da ESPN com os do jogo.", evento);

            var elencos = new Dictionary<bool, List<Jogador>>
            {
                [true] = await EspnEscalacaoService.ElencoAsync(context, jogo!.TimeCasaId, ct),
                [false] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeVisitanteId, ct),
            };

            // Resolve pelo nome apenas: keyEvents não traz número de camisa. Não cadastra
            // ninguém — quem aparece num lance e não está no elenco é divergência de nome
            // para conferir, e inventar jogador a partir de um lance criaria duplicata
            // sem posição nem camisa. O contador de não resolvidos vai na mensagem.
            var naoResolvidos = new List<string>();
            Jogador? Resolver(string? nome, bool ehCasa)
            {
                if (string.IsNullOrWhiteSpace(nome)) return null;
                var achado = EspnEscalacaoService.Casar(elencos[ehCasa], nome, null)?.Jogador;
                if (achado == null) naoResolvidos.Add(nome);
                return achado;
            }

            var gols = new List<Gol>();
            var assistencias = new List<Assistencia>();
            var cartoes = new List<Cartao>();
            var substituicoes = new List<Substituicao>();
            var penaltisPerdidos = new List<PenaltiPerdido>();

            foreach (var lance in lances.EnumerateArray())
            {
                // Cobrança de disputa de pênaltis: tem modelo próprio (PenaltiDisputa),
                // que depende da ordem das cobranças. Entrar aqui somaria gol de
                // disputa ao placar do tempo normal.
                if (lance.TryGetProperty("shootout", out var so) && so.ValueKind == JsonValueKind.True)
                    continue;

                var tipo = lance.TryGetProperty("type", out var t) && t.TryGetProperty("type", out var tt)
                    ? tt.GetString() ?? "" : "";

                var idTimeEspn = lance.TryGetProperty("team", out var tm) && tm.TryGetProperty("id", out var ti)
                    ? ti.GetString() : null;

                // Lance sem time é marcação de tempo (kickoff, halftime, atraso).
                if (idTimeEspn == null || !ladoPorTimeEspn.TryGetValue(idTimeEspn, out var ehCasa)) continue;

                var minuto = MinutoDe(lance);
                var atletas = AtletasDoLance(lance);
                var texto = lance.TryGetProperty("text", out var tx) ? tx.GetString() : null;
                var foiGol = lance.TryGetProperty("scoringPlay", out var sp) && sp.ValueKind == JsonValueKind.True;

                if (foiGol && tipo == "own-goal")
                {
                    // Gol contra: a ESPN põe em "team" quem SE BENEFICIOU, ao contrário
                    // da api-football, que põe o time do autor. O autor é do outro lado.
                    //
                    // E o nome em participants nem sempre bate com o do texto (visto num
                    // Palmeiras x Cerro Porteño, onde o texto diz "Carlos Miguel" e o
                    // participante vem "Carlos Eduardo"): o texto é a fonte melhor, com
                    // o participante como reserva.
                    var autorTexto = Regex.Match(texto ?? "", @"Own Goal by\s+(.+?)\s*,").Groups[1].Value;
                    var nomeAutor = !string.IsNullOrWhiteSpace(autorTexto) ? autorTexto : atletas.FirstOrDefault();

                    var autor = Resolver(nomeAutor, !ehCasa);
                    if (autor != null)
                        gols.Add(new Gol { JogoId = jogo.Id, JogadorId = autor.Id, Minuto = minuto, Contra = true });
                    continue;
                }

                if (foiGol)
                {
                    // Cabeçada, voleio e falta são tipos distintos ("goal---header" e
                    // companhia), então o que identifica gol é scoringPlay, não o tipo.
                    var autor = Resolver(atletas.ElementAtOrDefault(0), ehCasa);
                    if (autor == null) continue;

                    gols.Add(new Gol { JogoId = jogo.Id, JogadorId = autor.Id, Minuto = minuto, Contra = false });

                    // Segundo participante é quem deu o passe, quando houve — gol de
                    // falta e de bola parada costumam vir com um só.
                    var assistente = atletas.Count > 1 ? Resolver(atletas[1], ehCasa) : null;
                    if (assistente != null && assistente.Id != autor.Id)
                        assistencias.Add(new Assistencia { JogoId = jogo.Id, JogadorId = assistente.Id, Minuto = minuto });

                    continue;
                }

                if (tipo.Contains("card", StringComparison.OrdinalIgnoreCase))
                {
                    var punido = Resolver(atletas.FirstOrDefault(), ehCasa);
                    if (punido == null) continue;

                    // "second-yellow-card" tem as duas palavras e é expulsão: vermelho
                    // ganha do amarelo na ordem do teste.
                    var cor = tipo.Contains("red", StringComparison.OrdinalIgnoreCase) ? "Vermelho" : "Amarelo";
                    cartoes.Add(new Cartao { JogoId = jogo.Id, JogadorId = punido.Id, Minuto = minuto, Tipo = cor });
                    continue;
                }

                if (tipo == "substitution")
                {
                    // Ordem da ESPN: primeiro quem entra, depois quem sai ("X replaces Y").
                    var entrou = Resolver(atletas.ElementAtOrDefault(0), ehCasa);
                    var saiu = Resolver(atletas.ElementAtOrDefault(1), ehCasa);
                    if (saiu == null) continue;

                    // Quem entrou fica null quando não resolve — nunca repetir quem saiu,
                    // que quebraria as setas ↑/↓ da tela de análise (mesmo cuidado da
                    // importação da api-football).
                    substituicoes.Add(new Substituicao
                    {
                        JogoId = jogo.Id,
                        JogadorEntrouId = entrou?.Id == saiu.Id ? null : entrou?.Id,
                        JogadorSaiuId = saiu.Id,
                        Minuto = minuto,
                        IsTimeCasa = ehCasa,
                    });
                    continue;
                }

                if (tipo.StartsWith("penalty", StringComparison.OrdinalIgnoreCase))
                {
                    // Chegou aqui porque scoringPlay é false: pênalti perdido ou defendido.
                    var cobrador = Resolver(atletas.FirstOrDefault(), ehCasa);
                    if (cobrador == null) continue;

                    penaltisPerdidos.Add(new PenaltiPerdido
                    {
                        JogoId = jogo.Id, JogadorId = cobrador.Id,
                        Minuto = minuto, IsTimeCasa = ehCasa,
                    });
                }
            }

            var total = gols.Count + cartoes.Count + substituicoes.Count + penaltisPerdidos.Count;
            if (total == 0)
                return new ResultadoEspn(false,
                    "A ESPN tem a partida, mas nenhum lance dela casou com o elenco cadastrado.", evento);

            // Troca completa, como faz a reimportação da api-football: o que existia era
            // digitado à mão ou de uma importação anterior, e misturar duplicaria gol.
            context.Gols.RemoveRange(await context.Gols.Where(g => g.JogoId == jogo.Id).ToListAsync(ct));
            context.Assistencias.RemoveRange(await context.Assistencias.Where(a => a.JogoId == jogo.Id).ToListAsync(ct));
            context.Cartoes.RemoveRange(await context.Cartoes.Where(c => c.JogoId == jogo.Id).ToListAsync(ct));
            context.Substituicoes.RemoveRange(await context.Substituicoes.Where(x => x.JogoId == jogo.Id).ToListAsync(ct));
            context.PenaltisPerdidos.RemoveRange(await context.PenaltisPerdidos.Where(p => p.JogoId == jogo.Id).ToListAsync(ct));

            context.Gols.AddRange(gols);
            context.Assistencias.AddRange(assistencias);
            context.Cartoes.AddRange(cartoes);
            context.Substituicoes.AddRange(substituicoes);
            context.PenaltisPerdidos.AddRange(penaltisPerdidos);
            await context.SaveChangesAsync(ct);

            if (naoResolvidos.Count > 0)
                _logger.LogInformation("[EspnEventos] Jogo {Id}: {N} nome(s) sem correspondência no elenco: {Nomes}",
                    jogo.Id, naoResolvidos.Count, string.Join(", ", naoResolvidos.Distinct()));

            var partes = new List<string>();
            if (gols.Count > 0) partes.Add($"{gols.Count} gol(s)");
            if (assistencias.Count > 0) partes.Add($"{assistencias.Count} assistência(s)");
            if (cartoes.Count > 0) partes.Add($"{cartoes.Count} cartão(ões)");
            if (substituicoes.Count > 0) partes.Add($"{substituicoes.Count} substituição(ões)");
            if (penaltisPerdidos.Count > 0) partes.Add($"{penaltisPerdidos.Count} pênalti(s) perdido(s)");

            var msg = $"Lances importados da ESPN: {string.Join(", ", partes)}.";
            if (naoResolvidos.Count > 0)
                msg += $" {naoResolvidos.Distinct().Count()} nome(s) não casaram com o elenco e ficaram de fora.";

            return new ResultadoEspn(true, msg, evento);
        }

        /// <summary>
        /// De-para do id de time da ESPN para "é o mandante?". Sai dos rosters, que são
        /// os mesmos ids que keyEvents usa em team.id.
        /// </summary>
        private static Dictionary<string, bool> MapearLados(JsonElement raiz, Jogo jogo)
        {
            var mapa = new Dictionary<string, bool>();
            if (!raiz.TryGetProperty("rosters", out var rosters)) return mapa;

            foreach (var roster in rosters.EnumerateArray())
            {
                if (!roster.TryGetProperty("team", out var time)) continue;

                var id = time.TryGetProperty("id", out var i) ? i.GetString() : null;
                var nome = time.TryGetProperty("displayName", out var n) ? n.GetString() : null;
                if (id == null) continue;

                if (TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nome)) mapa[id] = true;
                else if (TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nome)) mapa[id] = false;
            }

            return mapa;
        }

        /// <summary>
        /// Minuto do lance. A ESPN manda "53'" ou "45'+3'"; fica só a parte antes do
        /// acréscimo, que é a mesma convenção da api-football (Time.Elapsed, sem Extra)
        /// e o que o resto do sistema espera em Gol.Minuto e companhia.
        /// </summary>
        private static int MinutoDe(JsonElement lance)
        {
            var texto = lance.TryGetProperty("clock", out var c) && c.TryGetProperty("displayValue", out var d)
                ? d.GetString() : null;

            var digitos = Regex.Match(texto ?? "", @"\d+").Value;
            return int.TryParse(digitos, out var m) ? m : 0;
        }

        private static List<string> AtletasDoLance(JsonElement lance)
        {
            var nomes = new List<string>();
            if (!lance.TryGetProperty("participants", out var ps)) return nomes;

            foreach (var p in ps.EnumerateArray())
            {
                if (!p.TryGetProperty("athlete", out var a)) continue;
                var nome = a.TryGetProperty("displayName", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(nome)) nomes.Add(nome);
            }

            return nomes;
        }
    }
}
