using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Aplica ao jogo a escalação que a FIFA publicou (live/football → HomeTeam/AwayTeam
    /// › Players).
    ///
    /// Vale para as competições que só existem na FIFA — Mundial Sub-20 Feminino e as
    /// demais de base/feminino que nem a api-football nem a ESPN catalogam. Fora delas
    /// nem é acionado: o jogo precisa ter vindo do FifaService, porque a referência da
    /// partida (competição/season/stage/match) mora em Jogo.LinkDetalhes.
    ///
    /// A REGRA de aplicação não está aqui — está em
    /// <see cref="EspnEscalacaoService.AplicarAtletasAsync"/>, que é fonte-agnóstica.
    /// Este serviço só traduz o formato da FIFA para a lista de
    /// <see cref="EspnEscalacaoService.AtletaEscalado"/> que aquele método consome,
    /// exatamente como o FotMobEscalacaoService faz com o FotMob.
    /// </summary>
    public class FifaEscalacaoService
    {
        private readonly FifaService _fifa;
        private readonly EspnEscalacaoService _aplicador;
        private readonly ILogger<FifaEscalacaoService> _logger;

        public FifaEscalacaoService(
            FifaService fifa,
            EspnEscalacaoService aplicador,
            ILogger<FifaEscalacaoService> logger)
        {
            _fifa = fifa;
            _aplicador = aplicador;
            _logger = logger;
        }

        /// <param name="filtroLado">
        /// Recebe true para o mandante e false para o visitante; devolver false pula o
        /// lado. Mesmo contrato das outras fontes — serve ao fallback automático da
        /// reimportação, que só quer preencher o lado que ninguém trouxe.
        /// </param>
        public async Task<ResultadoFifa> AplicarAsync(
            FutebolContext context, int jogoId, string usuarioId,
            Func<bool, bool>? filtroLado = null, CancellationToken ct = default)
        {
            var (jogo, doc, idMatch, erro) = await _fifa.AbrirPartidaAsync(context, jogoId, ct);
            if (erro != null) return erro;

            using var _ = doc!;
            var raiz = doc!.RootElement;

            var lados = 0;
            var criados = 0;

            // Estádio e árbitro só entram no calendário da FIFA perto da partida, e a
            // sincronização não volta no jogo já importado — a reimportação é a chance
            // de preencher quem nasceu sem eles. Só grava sobre o que está em branco.
            var fichaDaPartida = false;
            if (string.IsNullOrWhiteSpace(jogo!.Estadio) && FifaService.Estadio(raiz) is { } estadio)
            {
                jogo.Estadio = estadio;
                fichaDaPartida = true;
            }
            if (string.IsNullOrWhiteSpace(jogo.Arbitro) && FifaService.Arbitro(raiz) is { } arbitro)
            {
                jogo.Arbitro = arbitro;
                fichaDaPartida = true;
            }

            // Placar final, pelo mesmo motivo: quem aperta o botão depois do apito
            // final espera sair da tela com o jogo resolvido, e a sincronização do
            // calendário da FIFA (a única que gravava resultado) é manual — ver
            // FifaService.AplicarPlacarFinal.
            var placarGravado = FifaService.AplicarPlacarFinal(jogo, raiz);
            if (placarGravado) fichaDaPartida = true;

            foreach (var (propriedade, ehCasa) in new[] { ("HomeTeam", true), ("AwayTeam", false) })
            {
                if (!raiz.TryGetProperty(propriedade, out var time) ||
                    time.ValueKind != JsonValueKind.Object) continue;

                // Confere de quem é o XI antes de gravar: pôr a escalação do adversário
                // no lado errado é pior do que não gravar nada. A conferência é pelo
                // IdTeam da FIFA (ver FifaService.ETimeDaFifa), não pelo nome.
                var nosso = ehCasa ? jogo!.TimeCasa : jogo!.TimeVisitante;
                if (!FifaService.ETimeDaFifa(time, nosso))
                {
                    _logger.LogInformation(
                        "[FifaEscalacao] Jogo {Id}: \"{Deles}\" não casou com \"{Nosso}\".",
                        jogoId, FifaService.Localizado(time, "TeamName"), nosso?.Nome);
                    continue;
                }

                if (filtroLado != null && !filtroLado(ehCasa)) continue;

                var atletas = LerAtletas(time).ToList();
                if (atletas.Count == 0) continue;

                var resultado = await _aplicador.AplicarAtletasAsync(
                    context, jogo!, ehCasa,
                    // A FIFA chama a formação de "Tactics" ("4-2-3-1") — mesmo formato
                    // que as outras fontes mandam.
                    FifaService.Texto(time, "Tactics"),
                    atletas, usuarioId, FonteEscalacao.Fifa, ct);

                if (resultado == null) continue;
                lados++;
                criados += resultado.Value;
            }

            if (lados == 0)
            {
                // Sem escalação, mas o estádio/árbitro/placar que vieram na mesma visita
                // valem a gravação — é o que o botão devolve para o jogo que ficou sem
                // ficha, e é também o caminho de quem chama só para atualizar o
                // resultado (filtro que recusa os dois lados).
                if (fichaDaPartida) await context.SaveChangesAsync(ct);

                if (placarGravado)
                    return new ResultadoFifa(true,
                        $"Placar final da FIFA gravado: {jogo.PlacarCasa}×{jogo.PlacarVisitante}.",
                        idMatch);

                return new ResultadoFifa(false,
                    filtroLado == null
                        ? "A FIFA não publicou a escalação desta partida (ou os times não casaram)."
                        : "A FIFA não tinha o lado que faltava desta partida.",
                    idMatch);
            }

            await context.SaveChangesAsync(ct);

            var msg = $"Escalação inicial de {lados} time(s) atualizada pela FIFA";
            msg += criados > 0 ? $" ({criados} jogador(es) cadastrado(s) no elenco)." : ".";
            if (placarGravado) msg += $" Placar final: {jogo.PlacarCasa}×{jogo.PlacarVisitante}.";
            return new ResultadoFifa(true, msg, idMatch);
        }

        // ── Leitura da escalação da FIFA ──────────────────────────────────────

        private static IEnumerable<EspnEscalacaoService.AtletaEscalado> LerAtletas(JsonElement time)
        {
            if (!time.TryGetProperty("Players", out var jogadores) ||
                jogadores.ValueKind != JsonValueKind.Array) yield break;

            var entraram = ReservasQueEntraram(time);

            foreach (var j in jogadores.EnumerateArray())
            {
                var nome = FifaService.Localizado(j, "PlayerName");
                if (string.IsNullOrWhiteSpace(nome)) continue;

                // Status 1 = titular, 2 = relacionado no banco.
                var titular = FifaService.Numero(j, "Status") == 1;

                yield return new EspnEscalacaoService.AtletaEscalado(
                    // A FIFA escreve o sobrenome em caixa alta ("Cathy BIYA"), o que
                    // atrapalharia o casamento por nome com o cadastro e ficaria feio
                    // no campinho da análise.
                    NormalizarNome(nome),
                    FifaService.NumeroNulavel(j, "ShirtNumber"),
                    titular ? Posicao(j) : "",
                    titular,
                    // FieldStatus não serve para isto: ele diz quem TERMINOU em campo,
                    // então marca como "fora" o titular que foi substituído. Quem entrou
                    // sai da lista de substituições do próprio time.
                    Atuou: titular || entraram.Contains(FifaService.Texto(j, "IdPlayer") ?? ""));
            }
        }

        /// <summary>
        /// Ids das reservas que entraram em campo, lidos das substituições do time.
        /// É o que separa "ficou no banco o jogo todo" de "jogou" — a mesma distinção
        /// que o "appearances" da ESPN dá.
        /// </summary>
        private static HashSet<string> ReservasQueEntraram(JsonElement time)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (!time.TryGetProperty("Substitutions", out var subs) ||
                subs.ValueKind != JsonValueKind.Array) return ids;

            foreach (var s in subs.EnumerateArray())
                if (FifaService.Texto(s, "IdPlayerOn") is { Length: > 0 } id)
                    ids.Add(id);

            return ids;
        }

        /// <summary>
        /// "Cathy BIYA" → "Cathy Biya". Só mexe na palavra que está inteira em
        /// maiúsculas: nome com acento e partícula ("de Aza") continua como veio.
        /// </summary>
        internal static string NormalizarNome(string nome)
        {
            var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Length > 1 && p.All(c => !char.IsLower(c))
                    ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()
                    : p);

            return string.Join(' ', partes);
        }

        /// <summary>
        /// Traduz a posição da FIFA para a string em português que
        /// EspnEscalacaoService.RankLinha e XEstimado sabem interpretar — mesma escolha
        /// do FotMobEscalacaoService, para uma fonte nova não exigir um segundo caminho
        /// de distribuição nos slots.
        ///
        /// A FIFA manda só a linha (Position), sem lado: LineupX/LineupY vêm nulos
        /// nestas competições. Sem o lado, o aplicador põe todo mundo na linha certa e
        /// o analista arrasta quem estiver trocado — que é o comportamento que a tela já
        /// tem para as outras fontes sem coordenada.
        /// </summary>
        private static string Posicao(JsonElement j) => FifaService.Numero(j, "Position") switch
        {
            0 => "Goleiro",
            1 => "Zagueiro",
            2 => "Meia",
            3 => "Atacante",
            // 6 aparece nos volantes de um 4-2-3-1 (dois jogadores, na linha do meio) —
            // é meio-campo, e não há código conhecido de defesa ou ataque acima de 3.
            6 => "Meia",
            // Posição desconhecida vira meia: é a linha do meio do campo e o erro mais
            // barato quando o aplicador for distribuir nos slots.
            _ => "Meia",
        };
    }
}
