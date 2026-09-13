using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>Placar imaginado para um jogo que ainda não aconteceu.</summary>
    public readonly record struct PalpiteSimulado(int PlacarCasa, int PlacarVisitante);

    /// <summary>
    /// Monta a tela do simulador de tabela: a classificação recalculada com os
    /// palpites e a rodada aberta para edição.
    ///
    /// Saiu do SimuladorController quando a área pública de creators ganhou o
    /// mesmo simulador. A diferença entre as duas telas é só a ORIGEM dos
    /// palpites — na tela logada eles vêm de SimulacoesJogoUsuario, na pública
    /// vivem no navegador do visitante e chegam na requisição —, então eles
    /// entram por parâmetro e o serviço nunca grava nada.
    /// </summary>
    public class SimuladorService
    {
        private readonly FutebolContext _context;

        public SimuladorService(FutebolContext context)
        {
            _context = context;
        }

        // ── Montagem ─────────────────────────────────────────────────────────

        /// <summary>
        /// Competições simuláveis: pontos corridos puros ou com uma fase declarada de
        /// pontos corridos (liga + playoffs), e que já tenham jogos importados.
        /// </summary>
        public async Task<List<Competicao>> CompeticoesElegiveisAsync()
        {
            return await _context.Competicoes
                .Include(c => c.Fases)
                .Where(c => (c.Tipo == "PONTOS_CORRIDOS" || c.Fases.Any(f => f.Tipo == "PONTOS_CORRIDOS"))
                            && c.Jogos.Any())
                .OrderByDescending(c => c.TopTier)
                .ThenBy(c => c.Nome)
                .ToListAsync();
        }

        public async Task<Competicao?> CompeticaoElegivelAsync(int id)
        {
            var competicao = await _context.Competicoes
                .Include(c => c.Fases)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (competicao == null) return null;

            var ehLiga = competicao.Tipo == "PONTOS_CORRIDOS"
                         || competicao.Fases.Any(f => f.Tipo == "PONTOS_CORRIDOS");

            return ehLiga ? competicao : null;
        }

        public async Task PreencherSimulacaoAsync(
            SimuladorViewModel vm, Competicao competicao, int? temporada, int? rodada,
            IReadOnlyDictionary<int, PalpiteSimulado> palpites, bool montarRodada = true)
        {
            vm.Competicao = competicao;

            vm.TemporadasDisponiveis = await _context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t)
                .ToListAsync();

            var temporadaSel = temporada
                ?? (vm.TemporadasDisponiveis.Count > 0 ? vm.TemporadasDisponiveis[0] : (int?)null);
            vm.Temporada = temporadaSel;

            // AsNoTracking porque os jogos pendentes recebem o placar do palpite só em
            // memória para alimentar o cálculo — nada disso pode voltar ao banco.
            var jogos = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicao.Id
                            && (temporadaSel == null || j.Temporada == temporadaSel))
                .ToListAsync();

            var jogosLiga = JogosDaFaseLiga(competicao, jogos);

            var realizados = jogosLiga
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();
            var pendentes = jogosLiga
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .ToList();


            // Todos os times da competição entram na tabela, mesmo sem jogo realizado —
            // no começo da temporada a tabela apareceria vazia sem isso.
            var participantes = jogosLiga
                .SelectMany(j => new[] { j.TimeCasa, j.TimeVisitante })
                .Where(t => t != null)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // Mesmos critérios de desempate da tela de Competições (Competicoes/Edit): sem
            // isso a ordem dos empatados sairia diferente da tabela oficial da competição.
            // Os cartões vêm só dos jogos realizados — jogo simulado não tem cartão.
            var criterios = CriteriosDesempateHelper.Parse(competicao.CriteriosDesempate);
            var idsRealizados = realizados.Select(j => j.Id).ToHashSet();
            var cartoes = await _context.Cartoes
                .AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => idsRealizados.Contains(c.JogoId))
                .ToListAsync();

            // O time de cada cartão sai da escalação daquele jogo — sem isso o fair play
            // cobraria de um clube o cartão que o jogador tomou por outro antes de ser
            // transferido.
            var escalacoesCartoes = await LadoJogadorHelper.EscalacoesDosEventos(
                _context, idsRealizados, cartoes.Select(c => c.JogadorId)).ToListAsync();

            var tabelaReal = ClassificacaoCalculator.Calcular(
                realizados, participantes, criterios, DadosDesempate.Construir(realizados, cartoes, escalacoesCartoes));

            // Jogos simulados: cópia rasa do jogo pendente com o placar do palpite.
            var jogosComSimulacao = new List<Jogo>(realizados);
            foreach (var jogo in pendentes)
            {
                if (!palpites.TryGetValue(jogo.Id, out var palpite)) continue;

                jogosComSimulacao.Add(new Jogo
                {
                    Id = jogo.Id,
                    TimeCasaId = jogo.TimeCasaId,
                    TimeCasa = jogo.TimeCasa,
                    TimeVisitanteId = jogo.TimeVisitanteId,
                    TimeVisitante = jogo.TimeVisitante,
                    PlacarCasa = palpite.PlacarCasa,
                    PlacarVisitante = palpite.PlacarVisitante,
                });
            }

            var tabelaSimulada = ClassificacaoCalculator.Calcular(
                jogosComSimulacao, participantes, criterios,
                DadosDesempate.Construir(jogosComSimulacao, cartoes, escalacoesCartoes));

            var posicaoReal = tabelaReal.ToDictionary(c => c.TimeId, c => c.Posicao);
            var pontosReais = tabelaReal.ToDictionary(c => c.TimeId, c => c.Pontos);

            vm.Classificacao = tabelaSimulada
                .Select(c => new SimuladorLinhaViewModel
                {
                    Linha = c,
                    PosicaoReal = posicaoReal.TryGetValue(c.TimeId, out var p) ? p : (int?)null,
                    PontosSimulados = c.Pontos - (pontosReais.TryGetValue(c.TimeId, out var pts) ? pts : 0),
                })
                .ToList();

            vm.TotalPendentes = pendentes.Count;
            vm.TotalSimulados = pendentes.Count(j => palpites.ContainsKey(j.Id));

            // Rodadas navegáveis: as que ainda têm jogo por simular.
            vm.Rodadas = pendentes
                .Select(j => j.Rodada)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            if (!montarRodada || vm.Rodadas.Count == 0) return;

            var rodadaSel = rodada.HasValue && vm.Rodadas.Contains(rodada.Value)
                ? rodada.Value
                : vm.Rodadas[0];
            vm.RodadaAtual = rodadaSel;

            var indice = vm.Rodadas.IndexOf(rodadaSel);

            // A rodada mostra também os jogos já realizados dela (só leitura), para o
            // usuário ver a rodada inteira e não só as sobras.
            var jogosRodada = jogosLiga
                .Where(j => j.Rodada == rodadaSel)
                .OrderBy(j => j.Data ?? DateTime.MaxValue)
                .ThenBy(j => j.Id)
                .Select(j =>
                {
                    var realizado = j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue;
                    var temPalpite = !realizado && palpites.TryGetValue(j.Id, out _);

                    return new SimuladorJogoViewModel
                    {
                        Jogo = j,
                        Realizado = realizado,
                        Simulado = temPalpite,
                        PlacarCasa = realizado ? j.PlacarCasa : (temPalpite ? palpites[j.Id].PlacarCasa : null),
                        PlacarVisitante = realizado ? j.PlacarVisitante : (temPalpite ? palpites[j.Id].PlacarVisitante : null),
                    };
                })
                .ToList();

            vm.Rodada = new SimuladorRodadaViewModel
            {
                Numero = rodadaSel,
                RodadaAnterior = indice > 0 ? vm.Rodadas[indice - 1] : null,
                RodadaProxima = indice < vm.Rodadas.Count - 1 ? vm.Rodadas[indice + 1] : null,
                Jogos = jogosRodada,
            };

        }

        /// <summary>
        /// Isola os jogos que valem pontos na tabela. Com fases declaradas, usa a fase
        /// de pontos corridos; sem elas, descarta só o que é claramente mata-mata
        /// (playoff de rebaixamento, final de série) — mesmo critério de Competicoes/Detalhes.
        /// </summary>
        private static List<Jogo> JogosDaFaseLiga(Competicao competicao, List<Jogo> jogos)
        {
            var fases = competicao.Fases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).ToList();
            var faseLiga = fases.FirstOrDefault(f => f.Tipo == "PONTOS_CORRIDOS");

            if (faseLiga != null)
                return FaseJogoClassifier.DistribuirPorFases(fases, jogos)[faseLiga.Id];

            return jogos
                .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                .ToList();
        }
    }
}
