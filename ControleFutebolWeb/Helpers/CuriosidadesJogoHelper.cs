using System.Globalization;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Gera as "curiosidades" da aba do pós-jogo: o que aconteceu nesta partida que
    /// foge do normal quando comparado com o resto do campeonato e da temporada —
    /// recorde individual do campeonato, recorde pessoal, número muito acima da média
    /// do jogador, marcos de artilharia, gol mais rápido, maior goleada, sequências.
    ///
    /// Tudo compara com o que veio ANTES desta partida (mesmo corte do pré-jogo): no
    /// pós-jogo de uma rodada antiga, "recorde" é o recorde da época, não o de hoje.
    ///
    /// É função pura — o PainelJogoService carrega os dados e entrega aqui — para
    /// dar pra testar as regras sem banco.
    /// </summary>
    public static class CuriosidadesJogoHelper
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        /// <param name="Tipo">recorde | pessoal | media | gol | time — o JS escolhe o ícone por ele.</param>
        /// <param name="Chave">Identifica a curiosidade dentro do jogo (ex.: "desarme:123", "goleada") —
        /// é o que impede de gravar a mesma duas vezes no perfil do jogador/clube.</param>
        /// <param name="AmbosTimes">Curiosidade de time que fala dos dois (fim de sequência,
        /// jogo com mais gols): vai para o perfil dos dois clubes.</param>
        public sealed record Curiosidade(string Tipo, string Titulo, string Texto, int Relevancia, int? JogadorId = null, bool? Casa = null,
            string Chave = "", bool AmbosTimes = false);

        public sealed record JogoResumo(int Id, DateTime? Data, int TimeCasaId, int TimeVisitanteId, int? PlacarCasa, int? PlacarVisitante);

        public sealed record GolCampeonato(int JogoId, int JogadorId, int Minuto);

        /// <summary>Jogo de um dos times em qualquer competição/temporada — base dos jejuns
        /// que atravessam campeonatos ("primeiro jogo sem sofrer gol em 9 meses").</summary>
        public sealed record JogoTime(int Id, DateTime? Data, int TimeCasaId, int TimeVisitanteId, int PlacarCasa, int PlacarVisitante,
            string NomeCasa, string NomeVisitante, string Competicao);

        public sealed class Entrada
        {
            public int JogoId { get; init; }
            public int TimeCasaId { get; init; }
            public int TimeVisitanteId { get; init; }
            public string NomeCasa { get; init; } = "";
            public string NomeVisitante { get; init; } = "";

            /// <summary>Linhas de EstatisticaJogador desta partida.</summary>
            public IReadOnlyList<EstatisticaJogador> EstatisticasJogo { get; init; } = Array.Empty<EstatisticaJogador>();

            /// <summary>Linhas dos outros jogos da mesma competição/temporada, antes desta partida.</summary>
            public IReadOnlyList<EstatisticaJogador> HistoricoCampeonato { get; init; } = Array.Empty<EstatisticaJogador>();

            /// <summary>Linhas dos jogadores desta partida em qualquer competição da temporada, antes dela.</summary>
            public IReadOnlyList<EstatisticaJogador> HistoricoTemporada { get; init; } = Array.Empty<EstatisticaJogador>();

            /// <summary>Jogos da competição/temporada antes desta partida, mais ela própria.</summary>
            public IReadOnlyList<JogoResumo> JogosCampeonato { get; init; } = Array.Empty<JogoResumo>();

            /// <summary>Gols (sem gol contra) da competição/temporada antes desta partida, mais os dela.</summary>
            public IReadOnlyList<GolCampeonato> GolsCampeonato { get; init; } = Array.Empty<GolCampeonato>();

            /// <summary>Jogos com placar dos dois times em qualquer competição e temporada, antes desta partida.</summary>
            public IReadOnlyList<JogoTime> HistoricoTimes { get; init; } = Array.Empty<JogoTime>();

            public DateTime? DataJogo { get; init; }

            public Func<int, string> NomeJogador { get; init; } = id => "";

            /// <summary>true = jogou pelo mandante, false = visitante, null = não dá pra saber.</summary>
            public Func<int, bool?> LadoJogador { get; init; } = id => null;

            public int Limite { get; init; } = 12;
        }

        // Id: o vocabulário de FonteEstatistica.Cobre — linha de fonte que não publica
        // a métrica fica fora da comparação, senão o 0 "não informado" vira histórico.
        // Mínimo: abaixo disso nem recorde vale a pena contar. Peso: desempata a
        // relevância (10 desarmes chama mais atenção que 5 faltas cometidas).
        private sealed record Metrica(string Id, string Verbo, string Singular, string Plural, Func<EstatisticaJogador, int> Valor, int Minimo, int Peso);

        private static readonly Metrica[] Metricas =
        {
            new("assistencia", "deu", "assistência", "assistências", e => e.Assistencias, 2, 12),
            new("desarme", "fez", "desarme", "desarmes", e => e.Desarmes, 5, 10),
            new("defesa", "fez", "defesa", "defesas", e => e.Defesas, 5, 10),
            new("interceptacao", "fez", "interceptação", "interceptações", e => e.Interceptacoes, 4, 8),
            new("finalizacao_gol", "acertou", "finalização no gol", "finalizações no gol", e => e.FinalizacoesNoGol, 4, 8),
            new("drible_certo", "acertou", "drible", "dribles", e => e.DriblesCertos, 4, 8),
            new("passe_chave", "deu", "passe decisivo", "passes decisivos", e => e.PassesChave, 4, 8),
            new("finalizacao", "fez", "finalização", "finalizações", e => e.FinalizacoesTotal, 5, 6),
            new("duelo_vencido", "venceu", "duelo", "duelos", e => e.DuelosVencidos, 10, 6),
            new("bloqueio", "fez", "bloqueio", "bloqueios", e => e.Bloqueios, 3, 6),
            new("passes_precisao", "acertou", "passe", "passes", e => e.PassesCertos, 60, 4),
            new("falta_sofrida", "sofreu", "falta", "faltas", e => e.FaltasSofridas, 5, 3),
            new("falta_cometida", "cometeu", "falta", "faltas", e => e.FaltasCometidas, 4, 0),
        };

        // Quanto histórico é preciso antes de chamar algo de recorde: com 2 jogos no
        // campeonato, qualquer número vira "o maior da competição".
        private const int MinJogosCampeonatoParaRecorde = 5;
        private const int MinJogosJogadorParaRecordePessoal = 5;
        private const int MinJogosJogadorParaPrimeiroGol = 3;
        private const int MaxPorJogador = 2;
        private const int MinGolsParaGolMaisRapido = 10;
        private const double MultiploMediaMinimo = 2.5;

        public static List<Curiosidade> Gerar(Entrada d)
        {
            var lista = new List<Curiosidade>();
            lista.AddRange(CuriosidadesEstatisticas(d));
            lista.AddRange(CuriosidadesGols(d));
            lista.AddRange(CuriosidadesTimes(d));

            // Até MaxPorJogador por jogador, para a aba não virar o jogo de um só.
            // Mesma chave duas vezes (fim de jejum no campeonato e em todas as
            // competições) fica só a mais relevante.
            var porJogador = new Dictionary<int, int>();
            var chaves = new HashSet<string>();
            var resultado = new List<Curiosidade>();
            foreach (var c in lista.OrderByDescending(c => c.Relevancia))
            {
                if (c.Chave.Length > 0 && !chaves.Add(c.Chave)) continue;
                if (c.JogadorId is int jid)
                {
                    if (porJogador.GetValueOrDefault(jid) >= MaxPorJogador) continue;
                    porJogador[jid] = porJogador.GetValueOrDefault(jid) + 1;
                }
                resultado.Add(c);
                if (resultado.Count >= d.Limite) break;
            }
            return resultado;
        }

        private static string Qtd(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

        private static string Dec(double v) => v.ToString("0.0", PtBr);

        // Recorde pessoal só quando supera com folga: 79 passes contra 78 não é notícia.
        private static bool SuperaComFolga(int v, int max) => v - max >= Math.Max(1, (int)Math.Ceiling(max * 0.15));

        // ── Números individuais (EstatisticaJogador) ────────────────────────────
        private static IEnumerable<Curiosidade> CuriosidadesEstatisticas(Entrada d)
        {
            // Uma linha por jogo/jogador no histórico (se a mesma partida vier de duas
            // fontes, fica a primeira — duplicar inflaria a contagem de jogos).
            static List<EstatisticaJogador> Unicas(IEnumerable<EstatisticaJogador> l) =>
                l.GroupBy(e => (e.JogoId, e.JogadorId)).Select(g => g.First()).ToList();

            var historicoCamp = Unicas(d.HistoricoCampeonato);
            var historicoTemp = Unicas(d.HistoricoTemporada);
            var jogosCampIds = historicoCamp.Select(e => e.JogoId).ToHashSet();

            foreach (var linha in Unicas(d.EstatisticasJogo).Where(e => e.Minutos > 0))
            {
                var nome = d.NomeJogador(linha.JogadorId);
                var lado = d.LadoJogador(linha.JogadorId);
                var meusCamp = historicoCamp.Where(e => e.JogadorId == linha.JogadorId && e.Minutos > 0).ToList();
                var meusTemp = historicoTemp.Where(e => e.JogadorId == linha.JogadorId && e.Minutos > 0).ToList();
                // Recorde "na temporada" só quando ele jogou outra competição; senão a
                // temporada é o próprio campeonato e a frase certa é "no campeonato".
                bool temOutrasCompeticoes = meusTemp.Any(e => !jogosCampIds.Contains(e.JogoId) && e.JogoId != d.JogoId);

                foreach (var m in Metricas)
                {
                    if (!FonteEstatistica.Cobre(linha.Fonte, m.Id)) continue;
                    var v = m.Valor(linha);
                    if (v < m.Minimo) continue;

                    var feito = $"{nome} {m.Verbo} {Qtd(v, m.Singular, m.Plural)}";

                    // 1) Recorde individual do campeonato
                    var campCobertas = historicoCamp.Where(e => FonteEstatistica.Cobre(e.Fonte, m.Id)).ToList();
                    if (campCobertas.Select(e => e.JogoId).Distinct().Count() >= MinJogosCampeonatoParaRecorde)
                    {
                        var max = campCobertas.Max(m.Valor);
                        if (v > max)
                        {
                            yield return new Curiosidade("recorde", "Recorde do campeonato",
                                $"{feito}, o maior número individual numa partida do campeonato. O recorde anterior era {max}.",
                                100 + m.Peso, linha.JogadorId, lado, Chave: $"{m.Id}:{linha.JogadorId}");
                            continue;
                        }
                        // Empate só em número alto: 5 finalizações empatadas no topo é comum.
                        if (v == max && v >= m.Minimo * 1.5)
                        {
                            yield return new Curiosidade("recorde", "Igualou o recorde",
                                $"{feito} e igualou o recorde individual numa partida do campeonato.",
                                75 + m.Peso, linha.JogadorId, lado, Chave: $"{m.Id}:{linha.JogadorId}");
                            continue;
                        }
                    }

                    // 2) Recorde pessoal (temporada inteira, ou só o campeonato)
                    var tempCobertas = meusTemp.Where(e => FonteEstatistica.Cobre(e.Fonte, m.Id)).ToList();
                    if (temOutrasCompeticoes && tempCobertas.Count >= MinJogosJogadorParaRecordePessoal)
                    {
                        var max = tempCobertas.Max(m.Valor);
                        if (SuperaComFolga(v, max))
                        {
                            yield return new Curiosidade("pessoal", "Recorde pessoal na temporada",
                                $"{feito}, seu melhor número na temporada. O máximo antes era {max}, em {tempCobertas.Count} jogos.",
                                65 + m.Peso, linha.JogadorId, lado, Chave: $"{m.Id}:{linha.JogadorId}");
                            continue;
                        }
                    }

                    var campMeusCobertas = meusCamp.Where(e => FonteEstatistica.Cobre(e.Fonte, m.Id)).ToList();
                    if (campMeusCobertas.Count < MinJogosJogadorParaRecordePessoal) continue;

                    var maxMeu = campMeusCobertas.Max(m.Valor);
                    if (SuperaComFolga(v, maxMeu))
                    {
                        yield return new Curiosidade("pessoal", "Recorde pessoal no campeonato",
                            $"{feito}, seu melhor número no campeonato. O máximo antes era {maxMeu}, em {campMeusCobertas.Count} jogos.",
                            55 + m.Peso, linha.JogadorId, lado, Chave: $"{m.Id}:{linha.JogadorId}");
                        continue;
                    }

                    // 3) Muito acima da própria média
                    var media = campMeusCobertas.Average(e => (double)m.Valor(e));
                    if (media > 0 && v >= media * MultiploMediaMinimo)
                    {
                        yield return new Curiosidade("media", "Muito acima da média",
                            $"{feito}, {Dec(v / media)} vezes a sua média no campeonato ({Dec(media)} por jogo).",
                            40 + m.Peso, linha.JogadorId, lado, Chave: $"{m.Id}:{linha.JogadorId}");
                    }
                }
            }
        }

        // ── Gols: hat-trick, artilharia, primeiro gol, gol mais rápido ──────────
        private static IEnumerable<Curiosidade> CuriosidadesGols(Entrada d)
        {
            var golsJogo = d.GolsCampeonato.Where(g => g.JogoId == d.JogoId).ToList();
            if (golsJogo.Count == 0) yield break;

            var anteriores = d.GolsCampeonato.Where(g => g.JogoId != d.JogoId).ToList();
            int jogosAnteriores = d.JogosCampeonato.Count(j => j.Id != d.JogoId);

            var antes = anteriores.GroupBy(g => g.JogadorId).ToDictionary(g => g.Key, g => g.Count());
            var depois = new Dictionary<int, int>(antes);
            foreach (var g in golsJogo) depois[g.JogadorId] = depois.GetValueOrDefault(g.JogadorId) + 1;
            int maxAntes = antes.Count > 0 ? antes.Values.Max() : 0;
            int maxDepois = depois.Values.Max();

            bool houveHatTrickAntes = anteriores.GroupBy(g => (g.JogoId, g.JogadorId)).Any(g => g.Count() >= 3);

            // Jogos que cada um já tinha disputado no campeonato (pela estatística).
            var jogosDisputados = d.HistoricoCampeonato
                .Where(e => e.Minutos > 0)
                .GroupBy(e => e.JogadorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.JogoId).Distinct().Count());

            foreach (var grupo in golsJogo.GroupBy(g => g.JogadorId))
            {
                int id = grupo.Key, n = grupo.Count();
                var nome = d.NomeJogador(id);
                var lado = d.LadoJogador(id);
                int total = depois[id], totalAntes = antes.GetValueOrDefault(id);

                if (n >= 3)
                {
                    var feito = n == 3 ? "fez um hat-trick" : $"marcou {n} gols";
                    if (!houveHatTrickAntes && jogosAnteriores >= MinJogosCampeonatoParaRecorde)
                        yield return new Curiosidade("gol", "Primeiro hat-trick do campeonato",
                            $"{nome} {feito}, {(n == 3 ? "o primeiro" : "o primeiro hat-trick")} do campeonato.", 95, id, lado, Chave: $"hattrick:{id}");
                    else
                        yield return new Curiosidade("gol", "Hat-trick", $"{nome} {feito} na partida.", 85, id, lado, Chave: $"hattrick:{id}");
                }

                // Artilharia, primeiro gol ou marco redondo: só a mais forte por jogador.
                if (total == maxDepois && total >= 3)
                {
                    int lideres = depois.Values.Count(x => x == maxDepois);
                    bool jaLiderava = maxAntes > 0 && totalAntes == maxAntes;
                    // Liderança dividida entre 4 ou mais (começo de campeonato) não é notícia.
                    if (lideres > 3)
                        continue;
                    if (lideres > 1)
                        yield return new Curiosidade("gol", "Artilharia",
                            $"{nome} chegou a {Qtd(total, "gol", "gols")} e divide a liderança da artilharia do campeonato.", 65, id, lado, Chave: $"gols:{id}");
                    else if (jaLiderava)
                        yield return new Curiosidade("gol", "Artilharia",
                            $"{nome} chegou a {Qtd(total, "gol", "gols")} e segue isolado na liderança da artilharia do campeonato.", 60, id, lado, Chave: $"gols:{id}");
                    else
                        yield return new Curiosidade("gol", "Artilharia",
                            $"{nome} assumiu a liderança isolada da artilharia do campeonato, com {total} gols.", 80, id, lado, Chave: $"gols:{id}");
                }
                else if (totalAntes == 0 && jogosDisputados.GetValueOrDefault(id) >= MinJogosJogadorParaPrimeiroGol)
                {
                    int jogoNumero = jogosDisputados[id] + 1;
                    yield return new Curiosidade("gol", "Primeiro gol no campeonato",
                        $"{nome} desencantou: primeiro gol no campeonato, no seu {jogoNumero}º jogo.", 45, id, lado, Chave: $"gols:{id}");
                }
                else
                {
                    int marco = total / 5 * 5;
                    if (marco >= 5 && totalAntes < marco)
                        yield return new Curiosidade("gol", "Marca de gols",
                            $"{nome} chegou a {total} gols no campeonato.", 50, id, lado, Chave: $"gols:{id}");
                }
            }

            // Gol mais rápido do campeonato
            if (anteriores.Count >= MinGolsParaGolMaisRapido)
            {
                var primeiro = golsJogo.OrderBy(g => g.Minuto).First();
                int recorde = anteriores.Min(g => g.Minuto);
                var nome = d.NomeJogador(primeiro.JogadorId);
                if (primeiro.Minuto < recorde)
                    yield return new Curiosidade("gol", "Gol mais rápido do campeonato",
                        $"{nome} marcou aos {primeiro.Minuto} minutos, o gol mais rápido do campeonato até aqui (o anterior foi aos {recorde}).",
                        90, primeiro.JogadorId, d.LadoJogador(primeiro.JogadorId), Chave: "gol_mais_rapido");
                else if (primeiro.Minuto == recorde)
                    yield return new Curiosidade("gol", "Gol mais rápido",
                        $"{nome} marcou aos {primeiro.Minuto} minutos e igualou o gol mais rápido do campeonato.",
                        60, primeiro.JogadorId, d.LadoJogador(primeiro.JogadorId), Chave: "gol_mais_rapido");
            }
        }

        // ── Times: placar fora do comum e sequências ────────────────────────────
        private static IEnumerable<Curiosidade> CuriosidadesTimes(Entrada d)
        {
            var este = d.JogosCampeonato.FirstOrDefault(j => j.Id == d.JogoId);
            if (este?.PlacarCasa == null || este.PlacarVisitante == null) yield break;
            int pc = este.PlacarCasa.Value, pv = este.PlacarVisitante.Value;

            var anteriores = d.JogosCampeonato
                .Where(j => j.Id != d.JogoId && j.PlacarCasa != null && j.PlacarVisitante != null)
                .OrderBy(j => j.Data)
                .ToList();

            if (anteriores.Count >= MinJogosCampeonatoParaRecorde)
            {
                int total = pc + pv;
                int maxTotal = anteriores.Max(j => j.PlacarCasa!.Value + j.PlacarVisitante!.Value);
                if (total >= 5 && total > maxTotal)
                    yield return new Curiosidade("time", "Jogo com mais gols",
                        $"Com {total} gols, é o jogo com mais gols do campeonato até aqui (o anterior teve {maxTotal}).", 85, Chave: "mais_gols", AmbosTimes: true);
                else if (total >= 5 && total == maxTotal)
                    yield return new Curiosidade("time", "Jogo com mais gols",
                        $"Com {total} gols, igualou o jogo com mais gols do campeonato.", 55, Chave: "mais_gols", AmbosTimes: true);

                int margem = Math.Abs(pc - pv);
                int maxMargem = anteriores.Max(j => Math.Abs(j.PlacarCasa!.Value - j.PlacarVisitante!.Value));
                if (margem >= 3)
                {
                    bool casaVenceu = pc > pv;
                    var vencedor = casaVenceu ? d.NomeCasa : d.NomeVisitante;
                    if (margem > maxMargem)
                        yield return new Curiosidade("time", "Maior goleada",
                            $"{vencedor} aplicou a maior goleada do campeonato até aqui: {Math.Max(pc, pv)} a {Math.Min(pc, pv)}.", 85, null, casaVenceu, Chave: "goleada");
                    else if (margem == maxMargem)
                        yield return new Curiosidade("time", "Maior goleada",
                            $"{vencedor} igualou a maior goleada do campeonato: {Math.Max(pc, pv)} a {Math.Min(pc, pv)}.", 55, null, casaVenceu, Chave: "goleada");
                }
            }

            foreach (var c in Sequencias(d, anteriores, d.TimeCasaId, d.NomeCasa, d.NomeVisitante, pc, pv, true))
                yield return c;
            foreach (var c in Sequencias(d, anteriores, d.TimeVisitanteId, d.NomeVisitante, d.NomeCasa, pv, pc, false))
                yield return c;

            foreach (var c in Jejuns(d, d.TimeCasaId, d.TimeVisitanteId, d.NomeCasa, d.NomeVisitante, pc, pv, true))
                yield return c;
            foreach (var c in Jejuns(d, d.TimeVisitanteId, d.TimeCasaId, d.NomeVisitante, d.NomeCasa, pv, pc, false))
                yield return c;
        }

        // ── Jejuns em todas as competições ──────────────────────────────────────
        // Diferente das sequências (só o campeonato), olham o histórico inteiro do
        // time: o jejum que vira notícia costuma atravessar temporada e competição.
        private const int MinJogosJejumSemSofrer = 8;
        private const int MinJogosJejumGols = 4;
        private const int MinJogosJejumVitoria = 6;
        private const int MinJogosJejumVitoriaFora = 6;
        private const int MinConfrontosJejum = 4;
        private const int MinJogosSequenciaSemSofrer = 4;

        private sealed record JogoDoTime(JogoTime Jogo, int GolsPro, int GolsContra, bool EmCasa, int AdversarioId);

        private static IEnumerable<Curiosidade> Jejuns(Entrada d, int timeId, int adversarioId, string nome, string adversario,
            int golsPro, int golsContra, bool casa)
        {
            // Do mais recente para o mais antigo; um jogo por Id (o confronto entre
            // os dois vem no histórico de ambos).
            var jogos = d.HistoricoTimes
                .Where(j => j.Id != d.JogoId && j.Data != null && (d.DataJogo == null || j.Data < d.DataJogo)
                         && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId))
                .GroupBy(j => j.Id).Select(g => g.First())
                .OrderByDescending(j => j.Data)
                .Select(j =>
                {
                    bool emCasa = j.TimeCasaId == timeId;
                    return new JogoDoTime(j, emCasa ? j.PlacarCasa : j.PlacarVisitante, emCasa ? j.PlacarVisitante : j.PlacarCasa,
                        emCasa, emCasa ? j.TimeVisitanteId : j.TimeCasaId);
                })
                .ToList();
            if (jogos.Count == 0) yield break;

            // Jogos seguidos, do mais recente para trás, até o primeiro em que
            // "ocorreu" — e esse jogo, quando está no histórico. Sem ele o jejum pode
            // ser só o começo dos dados, então as frases de "fim de jejum" não saem.
            static (int N, JogoDoTime? Ultimo) Jejum(IEnumerable<JogoDoTime> l, Func<JogoDoTime, bool> ocorreu)
            {
                int n = 0;
                foreach (var j in l)
                {
                    if (ocorreu(j)) return (n, j);
                    n++;
                }
                return (n, null);
            }

            string Duracao(int n, JogoDoTime ultimo)
            {
                var periodo = Periodo(ultimo.Jogo.Data, d.DataJogo);
                return periodo != null ? $"{n} jogos e {periodo}" : $"{n} jogos";
            }

            // Sem sofrer gol depois de muito tempo
            if (golsContra == 0)
            {
                var (n, ultimo) = Jejum(jogos, j => j.GolsContra == 0);
                if (n >= MinJogosJejumSemSofrer && ultimo != null)
                {
                    var periodo = Periodo(ultimo.Jogo.Data, d.DataJogo);
                    var quando = periodo != null ? $"pela primeira vez em {periodo} ({n} jogos seguidos sofrendo gol)" : $"depois de {n} jogos seguidos sofrendo gol";
                    yield return new Curiosidade("time", "Sem sofrer gol",
                        $"{nome} terminou um jogo sem sofrer gol {quando}. A última vez tinha sido em {Referencia(ultimo.Jogo)}.",
                        60 + Math.Min(n, 30), null, casa, Chave: $"sem_sofrer:{timeId}");
                }
            }

            // Voltou a marcar
            if (golsPro > 0)
            {
                var (n, ultimo) = Jejum(jogos, j => j.GolsPro > 0);
                if (n >= MinJogosJejumGols && ultimo != null)
                    yield return new Curiosidade("time", "Fim do jejum de gols",
                        $"{nome} voltou a marcar depois de {Duracao(n, ultimo)} sem fazer gol. O último tinha sido em {Referencia(ultimo.Jogo)}.",
                        60 + Math.Min(n * 2, 30), null, casa, Chave: $"gol_time:{timeId}");
            }

            if (golsPro > golsContra)
            {
                // Voltou a vencer, contando todas as competições — a versão do
                // campeonato usa a mesma chave e fica a mais relevante.
                var (n, ultimo) = Jejum(jogos, j => j.GolsPro > j.GolsContra);
                if (n >= MinJogosJejumVitoria && ultimo != null)
                    yield return new Curiosidade("time", "Fim do jejum",
                        $"{nome} voltou a vencer depois de {Duracao(n, ultimo)}, contando todas as competições. A última vitória tinha sido em {Referencia(ultimo.Jogo)}.",
                        58 + Math.Min(n, 30), null, casa, Chave: $"vitoria:{timeId}");

                if (!casa)
                {
                    var (nf, ultimaFora) = Jejum(jogos.Where(j => !j.EmCasa), j => j.GolsPro > j.GolsContra);
                    if (nf >= MinJogosJejumVitoriaFora && ultimaFora != null)
                        yield return new Curiosidade("time", "Vitória fora de casa",
                            $"{nome} venceu fora de casa depois de {Duracao(nf, ultimaFora)} sem conseguir. A última tinha sido em {Referencia(ultimaFora.Jogo)}.",
                            55 + Math.Min(nf, 30), null, casa, Chave: $"vitoria_fora:{timeId}");
                }

                // Confronto direto: aqui "nunca venceu" vale, dito como "nos confrontos registrados".
                var (nc, ultimaSobre) = Jejum(jogos.Where(j => j.AdversarioId == adversarioId), j => j.GolsPro > j.GolsContra);
                if (nc >= MinConfrontosJejum)
                {
                    var texto = ultimaSobre != null
                        ? $"Primeira vitória de {nome} sobre {adversario} desde {DateHelper.FormatarData(ultimaSobre.Jogo.Data)}, depois de {nc} confrontos sem vencer."
                        : $"Primeira vitória de {nome} sobre {adversario} nos {nc + 1} confrontos registrados.";
                    yield return new Curiosidade("time", "Confronto direto", texto, 55 + Math.Min(nc * 2, 30), null, casa,
                        Chave: $"confronto:{timeId}", AmbosTimes: true);
                }
            }

            // Sequências em andamento, contando esta partida
            if (golsContra == 0)
            {
                int n = Jejum(jogos, j => j.GolsContra > 0).N + 1;
                if (n >= MinJogosSequenciaSemSofrer)
                    yield return new Curiosidade("time", "Sem sofrer gol",
                        $"{nome} chegou a {n} jogos seguidos sem sofrer gol, contando todas as competições.", 45 + Math.Min(n * 2, 30), null, casa,
                        Chave: $"seq_sem_sofrer:{timeId}");
            }
            if (golsPro == 0)
            {
                var (n0, ultimo) = Jejum(jogos, j => j.GolsPro > 0);
                int n = n0 + 1;
                if (n >= MinJogosJejumGols)
                    yield return new Curiosidade("time", "Jejum de gols",
                        $"{nome} chegou a {n} jogos seguidos sem marcar{(ultimo != null ? $"; o último gol foi em {Referencia(ultimo.Jogo)}" : "")}.",
                        45 + Math.Min(n * 2, 30), null, casa, Chave: $"seq_sem_marcar:{timeId}");
            }
        }

        // "21/01/2026, Chelsea 1 x 0 Pafos (Champions League)" — competição entre
        // parênteses porque o artigo muda ("pela Premier", "pelo Brasileirão").
        private static string Referencia(JogoTime j)
        {
            var r = $"{DateHelper.FormatarData(j.Data)}, {j.NomeCasa} {j.PlacarCasa} x {j.PlacarVisitante} {j.NomeVisitante}";
            return string.IsNullOrEmpty(j.Competicao) ? r : $"{r} ({j.Competicao})";
        }

        /// <summary>"9 meses", "quase 9 meses", "1 ano", "mais de 2 anos" — null abaixo
        /// de 2 meses, quando a contagem de jogos já diz tudo.</summary>
        internal static string? Periodo(DateTime? de, DateTime? ate)
        {
            if (de == null || ate == null) return null;
            DateTime a = de.Value, b = ate.Value;
            int meses = (b.Year - a.Year) * 12 + b.Month - a.Month;
            if (b.Day < a.Day) meses--;
            // 8 meses e 26 dias se diz "quase 9 meses".
            bool quase = meses >= 1 && meses < 12 && (b - a.AddMonths(meses)).TotalDays >= 20;
            if (quase) return meses + 1 == 12 ? "quase 1 ano" : $"quase {meses + 1} meses";
            if (meses < 2) return null;
            if (meses < 12) return $"{meses} meses";
            int anos = meses / 12;
            var t = anos == 1 ? "1 ano" : $"{anos} anos";
            return meses % 12 == 0 ? t : $"mais de {t}";
        }

        private static IEnumerable<Curiosidade> Sequencias(Entrada d, List<JogoResumo> anteriores, int timeId,
            string nome, string adversario, int golsPro, int golsContra, bool casa)
        {
            var historico = anteriores
                .Where(j => j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                .Select(j =>
                {
                    bool ehCasa = j.TimeCasaId == timeId;
                    int gp = (ehCasa ? j.PlacarCasa : j.PlacarVisitante)!.Value;
                    int gc = (ehCasa ? j.PlacarVisitante : j.PlacarCasa)!.Value;
                    return gp > gc ? 'V' : gp == gc ? 'E' : 'D';
                })
                .ToList();
            char atual = golsPro > golsContra ? 'V' : golsPro == golsContra ? 'E' : 'D';

            // Tamanho da sequência no fim da lista em que todo resultado está em "aceitos".
            static int Fim(List<char> l, string aceitos)
            {
                int n = 0;
                for (int i = l.Count - 1; i >= 0 && aceitos.Contains(l[i]); i--) n++;
                return n;
            }

            // Sequência que esta partida encerrou
            int vitoriasAntes = Fim(historico, "V");
            int invictoAntes = Fim(historico, "VE");
            int derrotasAntes = Fim(historico, "D");
            int semVencerAntes = Fim(historico, "ED");

            if (atual == 'V' && historico.Count >= 2 && !historico.Contains('V'))
            {
                yield return new Curiosidade("time", "Primeira vitória",
                    $"Primeira vitória de {nome} no campeonato, no {historico.Count + 1}º jogo.", 70, null, casa, Chave: $"vitoria:{timeId}");
            }
            else if (atual == 'V' && semVencerAntes >= 4)
            {
                yield return new Curiosidade("time", "Fim do jejum",
                    $"{nome} voltou a vencer depois de {semVencerAntes} jogos.", 55 + semVencerAntes, null, casa, Chave: $"vitoria:{timeId}");
            }

            if (atual == 'D' && invictoAntes >= 5)
                yield return new Curiosidade("time", "Fim da invencibilidade",
                    $"{adversario} acabou com a invencibilidade de {invictoAntes} jogos de {nome}.", 60 + invictoAntes, null, !casa, Chave: $"fim_sequencia:{timeId}", AmbosTimes: true);
            else if (atual != 'V' && vitoriasAntes >= 3)
                yield return new Curiosidade("time", "Fim da sequência",
                    $"{adversario} interrompeu a sequência de {vitoriasAntes} vitórias seguidas de {nome}.", 60 + vitoriasAntes, null, !casa, Chave: $"fim_sequencia:{timeId}", AmbosTimes: true);

            // Sequência em andamento, contando esta partida
            var comEste = new List<char>(historico) { atual };
            int vitorias = Fim(comEste, "V"), invicto = Fim(comEste, "VE");
            int derrotas = Fim(comEste, "D"), semVencer = Fim(comEste, "ED");

            if (vitorias >= 3)
                yield return new Curiosidade("time", "Sequência",
                    $"{nome} chegou a {vitorias} vitórias seguidas no campeonato.", 50 + vitorias, null, casa, Chave: $"sequencia:{timeId}");
            else if (invicto >= 5)
                yield return new Curiosidade("time", "Sequência",
                    $"{nome} chegou a {invicto} jogos sem perder no campeonato.", 45 + invicto, null, casa, Chave: $"sequencia:{timeId}");
            else if (derrotas >= 3)
                yield return new Curiosidade("time", "Sequência",
                    $"{nome} chegou a {derrotas} derrotas seguidas no campeonato.", 45 + derrotas, null, casa, Chave: $"sequencia:{timeId}");
            else if (semVencer >= 5)
                yield return new Curiosidade("time", "Sequência",
                    $"{nome} chegou a {semVencer} jogos sem vencer no campeonato.", 40 + semVencer, null, casa, Chave: $"sequencia:{timeId}");
        }
    }
}
