using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>Resultado de uma operação do módulo de campeonatos. Erro vai direto para a tela.</summary>
    public sealed record ResultadoCampeonato(bool Ok, string? Erro = null, int Partidas = 0)
    {
        public static ResultadoCampeonato Falha(string erro) => new(false, erro);
    }

    /// <summary>
    /// Acesso ao banco do módulo de campeonatos próprios: carrega o campeonato do
    /// dono, monta o painel (via CampeonatoPainelAdapter) e grava as partidas que o
    /// GeradorPartidasCampeonato gera. Toda leitura/escrita filtra pelo dono — o
    /// único caminho sem usuário é o do link público, que é só leitura.
    /// </summary>
    public class CampeonatoService
    {
        private readonly FutebolContext _context;

        public CampeonatoService(FutebolContext context)
        {
            _context = context;
        }

        private IQueryable<Campeonato> ComTudo() => _context.Campeonatos
            .Include(c => c.Fases)
            .Include(c => c.Participantes).ThenInclude(p => p.TimeProprio)
            .Include(c => c.Participantes).ThenInclude(p => p.Time)
            .Include(c => c.Partidas).ThenInclude(p => p.Eventos)
            .AsSplitQuery();

        public Task<Campeonato?> CarregarAsync(int campeonatoId, string usuarioId) =>
            ComTudo().FirstOrDefaultAsync(c => c.Id == campeonatoId && c.UsuarioId == usuarioId);

        public Task<Campeonato?> CarregarPublicoAsync(string token) =>
            ComTudo().AsNoTracking().FirstOrDefaultAsync(c => c.TokenPublico == token);

        public async Task<CompeticaoPainelBuilder.Painel?> MontarPainelAsync(int campeonatoId, string usuarioId)
        {
            var campeonato = await CarregarAsync(campeonatoId, usuarioId);
            return campeonato == null ? null : CampeonatoPainelAdapter.MontarPainel(campeonato);
        }

        /// <summary>
        /// Gera as partidas de uma fase. A primeira fase usa os participantes (na
        /// ordem de Semente, depois Ordem); as seguintes, os classificados da fase
        /// anterior — que precisa estar com todos os placares lançados.
        ///
        /// <paramref name="quantidadeGrupos"/> só é usado em fase de grupos sem grupos
        /// definidos: na primeira fase, se todo participante já tem Grupo (sorteio feito
        /// à mão), vale o sorteio; senão distribui por potes e grava o Grupo neles.
        /// </summary>
        public async Task<ResultadoCampeonato> GerarFaseAsync(
            int campeonatoId, int faseId, string usuarioId, int? quantidadeGrupos = null)
        {
            var campeonato = await CarregarAsync(campeonatoId, usuarioId);
            if (campeonato == null) return ResultadoCampeonato.Falha("Campeonato não encontrado.");

            var fases = campeonato.Fases.OrderBy(f => f.Ordem).ToList();
            var fase = fases.FirstOrDefault(f => f.Id == faseId);
            if (fase == null) return ResultadoCampeonato.Falha("Fase não encontrada.");
            if (campeonato.Partidas.Any(p => p.FaseId == faseId))
                return ResultadoCampeonato.Falha("As partidas desta fase já foram geradas.");

            var indice = fases.IndexOf(fase);
            List<int> entrantes;
            if (indice == 0)
            {
                entrantes = campeonato.Participantes
                    .OrderBy(p => p.Semente ?? int.MaxValue).ThenBy(p => p.Ordem).ThenBy(p => p.Id)
                    .Select(p => p.Id)
                    .ToList();
            }
            else
            {
                var anterior = fases[indice - 1];
                var classificados = Classificados(campeonato, anterior);
                if (!classificados.Ok) return ResultadoCampeonato.Falha(classificados.Erro!);
                entrantes = classificados.Ids;
            }

            if (entrantes.Count < 2) return ResultadoCampeonato.Falha("São precisos pelo menos dois participantes.");

            var rodadaInicial = (campeonato.Partidas.Any() ? campeonato.Partidas.Max(p => p.Rodada) : 0) + 1;
            List<PartidaCampeonato> partidas;

            switch (fase.Tipo)
            {
                case "PONTOS_CORRIDOS":
                    partidas = GeradorPartidasCampeonato.TodosContraTodos(entrantes, fase.IdaEVolta, rodadaInicial);
                    break;

                case "GRUPOS":
                    var grupos = MontarGrupos(campeonato, entrantes, indice == 0, quantidadeGrupos);
                    if (grupos == null)
                        return ResultadoCampeonato.Falha("Informe em quantos grupos dividir os participantes.");
                    if (grupos.Values.Any(g => g.Count < 2))
                        return ResultadoCampeonato.Falha("Cada grupo precisa de pelo menos dois participantes.");
                    partidas = GeradorPartidasCampeonato.Grupos(grupos, fase.IdaEVolta, rodadaInicial);
                    break;

                case "MATA_MATA":
                    partidas = GeradorPartidasCampeonato.MataMata(entrantes, fase.IdaEVolta, rodadaInicial);
                    break;

                default:
                    return ResultadoCampeonato.Falha($"Tipo de fase desconhecido: {fase.Tipo}.");
            }

            foreach (var partida in partidas)
            {
                partida.CampeonatoId = campeonato.Id;
                partida.FaseId = fase.Id;
                campeonato.Partidas.Add(partida);
            }

            campeonato.Status = StatusCampeonato.EmAndamento;
            await _context.SaveChangesAsync();
            return new ResultadoCampeonato(true, Partidas: partidas.Count);
        }

        /// <summary>
        /// O que muda depois de um placar salvo: os vencedores do mata-mata vão para a
        /// etapa seguinte e o campeonato encerra (ou reabre, se o placar foi apagado ou
        /// corrigido) conforme SituacaoCampeonatoHelper. Chamar depois de salvar
        /// qualquer placar.
        /// </summary>
        public async Task<ResultadoCampeonato> AtualizarAposResultadoAsync(int campeonatoId, string usuarioId)
        {
            var campeonato = await CarregarAsync(campeonatoId, usuarioId);
            if (campeonato == null) return ResultadoCampeonato.Falha("Campeonato não encontrado.");

            var alteradas = 0;
            foreach (var fase in campeonato.Fases.Where(f => f.Tipo == "MATA_MATA"))
                alteradas += GeradorPartidasCampeonato.AvancarVencedores(
                    campeonato.Partidas.Where(p => p.FaseId == fase.Id).ToList());

            // Depois do avanço: a final só tem os dois lados depois que as semis passam.
            var statusMudou = SituacaoCampeonatoHelper.AplicarStatus(campeonato, DateTime.UtcNow);

            if (alteradas > 0 || statusMudou) await _context.SaveChangesAsync();
            return new ResultadoCampeonato(true, Partidas: alteradas);
        }

        /// <summary>Campeonatos do usuário com a situação (encerrado? campeão?) de cada um.</summary>
        public async Task<List<(Campeonato Campeonato, SituacaoCampeonato Situacao)>> ListarAsync(string usuarioId)
        {
            var campeonatos = await ComTudo()
                .AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();

            return campeonatos
                .OrderBy(c => c.Status == StatusCampeonato.Encerrado)
                .ThenByDescending(c => c.CriadoEm)
                .Select(c => (c, SituacaoCampeonatoHelper.Avaliar(c)))
                .ToList();
        }

        private Dictionary<string, List<int>>? MontarGrupos(
            Campeonato campeonato, List<int> entrantes, bool primeiraFase, int? quantidadeGrupos)
        {
            if (primeiraFase && campeonato.Participantes.All(p => !string.IsNullOrWhiteSpace(p.Grupo)))
            {
                return campeonato.Participantes
                    .GroupBy(p => p.Grupo!.Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => entrantes.Where(id => g.Any(p => p.Id == id)).ToList());
            }

            if (quantidadeGrupos is not > 0) return null;

            var grupos = GeradorPartidasCampeonato.DistribuirEmGrupos(entrantes, quantidadeGrupos.Value);
            if (primeiraFase)
            {
                foreach (var (letra, ids) in grupos)
                    foreach (var p in campeonato.Participantes.Where(p => ids.Contains(p.Id)))
                        p.Grupo = letra;
            }
            return grupos;
        }

        private sealed record ClassificadosDaFase(bool Ok, List<int> Ids, string? Erro = null);

        /// <summary>
        /// Quem passa da fase anterior, já na ordem de força para a próxima: em
        /// pontos corridos, a ordem da tabela; em grupos, todos os primeiros (grupo A,
        /// B, C...), depois todos os segundos — o que, na chave do mata-mata, cruza
        /// 1º de um grupo com 2º de outro.
        /// </summary>
        private static ClassificadosDaFase Classificados(Campeonato campeonato, CampeonatoFase anterior)
        {
            var partidas = campeonato.Partidas.Where(p => p.FaseId == anterior.Id).ToList();
            if (partidas.Count == 0)
                return new(false, new(), $"As partidas da fase \"{anterior.Nome}\" ainda não foram geradas.");
            if (partidas.Any(p => p.PlacarCasa == null || p.PlacarVisitante == null))
                return new(false, new(), $"A fase \"{anterior.Nome}\" ainda tem partidas sem placar.");
            if (anterior.Classificados is not > 0)
                return new(false, new(), $"Defina quantos se classificam na fase \"{anterior.Nome}\".");

            var n = anterior.Classificados.Value;
            var detalhe = CampeonatoPainelAdapter.MontarFase(CampeonatoPainelAdapter.Converter(campeonato), anterior);

            return anterior.Tipo switch
            {
                "PONTOS_CORRIDOS" => new(true, detalhe.Classificacao.Take(n).Select(c => c.TimeId).ToList()),
                "GRUPOS" => new(true, Enumerable.Range(0, n)
                    .SelectMany(pos => detalhe.Grupos
                        .Where(g => g.Times.Count > pos)
                        .Select(g => g.Times[pos].TimeId))
                    .ToList()),
                _ => new(false, new(), "Não há fase depois de um mata-mata.")
            };
        }
    }
}
