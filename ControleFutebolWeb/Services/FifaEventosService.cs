using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Importa os lances de uma partida (gols, assistências, cartões e substituições)
    /// da narração da FIFA (timelines).
    ///
    /// Existe pelo mesmo motivo do FifaEscalacaoService: nas competições que só a FIFA
    /// cobre, não falta só a escalação — falta o jogo inteiro. Sem isto o analista
    /// terminaria com o XI certo e o placar zerado.
    ///
    /// A timeline identifica cada participante por IdPlayer, sem nome; quem tem o
    /// de-para id → nome é o documento da partida (live/football), que este serviço
    /// abre junto e usa para casar com o elenco cadastrado. Ninguém é criado aqui —
    /// quem aparece num lance e não está no elenco é divergência para conferir, e
    /// inventar jogadora a partir de um lance criaria duplicata sem posição nem camisa.
    /// </summary>
    public class FifaEventosService
    {
        private readonly FifaService _fifa;
        private readonly ILogger<FifaEventosService> _logger;

        public FifaEventosService(FifaService fifa, ILogger<FifaEventosService> logger)
        {
            _fifa = fifa;
            _logger = logger;
        }

        public async Task<ResultadoFifa> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, idMatch, erro) = await _fifa.AbrirPartidaAsync(context, jogoId, ct);
            if (erro != null) return erro;

            using var _doc = doc!;

            // O placar final sai deste mesmo documento e é gravado antes de qualquer
            // saída: partida que a FIFA encerrou sem publicar timeline (ou cujos lances
            // não casaram com o elenco) tem de ficar com o resultado do mesmo jeito —
            // é PlacarCasa preenchido que faz o jogo contar como realizado.
            var placarGravado = FifaService.AplicarPlacarFinal(jogo!, _doc.RootElement);
            if (placarGravado) await context.SaveChangesAsync(ct);

            var referencia = FifaService.RefDaPartida(jogo!.LinkDetalhes)!;
            using var timeline = await _fifa.AbrirTimelineAsync(referencia, ct);

            if (timeline == null ||
                !timeline.RootElement.TryGetProperty("Event", out var lances) ||
                lances.ValueKind != JsonValueKind.Array || lances.GetArrayLength() == 0)
                return new ResultadoFifa(false, "A FIFA não publicou os lances desta partida.", idMatch);

            // Quem é quem: id da jogadora → nome, camisa e lado, lido da partida.
            var fichas = LerFichas(_doc.RootElement, jogo);
            if (fichas.Count == 0)
                return new ResultadoFifa(false,
                    "A FIFA publicou os lances, mas não a relação de jogadoras desta partida.", idMatch);

            var elencos = new Dictionary<bool, List<Jogador>>
            {
                [true] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeCasaId, ct),
                [false] = await EspnEscalacaoService.ElencoAsync(context, jogo.TimeVisitanteId, ct),
            };

            var naoResolvidos = new List<string>();

            Jogador? Resolver(string? idPlayer)
            {
                if (string.IsNullOrWhiteSpace(idPlayer) ||
                    !fichas.TryGetValue(idPlayer, out var ficha)) return null;

                var achado = EspnEscalacaoService.Casar(elencos[ficha.EhCasa], ficha.Nome, ficha.Camisa)?.Jogador;
                if (achado == null) naoResolvidos.Add(ficha.Nome);
                return achado;
            }

            var gols = new List<Gol>();
            var assistencias = new List<Assistencia>();
            var cartoes = new List<Cartao>();
            var substituicoes = new List<Substituicao>();
            var penaltisPerdidos = new List<PenaltiPerdido>();
            var disputa = new List<PenaltiDisputa>();

            foreach (var lance in lances.EnumerateArray())
            {
                var tipo = FifaService.Numero(lance, "Type");
                var rotulo = FifaService.Localizado(lance, "TypeLocalized") ?? "";
                var minuto = Minuto(lance);
                var idPlayer = FifaService.Texto(lance, "IdPlayer");
                var idSubPlayer = FifaService.Texto(lance, "IdSubPlayer");

                // Disputa de pênaltis: a FIFA narra cada cobrança com os MESMOS códigos
                // do tempo normal (41 = "Penalty Goal", 60 = "Penalty missed"), só que
                // no período 11 e sem minuto. Sem este desvio as cobranças entravam como
                // gols no minuto 0 — o BRA×USA das oitavas do Sub-20 (1×1, 5×4 nos
                // pênaltis) ficou com 11 gols na análise. Elas vão para PenaltisDisputa,
                // que não mexe no placar nem na artilharia; o placar da disputa já sai
                // de AplicarPlacarFinal.
                if (FifaService.Numero(lance, "Period") == PeriodoDisputaPenaltis)
                {
                    var convertido = tipo == TipoGolPenalti;
                    var desperdicado = !convertido &&
                                       rotulo.Contains("penalty", StringComparison.OrdinalIgnoreCase);
                    if (!convertido && !desperdicado) continue;

                    var cobradora = Resolver(idPlayer);
                    if (cobradora == null) continue;

                    disputa.Add(new PenaltiDisputa
                    {
                        JogoId = jogo.Id, JogadorId = cobradora.Id,
                        IsTimeCasa = fichas[idPlayer!].EhCasa,
                        Convertido = convertido,
                        // A timeline vem em ordem cronológica, que é a ordem das cobranças.
                        Ordem = disputa.Count + 1,
                    });
                    continue;
                }

                // Gol contra: a FIFA descreve o lance com a jogadora que marcou, então o
                // lado sai da ficha dela — não há o problema da ESPN, que aponta o time
                // beneficiado. O que muda aqui é só a marca Contra.
                var contra = tipo == TipoGolContra ||
                             rotulo.Contains("own goal", StringComparison.OrdinalIgnoreCase);

                if (tipo == TipoGol || tipo == TipoGolPenalti || contra)
                {
                    var autor = Resolver(idPlayer);
                    if (autor == null) continue;

                    gols.Add(new Gol
                    {
                        JogoId = jogo.Id, JogadorId = autor.Id, Minuto = minuto, Contra = contra,
                    });

                    // IdSubPlayer no gol é quem deu o passe. Não vem em gol de falta,
                    // pênalti nem gol contra.
                    if (!contra && tipo != TipoGolPenalti)
                    {
                        var assistente = Resolver(idSubPlayer);
                        if (assistente != null && assistente.Id != autor.Id)
                            assistencias.Add(new Assistencia
                            {
                                JogoId = jogo.Id, JogadorId = assistente.Id, Minuto = minuto,
                            });
                    }

                    continue;
                }

                if (rotulo.Contains("card", StringComparison.OrdinalIgnoreCase))
                {
                    var punida = Resolver(idPlayer);
                    if (punida == null) continue;

                    // "Second yellow card" é expulsão: vermelho ganha do amarelo na
                    // ordem do teste, como na leitura da ESPN.
                    var cor = rotulo.Contains("red", StringComparison.OrdinalIgnoreCase) ||
                              rotulo.Contains("second", StringComparison.OrdinalIgnoreCase)
                        ? "Vermelho" : "Amarelo";

                    cartoes.Add(new Cartao
                    {
                        JogoId = jogo.Id, JogadorId = punida.Id, Minuto = minuto, Tipo = cor,
                    });
                    continue;
                }

                if (tipo == TipoSubstituicao)
                {
                    // Na FIFA, IdPlayer é quem ENTRA e IdSubPlayer quem SAI (confere com
                    // o EventDescription: "X (in) comes off the bench to replace Y (out)").
                    var entrou = Resolver(idPlayer);
                    var saiu = Resolver(idSubPlayer);
                    if (saiu == null) continue;

                    var ficha = fichas[idSubPlayer!];

                    substituicoes.Add(new Substituicao
                    {
                        JogoId = jogo.Id,
                        // Quem entrou fica null quando não resolve — nunca repetir quem
                        // saiu, que quebraria as setas ↑/↓ da tela de análise.
                        JogadorEntrouId = entrou?.Id == saiu.Id ? null : entrou?.Id,
                        JogadorSaiuId = saiu.Id,
                        Minuto = minuto,
                        IsTimeCasa = ficha.EhCasa,
                    });
                    continue;
                }

                // Pênalti perdido ou defendido (o convertido já entrou como gol acima).
                if (rotulo.Contains("penalty", StringComparison.OrdinalIgnoreCase) &&
                    (rotulo.Contains("miss", StringComparison.OrdinalIgnoreCase) ||
                     rotulo.Contains("saved", StringComparison.OrdinalIgnoreCase)))
                {
                    var cobradora = Resolver(idPlayer);
                    if (cobradora == null) continue;

                    penaltisPerdidos.Add(new PenaltiPerdido
                    {
                        JogoId = jogo.Id, JogadorId = cobradora.Id,
                        Minuto = minuto, IsTimeCasa = fichas[idPlayer!].EhCasa,
                    });
                }
            }

            var total = gols.Count + cartoes.Count + substituicoes.Count + penaltisPerdidos.Count + disputa.Count;
            if (total == 0)
                return new ResultadoFifa(placarGravado,
                    placarGravado
                        ? $"Placar final da FIFA gravado ({jogo.PlacarCasa}×{jogo.PlacarVisitante}), "
                          + "mas nenhum lance casou com o elenco cadastrado."
                        : "A FIFA tem a partida, mas nenhum lance dela casou com o elenco cadastrado.",
                    idMatch);

            // Troca completa, como fazem a reimportação da api-football e a da ESPN: o
            // que existia era digitado à mão ou de uma importação anterior, e misturar
            // duplicaria gol.
            context.Gols.RemoveRange(await context.Gols.Where(g => g.JogoId == jogo.Id).ToListAsync(ct));
            context.Assistencias.RemoveRange(await context.Assistencias.Where(a => a.JogoId == jogo.Id).ToListAsync(ct));
            context.Cartoes.RemoveRange(await context.Cartoes.Where(c => c.JogoId == jogo.Id).ToListAsync(ct));
            context.Substituicoes.RemoveRange(await context.Substituicoes.Where(s => s.JogoId == jogo.Id).ToListAsync(ct));
            context.PenaltisPerdidos.RemoveRange(await context.PenaltisPerdidos.Where(p => p.JogoId == jogo.Id).ToListAsync(ct));
            context.PenaltisDisputa.RemoveRange(await context.PenaltisDisputa.Where(p => p.JogoId == jogo.Id).ToListAsync(ct));

            context.Gols.AddRange(gols);
            context.Assistencias.AddRange(assistencias);
            context.Cartoes.AddRange(cartoes);
            context.Substituicoes.AddRange(substituicoes);
            context.PenaltisPerdidos.AddRange(penaltisPerdidos);
            context.PenaltisDisputa.AddRange(disputa);
            await context.SaveChangesAsync(ct);

            if (naoResolvidos.Count > 0)
                _logger.LogInformation(
                    "[FifaEventos] Jogo {Id}: {N} nome(s) sem correspondência no elenco: {Nomes}",
                    jogo.Id, naoResolvidos.Count, string.Join(", ", naoResolvidos.Distinct()));

            var partes = new List<string>();
            if (gols.Count > 0) partes.Add($"{gols.Count} gol(s)");
            if (assistencias.Count > 0) partes.Add($"{assistencias.Count} assistência(s)");
            if (cartoes.Count > 0) partes.Add($"{cartoes.Count} cartão(ões)");
            if (substituicoes.Count > 0) partes.Add($"{substituicoes.Count} substituição(ões)");
            if (penaltisPerdidos.Count > 0) partes.Add($"{penaltisPerdidos.Count} pênalti(s) perdido(s)");
            if (disputa.Count > 0)
                partes.Add($"{disputa.Count} cobrança(s) da disputa de pênaltis ({disputa.Count(d => d.Convertido)} convertida(s))");

            var msg = $"Lances importados da FIFA: {string.Join(", ", partes)}.";
            if (placarGravado)
                msg += $" Placar final: {jogo.PlacarCasa}×{jogo.PlacarVisitante}.";
            if (naoResolvidos.Count > 0)
                msg += $" {naoResolvidos.Distinct().Count()} nome(s) não casaram com o elenco e ficaram de fora.";

            return new ResultadoFifa(true, msg, idMatch);
        }

        // Tipos de evento da FIFA usados aqui. Os demais (falta, escanteio, impedimento,
        // finalização, marcações de tempo) não têm equivalente no nosso modelo e são
        // ignorados; cartão e pênalti saem do rótulo, que é mais estável que o código.
        private const int TipoGol = 0;
        private const int TipoSubstituicao = 5;

        // Pênalti convertido e gol contra têm código PRÓPRIO — não entram como Type 0.
        // Ficaram de fora na primeira versão, e o efeito era o placar do jogo sair
        // errado: o gol de pênalti da Tanzânia contra o Brasil (Sub-20 Feminino, 39')
        // simplesmente não era importado, e o time terminava a partida sem nada.
        // "Penalty Awarded" (6) é a marcação da penalidade, não o gol, e continua fora.
        private const int TipoGolContra = 34;
        private const int TipoGolPenalti = 41;

        // Período da disputa de pênaltis na timeline (3 e 5 são os tempos normais,
        // 7 e 9 a prorrogação, 10 o fim de jogo).
        internal const int PeriodoDisputaPenaltis = 11;

        private record Ficha(string Nome, int? Camisa, bool EhCasa);

        /// <summary>
        /// De-para id → nome/camisa/lado, montado da relação de jogadoras da partida.
        /// É o que permite resolver os lances, que só trazem IdPlayer.
        /// </summary>
        private static Dictionary<string, Ficha> LerFichas(JsonElement raiz, Jogo jogo)
        {
            var fichas = new Dictionary<string, Ficha>(StringComparer.Ordinal);

            foreach (var propriedade in new[] { "HomeTeam", "AwayTeam" })
            {
                if (!raiz.TryGetProperty(propriedade, out var time) ||
                    !time.TryGetProperty("Players", out var jogadoras) ||
                    jogadoras.ValueKind != JsonValueKind.Array) continue;

                // De que lado do NOSSO jogo este time está — conferido pelo IdTeam da
                // FIFA, não pela posição no documento. Um lado que não casa com nenhum
                // dos dois fica de fora: sem saber de quem é o gol, gravá-lo poria o
                // lance no time errado.
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
                        ehCasa);
                }
            }

            return fichas;
        }

        /// <summary>
        /// Minuto do lance. A FIFA manda "48'" ou "45'+3'"; fica só a parte antes do
        /// acréscimo, mesma convenção da api-football (Time.Elapsed, sem Extra) e o que
        /// o resto do sistema espera em Gol.Minuto e companhia.
        /// </summary>
        private static int Minuto(JsonElement lance)
        {
            var texto = FifaService.Texto(lance, "MatchMinute") ?? "";
            var digitos = Regex.Match(texto, @"\d+").Value;
            return int.TryParse(digitos, out var m) ? m : 0;
        }
    }
}
