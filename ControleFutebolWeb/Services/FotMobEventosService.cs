using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Lê do matchDetails do FotMob os lances da partida (gols, assistências, cartões,
    /// substituições e pênaltis perdidos), o placar e o relógio.
    ///
    /// Existe por causa dos jogos em que a api-football fica parada durante a partida:
    /// no Botafogo x Grêmio de 16/09/2026 ela devolveu a escalação e as estatísticas,
    /// mas o status continuou "NS" e a lista de eventos vazia até depois do apito final
    /// — o analista acompanhou o jogo inteiro sem um gol, cartão ou substituição na
    /// tela, enquanto o FotMob já tinha tudo.
    ///
    /// Este serviço NÃO grava nada: devolve as entidades montadas e quem decide o que
    /// entra é o ComplementoFotMobService, que compara com o que já está no banco.
    /// </summary>
    public class FotMobEventosService
    {
        // ── Leitura bruta (sem banco) ─────────────────────────────────────────

        public enum TipoLance { Gol, PenaltiPerdido, Cartao, Substituicao }

        /// <summary>
        /// Um lance como o FotMob publica, antes de casar jogador.
        /// Na substituição, Jogador é quem ENTROU e Outro é quem SAIU; no gol, Outro é
        /// quem deu a assistência.
        /// </summary>
        public record LanceBruto(
            TipoLance Tipo, int Minuto, bool IsHome,
            long IdJogador, string? NomeJogador,
            long IdOutro = 0, string? NomeOutro = null,
            bool Contra = false, string? Cartao = null);

        /// <summary>Quem é quem na escalação do FotMob: lado, nome e camisa.</summary>
        public record AtletaFotMob(long Id, string Nome, int? Camisa, bool EhCasa);

        /// <summary>
        /// Situação da partida. Codigo usa o vocabulário da api-football ("1H", "HT",
        /// "2H", "ET", "P", "FT", "AET", "PEN") porque é o que Jogo.StatusParcial guarda
        /// e o card de Jogos/Hoje interpreta.
        /// </summary>
        public record SituacaoPartida(
            bool Iniciada, bool Finalizada, string? Codigo, int? Minuto, int? Acrescimo,
            int? PlacarCasa, int? PlacarVisitante,
            int? PenaltisCasa = null, int? PenaltisVisitante = null);

        /// <summary>Uma cobrança da disputa de pênaltis, na ordem em que foi batida.</summary>
        public record CobrancaDisputa(int Ordem, bool IsHome, long IdJogador, string? NomeJogador, bool Convertido);

        internal static List<LanceBruto> LerLances(JsonElement raiz)
        {
            var lances = new List<LanceBruto>();

            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("matchFacts", out var fatos) ||
                fatos.ValueKind != JsonValueKind.Object ||
                !fatos.TryGetProperty("events", out var bloco) ||
                bloco.ValueKind != JsonValueKind.Object ||
                !bloco.TryGetProperty("events", out var eventos) ||
                eventos.ValueKind != JsonValueKind.Array) return lances;

            foreach (var e in eventos.EnumerateArray())
            {
                var tipo = Texto(e, "type");

                // Marcações de tempo (Half, AddedTime) e revisão de VAR vêm sem isHome ou
                // sem jogador e não são lance para gravar. Gol anulado pelo VAR não
                // aparece como Goal — some da lista —, então não há o que desfazer aqui.
                if (!e.TryGetProperty("isHome", out var casa) ||
                    casa.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) continue;
                var isHome = casa.GetBoolean();

                // Minuto sem o acréscimo, a mesma convenção da api-football (Elapsed sem
                // Extra) que Gol.Minuto e companhia seguem.
                var minuto = e.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Number
                    ? t.GetInt32() : 0;

                // Cobrança da disputa de pênaltis: tem modelo próprio (PenaltiDisputa)
                // e somaria gol de disputa ao placar do tempo normal.
                if (e.TryGetProperty("isPenaltyShootoutEvent", out var disputa) &&
                    disputa.ValueKind == JsonValueKind.True) continue;

                var (idJogador, nomeJogador) = Jogador(e);

                switch (tipo)
                {
                    case "Goal":
                    {
                        if (idJogador == 0 && nomeJogador == null) break;

                        var contra = e.TryGetProperty("ownGoal", out var og) && og.ValueKind == JsonValueKind.True;
                        var idAssist = e.TryGetProperty("assistPlayerId", out var ap) && ap.ValueKind == JsonValueKind.Number
                            ? ap.GetInt64() : 0;
                        var nomeAssist = Texto(e, "assistInput");

                        lances.Add(new LanceBruto(TipoLance.Gol, minuto, isHome, idJogador, nomeJogador,
                            contra ? 0 : idAssist, contra ? null : nomeAssist, contra));
                        break;
                    }

                    case "MissedPenalty":
                        if (idJogador == 0 && nomeJogador == null) break;
                        lances.Add(new LanceBruto(TipoLance.PenaltiPerdido, minuto, isHome, idJogador, nomeJogador));
                        break;

                    case "Card":
                    {
                        var cor = Texto(e, "card");
                        if (cor == null || (idJogador == 0 && nomeJogador == null)) break;
                        lances.Add(new LanceBruto(TipoLance.Cartao, minuto, isHome, idJogador, nomeJogador, Cartao: cor));
                        break;
                    }

                    case "Substitution":
                    {
                        // swap[0] entrou, swap[1] saiu — conferido contra a escalação: o
                        // primeiro está sempre em "subs" e o segundo em "starters" (ou é
                        // um reserva que já tinha entrado).
                        if (!e.TryGetProperty("swap", out var swap) ||
                            swap.ValueKind != JsonValueKind.Array || swap.GetArrayLength() < 2) break;

                        var (idEntrou, nomeEntrou) = IdNome(swap[0]);
                        var (idSaiu, nomeSaiu) = IdNome(swap[1]);
                        if (idSaiu == 0 && nomeSaiu == null) break;

                        lances.Add(new LanceBruto(TipoLance.Substituicao, minuto, isHome,
                            idEntrou, nomeEntrou, idSaiu, nomeSaiu));
                        break;
                    }
                }
            }

            return lances;
        }

        /// <summary>
        /// Cobranças da disputa de pênaltis. Elas NÃO estão em events.events (lá só há
        /// um marcador "PenaltyShootout" com o placar): vêm à parte, em
        /// matchFacts.events.penaltyShootoutEvents, já na ordem das cobranças. Goal é
        /// convertida; MissedPenalty é perdida ou defendida.
        /// </summary>
        internal static List<CobrancaDisputa> LerCobrancasDisputa(JsonElement raiz)
        {
            var cobrancas = new List<CobrancaDisputa>();

            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("matchFacts", out var fatos) ||
                fatos.ValueKind != JsonValueKind.Object ||
                !fatos.TryGetProperty("events", out var bloco) ||
                bloco.ValueKind != JsonValueKind.Object ||
                !bloco.TryGetProperty("penaltyShootoutEvents", out var eventos) ||
                eventos.ValueKind != JsonValueKind.Array) return cobrancas;

            foreach (var e in eventos.EnumerateArray())
            {
                var tipo = Texto(e, "type");
                if (tipo is not ("Goal" or "MissedPenalty")) continue;
                if (!e.TryGetProperty("isHome", out var casa) ||
                    casa.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) continue;

                var (id, nome) = Jogador(e);
                if (id == 0 && nome == null) continue;

                cobrancas.Add(new CobrancaDisputa(cobrancas.Count + 1, casa.GetBoolean(), id, nome, tipo == "Goal"));
            }

            return cobrancas;
        }

        /// <summary>
        /// Id FotMob -> atleta, dos titulares e reservas dos dois lados. Só entra o lado
        /// cujo nome confere com o nosso cadastro: com os times invertidos, todo gol
        /// iria para o lado errado.
        /// </summary>
        internal static Dictionary<long, AtletaFotMob> LerEscalacao(JsonElement raiz, string? nomeCasa, string? nomeVisitante)
        {
            var mapa = new Dictionary<long, AtletaFotMob>();

            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("lineup", out var escalacao) ||
                escalacao.ValueKind != JsonValueKind.Object) return mapa;

            foreach (var (propriedade, ehCasa) in new[] { ("homeTeam", true), ("awayTeam", false) })
            {
                if (!escalacao.TryGetProperty(propriedade, out var time)) continue;
                if (!TimeNomeMatcher.SaoMesmoTime(ehCasa ? nomeCasa : nomeVisitante, Texto(time, "name"))) continue;

                foreach (var grupo in new[] { "starters", "subs" })
                {
                    if (!time.TryGetProperty(grupo, out var jogadores) ||
                        jogadores.ValueKind != JsonValueKind.Array) continue;

                    foreach (var j in jogadores.EnumerateArray())
                    {
                        var (id, nome) = IdNome(j);
                        if (id == 0 || nome == null) continue;

                        int? camisa = j.TryGetProperty("shirtNumber", out var c)
                            ? c.ValueKind switch
                            {
                                JsonValueKind.String => int.TryParse(c.GetString(), out var i) ? i : null,
                                JsonValueKind.Number => c.GetInt32(),
                                _ => null,
                            }
                            : null;

                        mapa[id] = new AtletaFotMob(id, nome, camisa, ehCasa);
                    }
                }
            }

            return mapa;
        }

        /// <summary>
        /// Placar e relógio. O placar sai de header.teams, que é o que o site mostra no
        /// topo da página, e só é devolvido quando o mandante do FotMob confere com o
        /// nosso — placar invertido é pior do que placar nenhum.
        /// </summary>
        internal static SituacaoPartida LerSituacao(JsonElement raiz, string? nomeCasa, string? nomeVisitante)
        {
            if (!raiz.TryGetProperty("header", out var cabecalho) ||
                !cabecalho.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.Object)
                return new SituacaoPartida(false, false, null, null, null, null, null);

            var iniciada = Bool(status, "started");
            var finalizada = Bool(status, "finished");
            var cancelada = Bool(status, "cancelled");

            int? placarCasa = null, placarVis = null;
            if (cabecalho.TryGetProperty("teams", out var times) &&
                times.ValueKind == JsonValueKind.Array && times.GetArrayLength() >= 2 &&
                TimeNomeMatcher.SaoMesmoTime(nomeCasa, Texto(times[0], "name")) &&
                TimeNomeMatcher.SaoMesmoTime(nomeVisitante, Texto(times[1], "name")))
            {
                placarCasa = Inteiro(times[0], "score");
                placarVis = Inteiro(times[1], "score");
            }

            if (cancelada || !iniciada)
                return new SituacaoPartida(false, false, null, null, null, placarCasa, placarVis);

            if (finalizada)
            {
                var motivo = status.TryGetProperty("reason", out var r) ? Texto(r, "short") : null;
                var codigo = motivo?.ToUpperInvariant() switch
                {
                    "AET" => "AET",
                    "PEN" => "PEN",
                    _ => "FT",
                };
                // Decisão por pênaltis: o placar da disputa vem em reason.penalties
                // ([casa, visitante]); header.teams.score é só o do tempo de jogo.
                int? penCasa = null, penVis = null;
                if (codigo == "PEN" && placarCasa != null &&
                    status.TryGetProperty("reason", out var rp) &&
                    rp.TryGetProperty("penalties", out var pens) &&
                    pens.ValueKind == JsonValueKind.Array && pens.GetArrayLength() >= 2 &&
                    pens[0].ValueKind == JsonValueKind.Number && pens[1].ValueKind == JsonValueKind.Number)
                {
                    penCasa = pens[0].GetInt32();
                    penVis = pens[1].GetInt32();
                }

                return new SituacaoPartida(true, true, codigo, null, null, placarCasa, placarVis, penCasa, penVis);
            }

            // Em andamento: liveTime.short é o minuto como o site mostra ("78’", "45+2’",
            // "HT"), com caracteres de direção de texto no meio; maxTime diz o período
            // (45 = 1º tempo, 90 = 2º, 105/120 = prorrogação).
            string? codigoVivo = null;
            int? minuto = null, acrescimo = null;

            if (status.TryGetProperty("liveTime", out var vivo) && vivo.ValueKind == JsonValueKind.Object)
            {
                var curto = Texto(vivo, "short") ?? "";
                var maximo = Inteiro(vivo, "maxTime");
                var penaltis = vivo.TryGetProperty("penalties", out var p) && p.ValueKind != JsonValueKind.Null;

                if (curto.Contains("HT", StringComparison.OrdinalIgnoreCase)) codigoVivo = "HT";
                else if (penaltis || curto.Contains("Pen", StringComparison.OrdinalIgnoreCase)) codigoVivo = "P";
                else
                {
                    codigoVivo = maximo switch
                    {
                        null => "LIVE",
                        <= 45 => "1H",
                        <= 90 => "2H",
                        _ => "ET",
                    };

                    // Fica só com dígitos e "+": o texto traz apóstrofo e marcas
                    // invisíveis de direção (U+200E) entre os números.
                    var m = Regex.Match(Regex.Replace(curto, @"[^\d+]", ""), @"^(\d+)(?:\+(\d+))?");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var min))
                    {
                        minuto = min;
                        if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var extra))
                            acrescimo = extra;
                        else if (maximo is int teto && min > teto)
                        {
                            minuto = teto;
                            acrescimo = min - teto;
                        }
                    }
                }
            }

            return new SituacaoPartida(true, false, codigoVivo ?? "LIVE", minuto, acrescimo, placarCasa, placarVis);
        }

        // ── Casamento com o nosso cadastro ────────────────────────────────────

        /// <summary>Lances do FotMob já convertidos nas nossas entidades (não anexadas ao contexto).</summary>
        public class LancesMontados
        {
            public List<Gol> Gols { get; } = new();
            public List<Assistencia> Assistencias { get; } = new();
            public List<Cartao> Cartoes { get; } = new();
            public List<Substituicao> Substituicoes { get; } = new();
            public List<PenaltiPerdido> PenaltisPerdidos { get; } = new();
            public List<PenaltiDisputa> PenaltisDisputa { get; } = new();
            public List<string> NaoResolvidos { get; } = new();
            public Dictionary<int, long> Vinculos { get; } = new();
        }

        /// <summary>
        /// Monta as entidades dos lances casando cada jogador do FotMob com o nosso.
        ///
        /// Ordem do casamento, do mais seguro para o menos: Jogador.IdFotMob já
        /// vinculado; nome + camisa entre os ESCALADOS do jogo; nome + camisa no elenco
        /// do time. A camisa vem da escalação do FotMob — o evento não a traz — e é o que
        /// separa homônimos no mesmo elenco (o Botafogo daquele jogo tinha dois Danilos,
        /// um titular que marcou dois gols e um reserva que entrou no intervalo).
        ///
        /// Não cadastra ninguém: nome que não casa fica de fora e é contado em
        /// NaoResolvidos, como no EspnEventosService.
        /// </summary>
        public async Task<LancesMontados> MontarAsync(
            FutebolContext context, Jogo jogo, JsonElement raiz, CancellationToken ct = default)
        {
            var resultado = new LancesMontados();
            var brutos = LerLances(raiz);
            var cobrancas = LerCobrancasDisputa(raiz);
            if (brutos.Count == 0 && cobrancas.Count == 0) return resultado;

            var atletas = LerEscalacao(raiz, jogo.TimeCasa?.Nome, jogo.TimeVisitante?.Nome);

            var escalados = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.UsuarioId == null && e.JogadorId != null)
                .Select(e => new { e.IsTimeCasa, e.Jogador })
                .ToListAsync(ct);

            var escaladosPorLado = new Dictionary<bool, List<Jogador>>
            {
                [true] = escalados.Where(e => e.IsTimeCasa).Select(e => e.Jogador!).DistinctBy(j => j.Id).ToList(),
                [false] = escalados.Where(e => !e.IsTimeCasa).Select(e => e.Jogador!).DistinctBy(j => j.Id).ToList(),
            };

            var elencoPorLado = new Dictionary<bool, List<Jogador>>
            {
                [true] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeCasaId, ct),
                [false] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeVisitanteId, ct),
            };

            Jogador? Resolver(long idFotMob, string? nome, bool ladoPadrao)
            {
                var atleta = idFotMob != 0 ? atletas.GetValueOrDefault(idFotMob) : null;
                var ehCasa = atleta?.EhCasa ?? ladoPadrao;
                nome = atleta?.Nome ?? nome;

                // Nome E camisa batendo entre os escalados é o casamento mais forte que
                // existe, e vem antes até do IdFotMob: com homônimos no elenco o
                // desempate por nome escolhia o "Danilo" de nome idêntico (o reserva,
                // camisa 90) no lugar do "Danilo Santos" camisa 8 que marcou — e
                // gravava esse vínculo errado, que passava a ganhar de tudo.
                var forte = !string.IsNullOrWhiteSpace(nome)
                    ? EspnEscalacaoService.CasarPorNomeECamisa(escaladosPorLado[ehCasa], nome, atleta?.Camisa) : null;
                if (forte != null)
                {
                    if (idFotMob != 0 && forte.IdFotMob == null) resultado.Vinculos[forte.Id] = idFotMob;
                    return forte;
                }

                if (idFotMob != 0)
                {
                    var vinculado = escaladosPorLado[ehCasa].FirstOrDefault(j => j.IdFotMob == idFotMob)
                                    ?? elencoPorLado[ehCasa].FirstOrDefault(j => j.IdFotMob == idFotMob);
                    if (vinculado != null) return vinculado;
                }

                if (string.IsNullOrWhiteSpace(nome)) return null;

                // Daqui para baixo o casamento é só por nome (ou só por camisa) e não
                // vira vínculo: é palpite bom o bastante para um lance, não para fixar
                // a identidade do jogador.
                var achado = EspnEscalacaoService.Casar(escaladosPorLado[ehCasa], nome, atleta?.Camisa)?.Jogador;
                if (achado != null) return achado;

                achado = EspnEscalacaoService.Casar(elencoPorLado[ehCasa], nome, atleta?.Camisa)?.Jogador;
                if (achado == null) resultado.NaoResolvidos.Add(nome);
                return achado;
            }

            foreach (var l in brutos)
            {
                switch (l.Tipo)
                {
                    case TipoLance.Gol:
                    {
                        // Gol contra: o autor é do outro lado. O lado real sai da
                        // escalação quando o FotMob conhece o jogador; isHome só serve
                        // de palpite para quem não está lá.
                        var autor = Resolver(l.IdJogador, l.NomeJogador, l.Contra ? !l.IsHome : l.IsHome);
                        if (autor == null) break;

                        resultado.Gols.Add(new Gol { JogoId = jogo.Id, JogadorId = autor.Id, Minuto = l.Minuto, Contra = l.Contra });

                        if (!l.Contra && (l.IdOutro != 0 || l.NomeOutro != null))
                        {
                            var assistente = Resolver(l.IdOutro, l.NomeOutro, l.IsHome);
                            if (assistente != null && assistente.Id != autor.Id)
                                resultado.Assistencias.Add(new Assistencia
                                {
                                    JogoId = jogo.Id, JogadorId = assistente.Id, Minuto = l.Minuto,
                                });
                        }
                        break;
                    }

                    case TipoLance.PenaltiPerdido:
                    {
                        var cobrador = Resolver(l.IdJogador, l.NomeJogador, l.IsHome);
                        if (cobrador == null) break;
                        resultado.PenaltisPerdidos.Add(new PenaltiPerdido
                        {
                            JogoId = jogo.Id, JogadorId = cobrador.Id, Minuto = l.Minuto, IsTimeCasa = l.IsHome,
                        });
                        break;
                    }

                    case TipoLance.Cartao:
                    {
                        var punido = Resolver(l.IdJogador, l.NomeJogador, l.IsHome);
                        if (punido == null) break;

                        // "YellowRed" é o segundo amarelo: expulsão, e é assim que a
                        // importação da api-football também registra.
                        var cor = l.Cartao == "Yellow" ? "Amarelo" : "Vermelho";
                        resultado.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id, JogadorId = punido.Id, Minuto = l.Minuto, Tipo = cor,
                        });
                        break;
                    }

                    case TipoLance.Substituicao:
                    {
                        var saiu = Resolver(l.IdOutro, l.NomeOutro, l.IsHome);
                        if (saiu == null) break;
                        var entrou = l.IdJogador != 0 || l.NomeJogador != null
                            ? Resolver(l.IdJogador, l.NomeJogador, l.IsHome) : null;

                        // Quem entrou fica null quando não resolve — nunca repetir quem
                        // saiu, que quebraria as setas ↑/↓ da tela de análise.
                        resultado.Substituicoes.Add(new Substituicao
                        {
                            JogoId = jogo.Id,
                            JogadorEntrouId = entrou?.Id == saiu.Id ? null : entrou?.Id,
                            JogadorSaiuId = saiu.Id,
                            Minuto = l.Minuto,
                            IsTimeCasa = l.IsHome,
                        });
                        break;
                    }
                }
            }

            foreach (var c in cobrancas)
            {
                // Cobrança sem batedor identificado fica de fora. O placar da disputa
                // (Jogo.PenaltisCasa) vem à parte e não depende disto.
                var batedor = Resolver(c.IdJogador, c.NomeJogador, c.IsHome);
                if (batedor == null) continue;

                resultado.PenaltisDisputa.Add(new PenaltiDisputa
                {
                    JogoId = jogo.Id, JogadorId = batedor.Id, IsTimeCasa = c.IsHome,
                    Convertido = c.Convertido, Ordem = c.Ordem,
                });
            }

            return resultado;
        }

        // ── Utilitários de leitura ────────────────────────────────────────────

        private static (long Id, string? Nome) Jogador(JsonElement e)
        {
            var id = e.TryGetProperty("playerId", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0;
            string? nome = null;

            if (e.TryGetProperty("player", out var jogador) && jogador.ValueKind == JsonValueKind.Object)
            {
                if (id == 0) id = IdNome(jogador).Id;
                nome = Texto(jogador, "name");
            }

            return (id, nome ?? Texto(e, "nameStr"));
        }

        /// <summary>O id vem como número em player e como texto em swap e na escalação.</summary>
        private static (long Id, string? Nome) IdNome(JsonElement j)
        {
            long id = 0;
            if (j.TryGetProperty("id", out var i))
            {
                if (i.ValueKind == JsonValueKind.Number) id = i.GetInt64();
                else if (i.ValueKind == JsonValueKind.String &&
                         long.TryParse(i.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    id = parsed;
            }

            var nome = Texto(j, "name");
            return (id, string.IsNullOrWhiteSpace(nome) ? null : nome);
        }

        private static string? Texto(JsonElement e, string propriedade) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(propriedade, out var v) &&
            v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int? Inteiro(JsonElement e, string propriedade) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(propriedade, out var v) &&
            v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

        private static bool Bool(JsonElement e, string propriedade) =>
            e.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.True;
    }
}
