using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Descobre os títulos de cada time a partir dos jogos importados — não existe
    /// cadastro de campeões no banco. Duas formas de decisão são reconhecidas, com as
    /// mesmas regras que /Competicoes/Detalhes usa na tabela e no chaveamento:
    ///
    /// • mata-mata, jogo único ou fase de grupos com eliminatória: vencedor do confronto
    ///   da fase "Final", pelo agregado e, no empate, pelos pênaltis;
    /// • pontos corridos: líder da tabela, e só com a temporada inteira jogada — do
    ///   contrário o primeiro colocado da rodada 5 já apareceria com a taça.
    ///
    /// Na dúvida não há título: é melhor uma taça faltando do que um campeão inventado.
    /// </summary>
    public static class TitulosHelper
    {
        public sealed record Titulo(
            int CompeticaoId, string Competicao, string? LogoUrl, int Temporada, string TemporadaRotulo);

        /// <summary>
        /// Títulos de todos os times, indexados por TimeId e ordenados da temporada
        /// mais recente para a mais antiga.
        /// </summary>
        public static async Task<Dictionary<int, List<Titulo>>> PorTimeAsync(FutebolContext context)
        {
            var competicoes = await context.Competicoes.AsNoTracking()
                .Select(c => new { c.Id, c.Nome, c.LogoUrl, c.Tipo, c.CriteriosDesempate })
                .ToListAsync();

            // Times e jogos entram como projeção leve e viram entidades em memória: o
            // ClassificacaoCalculator e o ConfrontoViewModel — reaproveitados abaixo para
            // não duplicar as regras de tabela e de agregado/pênaltis — trabalham sobre
            // Jogo/Time, mas carregar as entidades inteiras do banco todo sairia caro.
            var times = (await context.Times.AsNoTracking()
                    .Select(t => new { t.Id, t.Nome })
                    .ToListAsync())
                .ToDictionary(t => t.Id, t => new Time { Id = t.Id, Nome = t.Nome });

            var jogos = (await context.Jogos.AsNoTracking()
                    .Select(j => new
                    {
                        j.Id, j.CompeticaoId, j.Temporada, j.Grupo, j.Data,
                        j.TimeCasaId, j.TimeVisitanteId,
                        j.PlacarCasa, j.PlacarVisitante, j.PenaltisCasa, j.PenaltisVisitante,
                    })
                    .ToListAsync())
                .Where(j => times.ContainsKey(j.TimeCasaId) && times.ContainsKey(j.TimeVisitanteId))
                .Select(j => new Jogo
                {
                    Id = j.Id,
                    CompeticaoId = j.CompeticaoId,
                    Temporada = j.Temporada,
                    Grupo = j.Grupo,
                    Data = j.Data,
                    TimeCasaId = j.TimeCasaId,
                    TimeVisitanteId = j.TimeVisitanteId,
                    TimeCasa = times[j.TimeCasaId],
                    TimeVisitante = times[j.TimeVisitanteId],
                    PlacarCasa = j.PlacarCasa,
                    PlacarVisitante = j.PlacarVisitante,
                    PenaltisCasa = j.PenaltisCasa,
                    PenaltisVisitante = j.PenaltisVisitante,
                })
                .ToList();

            var fasesPorCompeticao = (await context.CompeticaoFases.AsNoTracking()
                    .Select(f => new { f.CompeticaoId, f.Tipo, f.Ordem, f.Id })
                    .ToListAsync())
                .GroupBy(f => f.CompeticaoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(f => f.Tipo).ToList());

            var calendarioEuropeu = await TemporadaHelper.CompeticoesDeCalendarioEuropeuAsync(
                context, competicoes.Select(c => c.Id).ToList());

            var titulos = new Dictionary<int, List<Titulo>>();

            foreach (var temporadaComp in jogos.GroupBy(j => new { j.CompeticaoId, j.Temporada }))
            {
                var competicao = competicoes.FirstOrDefault(c => c.Id == temporadaComp.Key.CompeticaoId);
                if (competicao == null) continue;

                var campeaoId = Campeao(
                    temporadaComp.ToList(),
                    competicao.Tipo,
                    fasesPorCompeticao.GetValueOrDefault(competicao.Id) ?? new List<string>(),
                    CriteriosDesempateHelper.Parse(competicao.CriteriosDesempate));

                if (campeaoId == null) continue;

                if (!titulos.TryGetValue(campeaoId.Value, out var lista))
                    titulos[campeaoId.Value] = lista = new List<Titulo>();

                var temporada = temporadaComp.Key.Temporada;
                lista.Add(new Titulo(
                    competicao.Id, competicao.Nome, competicao.LogoUrl, temporada,
                    TemporadaHelper.Rotulo(temporada, calendarioEuropeu.Contains(competicao.Id))));
            }

            foreach (var lista in titulos.Values)
                lista.Sort((a, b) => b.Temporada != a.Temporada
                    ? b.Temporada.CompareTo(a.Temporada)
                    : string.Compare(a.Competicao, b.Competicao, StringComparison.CurrentCulture));

            return titulos;
        }

        /// <summary>Campeão de uma competição numa temporada, ou nulo quando não dá para afirmar.</summary>
        private static int? Campeao(
            List<Jogo> jogos, string tipo, IReadOnlyList<string> tiposDasFases, IReadOnlyList<string>? criterios)
        {
            // Quem decide o título: a última fase declarada, quando existem fases; senão o
            // tipo da competição. Numa liga de pontos corridos o título é sempre da tabela
            // mesmo havendo um round chamado "Final" — na Bundesliga esse round é o playoff
            // de rebaixamento, e o vencedor dele não é campeão de nada.
            var decisiva = tiposDasFases.Count > 0 ? tiposDasFases[^1] : tipo;

            if (FaseJogoClassifier.EhEliminatoria(decisiva) || decisiva == "GRUPOS")
                return VencedorDaFinal(jogos, decisiva);

            // Jogos de playoff não entram na tabela — mesmo recorte de /Competicoes/Detalhes.
            var doPontosCorridos = jogos
                .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                .ToList();

            if (doPontosCorridos.Count == 0) return null;

            var tabela = ClassificacaoCalculator.Calcular(doPontosCorridos, criterios: criterios);

            // Tabela fecha o campeonato quando a temporada inteira foi jogada: nada
            // pendente e turno completo — mesmo número de jogos para todo mundo e pelo
            // menos um confronto contra cada rival. Sem isso, o líder da rodada 5 (ou de
            // uma temporada importada pela metade) já apareceria com a taça.
            bool temporadaFechada =
                doPontosCorridos.All(j => j.PlacarCasa != null && j.PlacarVisitante != null)
                && tabela.Count >= 2
                && tabela.All(t => t.Jogos == tabela[0].Jogos)
                && tabela[0].Jogos >= tabela.Count - 1;

            if (temporadaFechada) return tabela[0].TimeId;

            // Sem tabela fechada, a final decide — é o caso da Champions, cadastrada como
            // pontos corridos (formato de liga) mas com o título entregue na final. Exige
            // que os dois finalistas estejam na tabela: o "Final" da Bundesliga é o playoff
            // de rebaixamento contra um clube da segunda divisão, que taça nenhuma dá.
            var idsDaTabela = tabela.Select(t => t.TimeId).ToHashSet();
            var vencedorDaFinal = VencedorDaFinal(jogos, tipo);
            if (vencedorDaFinal != null && FinalistasNaTabela(jogos, idsDaTabela))
                return vencedorDaFinal;

            return null;
        }

        private static bool FinalistasNaTabela(List<Jogo> jogos, HashSet<int> idsDaTabela)
            => jogos.Where(j => EhRoundDeFinal(j.Grupo))
                .All(j => idsDaTabela.Contains(j.TimeCasaId) && idsDaTabela.Contains(j.TimeVisitanteId));

        /// <summary>
        /// Vencedor do confronto que decide o título. Nulo quando não há final, quando
        /// ela ainda não terminou ou quando o round "Final" traz mais de um confronto
        /// (aí não dá para saber qual é a decisão).
        /// </summary>
        private static int? VencedorDaFinal(List<Jogo> jogos, string tipo)
        {
            var finais = jogos.Where(j => EhRoundDeFinal(j.Grupo)).ToList();

            // Competição decidida em partida única (Supercopa, Recopa) importada sem
            // round na api-football: a própria partida da temporada é a final.
            if (finais.Count == 0 && tipo == "JOGO_UNICO")
                finais = jogos.ToList();

            if (finais.Count == 0 || finais.Count > 2) return null;

            var ordenados = finais.OrderBy(j => j.Data).ToList();
            var timeA = ordenados[0].TimeCasaId;
            var timeB = ordenados[0].TimeVisitanteId;

            // Ida e volta têm de ser entre os mesmos dois times; se não são, o round
            // "Final" está agrupando decisões diferentes.
            if (ordenados.Count == 2 &&
                (ordenados[1].TimeCasaId != timeA && ordenados[1].TimeCasaId != timeB))
                return null;

            var confronto = new ConfrontoViewModel
            {
                JogoIda = ordenados[0],
                JogoVolta = ordenados.Count > 1 ? ordenados[1] : null,
                TimeA = ordenados[0].TimeCasa,
                TimeB = ordenados[0].TimeVisitante,
            };

            if (confronto.VenceA) return timeA;
            if (confronto.VenceB) return timeB;
            return null;
        }

        // Round que decide o título. "Semifinals", "Quarterfinals" e "3rd Place Final"
        // também contêm "final" e precisam ficar de fora.
        private static bool EhRoundDeFinal(string? grupo)
        {
            var nome = (grupo ?? "").Trim();
            if (!nome.Contains("final", StringComparison.OrdinalIgnoreCase)) return false;

            string[] outrasFases = { "semi", "quarter", "quartas", "3rd", "third", "terceiro" };
            return !outrasFases.Any(f => nome.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
    }
}
