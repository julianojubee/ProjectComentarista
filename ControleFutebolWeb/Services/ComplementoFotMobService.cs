using System.Globalization;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Confere o que a reimportação gravou contra o FotMob e completa com ele o que
    /// estiver faltando: gols, assistências, cartões, substituições, pênaltis perdidos,
    /// estatísticas de time e de jogador, e o placar.
    ///
    /// Motivo: a api-football às vezes fica parada durante a partida. No Botafogo x
    /// Grêmio de 16/09/2026 ela devolveu escalação e estatísticas, mas o status ficou
    /// "NS" e a lista de eventos vazia até bem depois do apito final — o analista
    /// acompanhou o jogo inteiro sem gol, cartão ou substituição, e o FotMob tinha tudo.
    ///
    /// A REGRA é "quem tem mais dado vence", categoria por categoria, e não "o FotMob
    /// vence": durante o jogo os números só crescem, então a fonte com mais gols, mais
    /// cartões ou mais finalizações é a que está mais atualizada. Empate fica com o que
    /// já está gravado — a api-football continua sendo a fonte principal (é ela que traz
    /// a nota do jogador, por exemplo) e o FotMob só entra onde ela ficou para trás.
    ///
    /// Buscar e aplicar são dois passos de propósito: o ciclo de jogos ao vivo busca o
    /// FotMob ANTES de abrir a transação da reimportação, para que a chamada HTTP (com
    /// o intervalo mínimo entre chamadas do FotMobService) não fique segurando a
    /// transação aberta.
    /// </summary>
    public class ComplementoFotMobService
    {
        private readonly FotMobService _fotmob;
        private readonly FotMobEventosService _eventos;
        private readonly ILogger<ComplementoFotMobService> _logger;

        public ComplementoFotMobService(
            FotMobService fotmob,
            FotMobEventosService eventos,
            ILogger<ComplementoFotMobService> logger)
        {
            _fotmob = fotmob;
            _eventos = eventos;
            _logger = logger;
        }

        public record Resultado(bool Consultou, bool Alterou, string Mensagem);

        /// <summary>O matchDetails já baixado, ou o motivo de não ter sido.</summary>
        public sealed class PartidaAberta : IDisposable
        {
            public JsonDocument? Documento { get; init; }
            public long? Partida { get; init; }
            public string? Falha { get; init; }

            /// <summary>Competição fora do de-para do FotMob — nem houve chamada.</summary>
            public bool SemCobertura { get; init; }

            public void Dispose() => Documento?.Dispose();
        }

        public async Task<PartidaAberta> BuscarAsync(FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var idApiLiga = await context.Jogos.AsNoTracking()
                .Where(j => j.Id == jogoId)
                .Select(j => j.Competicao!.IdApi)
                .FirstOrDefaultAsync(ct);

            // Sem liga mapeada a chamada só devolveria "sem cobertura" — e o ciclo passa
            // aqui a cada 15 min para todo jogo em andamento.
            if (!_fotmob.TemCobertura(idApiLiga))
                return new PartidaAberta { SemCobertura = true, Falha = "Competição sem correspondente no FotMob." };

            var (_, doc, partida, erro) = await _fotmob.AbrirPartidaAsync(context, jogoId, exigeIdApi: false, ct);
            return new PartidaAberta { Documento = doc, Partida = partida, Falha = erro?.Mensagem };
        }

        /// <summary>
        /// Compara e grava. Chama SaveChanges quando algo mudou — quem quiser que isso
        /// vá junto com a reimportação abre a transação por fora.
        /// </summary>
        public async Task<Resultado> AplicarAsync(
            FutebolContext context, int jogoId, PartidaAberta aberta, CancellationToken ct = default)
        {
            if (aberta.Documento == null)
                return new Resultado(false, false, aberta.Falha ?? "FotMob indisponível.");

            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);
            if (jogo == null) return new Resultado(false, false, "Jogo não encontrado.");

            var raiz = aberta.Documento.RootElement;
            var trocas = new List<string>();

            // ── Lances ──
            var lances = await _eventos.MontarAsync(context, jogo, raiz, ct);

            await TrocarSeFotMobTemMaisAsync(context.Gols,
                context.Gols.Where(g => g.JogoId == jogoId), lances.Gols, "gols", trocas, ct);
            await TrocarSeFotMobTemMaisAsync(context.Assistencias,
                context.Assistencias.Where(a => a.JogoId == jogoId), lances.Assistencias, "assistências", trocas, ct);
            await TrocarSeFotMobTemMaisAsync(context.Cartoes,
                context.Cartoes.Where(c => c.JogoId == jogoId), lances.Cartoes, "cartões", trocas, ct);
            await TrocarSeFotMobTemMaisAsync(context.Substituicoes,
                context.Substituicoes.Where(s => s.JogoId == jogoId), lances.Substituicoes, "substituições", trocas, ct);
            await TrocarSeFotMobTemMaisAsync(context.PenaltisPerdidos,
                context.PenaltisPerdidos.Where(p => p.JogoId == jogoId), lances.PenaltisPerdidos, "pênaltis perdidos", trocas, ct);
            await TrocarSeFotMobTemMaisAsync(context.PenaltisDisputa,
                context.PenaltisDisputa.Where(p => p.JogoId == jogoId), lances.PenaltisDisputa, "cobranças na disputa de pênaltis", trocas, ct);

            if (trocas.Count > 0)
                await FotMobService.GravarVinculosAsync(context, lances.Vinculos, ct);

            // ── Estatísticas de time ──
            // EstatisticasJson é indexado pelo IdApi do time; sem ele não há como montar.
            if (jogo.TimeCasa is { IdApi: > 0 } && jogo.TimeVisitante is { IdApi: > 0 })
            {
                var doFotMob = _fotmob.MontarEstatisticasTimeJson(jogo, raiz);
                if (doFotMob != null)
                {
                    var atual = VolumeEstatisticasTime(jogo.EstatisticasJson);
                    var novo = VolumeEstatisticasTime(doFotMob);
                    if (novo > atual)
                    {
                        jogo.EstatisticasJson = doFotMob;
                        trocas.Add("estatísticas de time");
                    }
                }
            }

            // ── Estatísticas de jogador ──
            var jogadores = await _fotmob.GravarEstatisticasJogadoresAsync(context, jogo, raiz, ct,
                decidirTroca: (existentes, novas) => VolumeEstatisticasJogador(novas) > VolumeEstatisticasJogador(existentes));
            if (jogadores > 0) trocas.Add($"estatísticas de {jogadores} jogador(es)");

            // ── Placar ──
            var placar = AplicarPlacar(jogo, FotMobEventosService.LerSituacao(raiz, jogo.TimeCasa?.Nome, jogo.TimeVisitante?.Nome));
            if (placar != null) trocas.Add(placar);

            string mensagem;
            if (trocas.Count > 0)
            {
                await context.SaveChangesAsync(ct);
                mensagem = $"Completado com o FotMob — {string.Join("; ", trocas)}.";
            }
            else
            {
                mensagem = "FotMob consultado: nada a acrescentar ao que já estava gravado.";
            }

            if (lances.NaoResolvidos.Count > 0)
            {
                var nomes = lances.NaoResolvidos.Distinct().ToList();
                mensagem += $" {nomes.Count} nome(s) sem correspondência no elenco: {string.Join(", ", nomes)}.";
            }

            _logger.LogInformation("[ComplementoFotMob] Jogo {Id}: {Msg}", jogoId, mensagem);

            await LogFonteExterna.RegistrarAsync(context, LogFonteExterna.TipoFotMob,
                "Comparação com a reimportação", true, jogo,
                aberta.Partida is long id ? $"{mensagem} [partida {id}]" : mensagem, ct);

            return new Resultado(true, trocas.Count > 0, mensagem);
        }

        /// <summary>
        /// Troca os registros do jogo pelos do FotMob quando ele tem mais. A troca é
        /// da categoria inteira, nunca mistura: somar um gol do FotMob aos da
        /// api-football duplicaria o gol que as duas têm com minutos diferentes.
        /// </summary>
        private static async Task TrocarSeFotMobTemMaisAsync<T>(
            DbSet<T> tabela, IQueryable<T> doJogo, List<T> doFotMob, string rotulo,
            List<string> trocas, CancellationToken ct) where T : class
        {
            if (doFotMob.Count == 0) return;

            var atuais = await doJogo.ToListAsync(ct);
            if (doFotMob.Count <= atuais.Count) return;

            tabela.RemoveRange(atuais);
            tabela.AddRange(doFotMob);
            trocas.Add($"{rotulo} {atuais.Count} → {doFotMob.Count}");
        }

        /// <summary>
        /// Placar do FotMob quando a api-football não deu conta dele.
        ///
        /// Encerrado: vai para o placar de verdade se ainda não há nenhum — o jogo
        /// acabou, e sem isso ele ficaria "em andamento" até a api-football acordar.
        /// Decidido nos pênaltis, o placar da disputa vai junto (Jogo.PenaltisCasa) —
        /// é ele que diz ao chaveamento quem avançou. Se o FotMob não trouxer esse
        /// placar, o jogo fica no parcial: placar final sem vencedor é pior que nenhum.
        ///
        /// Em andamento: vai para o parcial quando a api-football não tem parcial ou
        /// tem menos gols que o FotMob.
        /// </summary>
        internal static string? AplicarPlacar(Jogo jogo, FotMobEventosService.SituacaoPartida s)
        {
            if (!s.Iniciada || s.PlacarCasa is not int casa || s.PlacarVisitante is not int fora) return null;
            if (jogo.PlacarCasa != null) return null;

            var semVencedorDaDisputa = s.Codigo == "PEN" && (s.PenaltisCasa == null || s.PenaltisVisitante == null);
            if (s.Finalizada && !semVencedorDaDisputa)
            {
                if (s.Codigo == "PEN")
                {
                    jogo.PenaltisCasa = s.PenaltisCasa;
                    jogo.PenaltisVisitante = s.PenaltisVisitante;
                }

                jogo.PlacarCasa = casa;
                jogo.PlacarVisitante = fora;
                jogo.Status = "Finalizado";
                jogo.Atualizado = 1;
                ApiFootballService.LimparParcial(jogo);
                return s.Codigo == "PEN"
                    ? $"placar final {casa}×{fora} ({s.PenaltisCasa}×{s.PenaltisVisitante} nos pênaltis)"
                    : $"placar final {casa}×{fora}";
            }

            var semParcial = jogo.PlacarParcialCasa == null || jogo.PlacarParcialVisitante == null;
            var fotmobNaFrente = !semParcial && casa + fora > jogo.PlacarParcialCasa + jogo.PlacarParcialVisitante;
            if (!semParcial && !fotmobNaFrente) return null;

            jogo.PlacarParcialCasa = casa;
            jogo.PlacarParcialVisitante = fora;
            jogo.MinutoParcial = s.Minuto;
            jogo.AcrescimoParcial = s.Acrescimo;
            jogo.StatusParcial = s.Finalizada ? "P" : s.Codigo;
            jogo.PlacarParcialEm = DateTime.UtcNow;
            return $"placar parcial {casa}×{fora}";
        }

        // Contadores que as duas fontes publicam com o mesmo significado. Posse de bola
        // fica de fora (é percentual e não cresce) e também o que só uma das fontes
        // tem ("Free Kicks" só a api-football), senão a comparação pesaria para um lado
        // mesmo com as duas igualmente atualizadas.
        private static readonly string[] ContadoresTime =
        {
            "Total Shots", "Shots on Goal", "Total passes", "Fouls",
            "Corner Kicks", "Offsides", "Yellow Cards", "Red Cards",
        };

        internal static double VolumeEstatisticasTime(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;

                double soma = 0;
                foreach (var time in doc.RootElement.EnumerateArray())
                {
                    if (!time.TryGetProperty("Stats", out var stats) || stats.ValueKind != JsonValueKind.Object) continue;

                    foreach (var chave in ContadoresTime)
                    {
                        if (!stats.TryGetProperty(chave, out var v)) continue;
                        if (v.ValueKind == JsonValueKind.Number) soma += v.GetDouble();
                        else if (v.ValueKind == JsonValueKind.String &&
                                 double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                            soma += n;
                    }
                }
                return soma;
            }
            catch (JsonException)
            {
                return 0;
            }
        }

        // Só o que as duas fontes medem igual. Duelos e desarmes o FotMob detalha mais,
        // e contá-los faria o FotMob "vencer" sempre — levando embora a nota do jogador,
        // que só a api-football publica.
        internal static long VolumeEstatisticasJogador(IEnumerable<EstatisticaJogador> linhas) =>
            linhas.Sum(e => (long)(e.Minutos ?? 0) + e.PassesTotal + e.FinalizacoesTotal);
    }
}
