using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Aplica ao jogo a escalação inicial publicada pelo FotMob. É o primeiro degrau da
    /// cascata quando a api-football não traz a escalação, antes da ESPN — ver
    /// EscalacaoAlternativaService, que é quem decide a ordem.
    ///
    /// A REGRA de aplicação não está aqui — está em
    /// <see cref="EspnEscalacaoService.AplicarAtletasAsync"/>, que é fonte-agnóstica.
    /// Este serviço faz só a tradução do formato do FotMob para a lista de
    /// <see cref="EspnEscalacaoService.AtletaEscalado"/> que aquele método consome.
    /// Duplicar a aplicação significaria manter duas cópias de decisões delicadas — em
    /// especial a de só escrever a escalação COMPARTILHADA quando ela está vazia.
    ///
    /// Por isso a dependência de EspnEscalacaoService, que à primeira vista parece
    /// estranha: o que se reaproveita dele não é nada da ESPN, é o aplicador.
    /// </summary>
    public class FotMobEscalacaoService
    {
        private readonly FotMobService _fotmob;
        private readonly EspnEscalacaoService _aplicador;
        private readonly ILogger<FotMobEscalacaoService> _logger;

        public FotMobEscalacaoService(
            FotMobService fotmob,
            EspnEscalacaoService aplicador,
            ILogger<FotMobEscalacaoService> logger)
        {
            _fotmob = fotmob;
            _aplicador = aplicador;
            _logger = logger;
        }

        /// <param name="filtroLado">
        /// Recebe true para o mandante e false para o visitante; devolver false pula o
        /// lado. Mesmo contrato do aplicador da ESPN — serve ao fallback automático da
        /// reimportação, que só quer preencher o lado que ninguém trouxe.
        /// </param>
        public async Task<ResultadoFotMob> AplicarAsync(
            FutebolContext context, int jogoId, string? usuarioId,
            Func<bool, bool>? filtroLado = null, CancellationToken ct = default)
        {
            var (jogo, doc, partida, erro) = await _fotmob.AbrirPartidaAsync(
                context, jogoId, exigeIdApi: false, ct);
            if (erro != null) return erro;

            using var _ = doc!;

            if (!doc!.RootElement.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("lineup", out var escalacao))
                return new ResultadoFotMob(false,
                    "O FotMob não publicou a escalação desta partida.", partida);

            var lados = 0;
            var criados = 0;

            foreach (var (propriedade, ehCasa) in new[] { ("homeTeam", true), ("awayTeam", false) })
            {
                if (!escalacao.TryGetProperty(propriedade, out var time)) continue;

                // Confere o nome antes de confiar em qual lado é qual: gravar o XI do
                // adversário no lugar errado é pior do que não gravar nada.
                var nome = time.TryGetProperty("name", out var n) ? n.GetString() : null;
                var nosso = ehCasa ? jogo!.TimeCasa?.Nome : jogo!.TimeVisitante?.Nome;
                if (!TimeNomeMatcher.SaoMesmoTime(nosso, nome))
                {
                    _logger.LogInformation(
                        "[FotMobEscalacao] Jogo {Id}: \"{Deles}\" não casou com \"{Nosso}\".",
                        jogoId, nome, nosso);
                    continue;
                }

                if (filtroLado != null && !filtroLado(ehCasa)) continue;

                var atletas = LerAtletas(time).ToList();
                if (atletas.Count == 0) continue;

                var resultado = await _aplicador.AplicarAtletasAsync(
                    context, jogo!, ehCasa,
                    time.TryGetProperty("formation", out var f) ? f.GetString() : null,
                    atletas, usuarioId, FonteEscalacao.FotMob, ct);

                if (resultado == null) continue;
                lados++;
                criados += resultado.Value;
            }

            if (lados == 0)
                return new ResultadoFotMob(false,
                    filtroLado == null
                        ? "Não foi possível casar os times do FotMob com os do jogo."
                        : "O FotMob não tinha o lado que faltava desta partida.",
                    partida);

            await context.SaveChangesAsync(ct);

            var msg = $"Escalação inicial de {lados} time(s) atualizada pelo FotMob";
            msg += criados > 0 ? $" ({criados} jogador(es) cadastrado(s) no elenco)." : ".";
            return new ResultadoFotMob(true, msg, partida);
        }

        // ── Leitura da escalação do FotMob ────────────────────────────────────

        private static IEnumerable<EspnEscalacaoService.AtletaEscalado> LerAtletas(JsonElement time)
        {
            foreach (var (grupo, titular) in new[] { ("starters", true), ("subs", false) })
            {
                if (!time.TryGetProperty(grupo, out var jogadores) ||
                    jogadores.ValueKind != JsonValueKind.Array) continue;

                foreach (var j in jogadores.EnumerateArray())
                {
                    var nome = j.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(nome)) continue;

                    long? idFotMob = j.TryGetProperty("id", out var idEl)
                        ? idEl.ValueKind switch
                        {
                            JsonValueKind.Number => idEl.GetInt64(),
                            JsonValueKind.String => long.TryParse(idEl.GetString(), out var parsed) ? parsed : null,
                            _ => null,
                        }
                        : null;

                    yield return new EspnEscalacaoService.AtletaEscalado(
                        nome,
                        Camisa(j),
                        titular ? Posicao(j) : "",
                        titular,
                        // Reserva que entrou tem evento de substituição registrado; quem
                        // ficou no banco o jogo todo não tem nenhum. É o equivalente ao
                        // "appearances" da ESPN, e é o que separa "não jogou" de "jogou".
                        Atuou: titular || EntrouEmCampo(j),
                        // O vínculo é o casamento mais seguro que existe: quem já foi
                        // ligado ao FotMob (na tela do jogador ou numa importação) casa
                        // direto, sem depender de nome nem de camisa.
                        IdFotMob: idFotMob is > 0 ? idFotMob : null);
                }
            }
        }

        /// <summary>
        /// A camisa vem como string ("7") no FotMob, e é o desempate quando o nome não
        /// casa — o que em clube árabe acontece bastante, porque a transliteração muda
        /// de fonte para fonte.
        /// </summary>
        private static int? Camisa(JsonElement j) =>
            j.TryGetProperty("shirtNumber", out var c)
                ? c.ValueKind switch
                {
                    JsonValueKind.String => int.TryParse(c.GetString(), out var i) ? i : null,
                    JsonValueKind.Number => c.GetInt32(),
                    _ => null,
                }
                : null;

        private static bool EntrouEmCampo(JsonElement j) =>
            j.TryGetProperty("performance", out var p) &&
            p.TryGetProperty("substitutionEvents", out var eventos) &&
            eventos.ValueKind == JsonValueKind.Array &&
            eventos.EnumerateArray().Any(e =>
                e.TryGetProperty("type", out var t) && t.GetString() == "subIn");

        /// <summary>
        /// Traduz a posição do FotMob para a string em português que
        /// EspnEscalacaoService.RankLinha e XEstimado sabem interpretar. O FotMob não
        /// publica o nome da posição: manda usualPlayingPositionId (0 goleiro, 1 defesa,
        /// 2 meio, 3 ataque) e as coordenadas do jogador no desenho da escalação.
        ///
        /// Traduzir para texto em vez de mexer no aplicador é de propósito: a
        /// distribuição nos slots da formação já é feita a partir dessa string, está
        /// testada e serve às outras fontes. Uma fonte nova não deveria exigir um
        /// segundo caminho de distribuição.
        /// </summary>
        private static string Posicao(JsonElement j)
        {
            var linha = j.TryGetProperty("usualPlayingPositionId", out var p) &&
                        p.ValueKind == JsonValueKind.Number ? p.GetInt32() : -1;

            var lado = Lado(j);

            return linha switch
            {
                0 => "Goleiro",
                // Sem o lado, "Zagueiro" — que XEstimado trata como central. Com lado
                // extremo é lateral; é o que o x do desenho separa.
                1 => lado switch
                {
                    Extremo.Esquerda => "Lateral esquerdo",
                    Extremo.Direita => "Lateral direito",
                    _ => "Zagueiro",
                },
                2 => lado switch
                {
                    Extremo.Esquerda => "Meia esquerdo",
                    Extremo.Direita => "Meia direito",
                    _ => "Meia",
                },
                3 => lado switch
                {
                    Extremo.Esquerda => "Atacante esquerdo",
                    Extremo.Direita => "Atacante direito",
                    _ => "Atacante",
                },
                // Posição desconhecida vira meia: é a linha do meio do campo e o erro
                // mais barato quando o aplicador for distribuir nos slots.
                _ => "Meia",
            };
        }

        private enum Extremo { Esquerda, Centro, Direita }

        /// <summary>
        /// De que lado do campo o jogador começou, pelo x do desenho da escalação
        /// (verticalLayout.x, de 0 a 1).
        ///
        /// SUPOSIÇÃO: 0 é a esquerda de quem ataca, que é a mesma orientação do
        /// PosicaoX do nosso cadastro. Se um dia a defesa aparecer espelhada na tela,
        /// é aqui que se inverte — e só aqui. Isso afeta apenas a POSIÇÃO no desenho:
        /// quem é titular, quem é reserva e as estatísticas de cada um não dependem
        /// disto.
        /// </summary>
        private static Extremo Lado(JsonElement j)
        {
            if (!j.TryGetProperty("verticalLayout", out var layout) ||
                layout.ValueKind != JsonValueKind.Object ||
                !layout.TryGetProperty("x", out var x) ||
                x.ValueKind != JsonValueKind.Number) return Extremo.Centro;

            var valor = x.GetDouble();
            if (valor <= 0.3) return Extremo.Esquerda;
            if (valor >= 0.7) return Extremo.Direita;
            return Extremo.Centro;
        }
    }
}
