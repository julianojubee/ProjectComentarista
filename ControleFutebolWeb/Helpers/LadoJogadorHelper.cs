using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Como o jogador atuou NAQUELE jogo, segundo a escalação da época:
    /// de que lado (casa/visitante) e em que posição.
    /// </summary>
    /// <param name="Posicao">
    /// Posição granular derivada do slot da formação ("Meia Direita", "Zagueiro
    /// Central"...) ou, quando não dá pra derivar, o texto da escalação vindo da
    /// api-football ("Defensor", "Meia"...). Null = posição desconhecida nesse jogo.
    /// </param>
    public readonly record struct AtuacaoNoJogo(bool IsTimeCasa, string? Posicao);

    // Como cada jogador atuou em cada jogo, tirado da escalação da época.
    //
    // Usar dados do cadastro do jogador erra os dois casos que este helper existe
    // para cobrir: o time ATUAL inverte o placar de todos os jogos anteriores a uma
    // transferência, e a posição ATUAL (Jogador.Posicao, um agregado das posições
    // que ele mais joga) não é a posição daquela partida — um lateral escalado de
    // meia ganharia bônus de defensor.
    //
    // Consumido pelo bônus "não sofreu gol" (CriteriosNotaHelper) e disponível para
    // qualquer cálculo que precise da atuação histórica.
    public static class LadoJogadorHelper
    {
        /// <summary>Mapa vazio, para quem não tem escalação daquele jogo à mão.</summary>
        public static readonly IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> SemAtuacoes =
            new Dictionary<(int, int), AtuacaoNoJogo>();

        /// <summary>
        /// O jogador atuou pelo time da casa neste jogo? Responde pela escalação da época;
        /// o clube do cadastro (TimeId, ou SelecaoId em competição de seleções) é só o
        /// fallback de quem não aparece escalado.
        /// </summary>
        public static bool EhDoTimeDaCasa(
            Jogador? jogador, Jogo jogo,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> atuacoes)
        {
            if (jogador == null) return false;
            if (atuacoes.TryGetValue((jogador.Id, jogo.Id), out var atuacao)) return atuacao.IsTimeCasa;

            return jogador.TimeId == jogo.TimeCasaId || jogador.SelecaoId == jogo.TimeCasaId;
        }

        /// <summary>
        /// De que time do jogo é o autor de um evento (gol, cartão, assistência). Null
        /// quando nem a escalação nem o cadastro ligam o jogador a um dos dois times —
        /// evento sem dono, que as contagens por time devem ignorar em vez de chutar.
        /// </summary>
        public static int? TimeDoAutor(
            Jogo jogo, Jogador? autor,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> atuacoes)
        {
            if (autor == null) return null;
            if (atuacoes.TryGetValue((autor.Id, jogo.Id), out var atuacao))
                return atuacao.IsTimeCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;

            if (autor.TimeId == jogo.TimeCasaId || autor.SelecaoId == jogo.TimeCasaId)
                return jogo.TimeCasaId;
            if (autor.TimeId == jogo.TimeVisitanteId || autor.SelecaoId == jogo.TimeVisitanteId)
                return jogo.TimeVisitanteId;

            return null;
        }

        /// <summary>
        /// Escalações compartilhadas dos jogos pedidos, restritas aos jogadores que
        /// aparecem nos eventos a contar — as escalações inteiras de uma competição são
        /// milhares de linhas para resolver algumas centenas de gols e cartões. Devolve a
        /// consulta porque há telas montadas de forma síncrona. Contagem por time não
        /// depende de quem está olhando, então vai pela escalação compartilhada.
        /// </summary>
        public static IQueryable<Escalacao> EscalacoesDosEventos(
            FutebolContext context, IEnumerable<int> jogoIds, IEnumerable<int> jogadorIds) =>
            Consulta(context, jogoIds.Distinct().ToList(), null, jogadorIds.Distinct().ToList());

        /// <summary>
        /// Só o lado de cada (jogador, jogo) dos eventos informados — sem as consultas de
        /// formação/slot que <see cref="CarregarAsync"/> faz para descobrir a posição.
        /// </summary>
        public static async Task<Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo>> CarregarLadosAsync(
            FutebolContext context, IEnumerable<int> jogoIds, IEnumerable<int> jogadorIds,
            CancellationToken ct = default) =>
            Montar(await EscalacoesDosEventos(context, jogoIds, jogadorIds).ToListAsync(ct));

        public static Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> Montar(
            IEnumerable<Escalacao> escalacoes,
            IReadOnlyDictionary<int, List<PosicaoFormacao>>? slotsPorFormacao = null,
            IReadOnlyDictionary<int, (int? Casa, int? Visitante)>? formacoesPorJogo = null) =>
            escalacoes
                .Where(e => e.JogadorId.HasValue)
                .GroupBy(e => (e.JogadorId!.Value, e.JogoId))
                .ToDictionary(g => g.Key, g => Resolver(g.ToList(), slotsPorFormacao, formacoesPorJogo));

        private static AtuacaoNoJogo Resolver(
            List<Escalacao> doJogo,
            IReadOnlyDictionary<int, List<PosicaoFormacao>>? slotsPorFormacao,
            IReadOnlyDictionary<int, (int? Casa, int? Visitante)>? formacoesPorJogo)
        {
            // A escalação INICIAL é a que descreve a posição real: no registro FINAL
            // quem entrou no decorrer do jogo herda a coordenada de quem ele
            // substituiu (mesma ressalva do PosicaoJogadorHelper).
            bool EhInicial(Escalacao e) => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null;

            var escolhida = doJogo.FirstOrDefault(EhInicial) ?? doJogo[0];

            return new AtuacaoNoJogo(escolhida.IsTimeCasa, PosicaoDe(escolhida, EhInicial(escolhida),
                slotsPorFormacao, formacoesPorJogo));
        }

        private static string? PosicaoDe(
            Escalacao e,
            bool inicial,
            IReadOnlyDictionary<int, List<PosicaoFormacao>>? slotsPorFormacao,
            IReadOnlyDictionary<int, (int? Casa, int? Visitante)>? formacoesPorJogo)
        {
            if (inicial && slotsPorFormacao != null && formacoesPorJogo != null
                && formacoesPorJogo.TryGetValue(e.JogoId, out var formacoes))
            {
                var formacaoId = e.IsTimeCasa ? formacoes.Casa : formacoes.Visitante;
                if (formacaoId != null && slotsPorFormacao.TryGetValue(formacaoId.Value, out var slots))
                {
                    var granular = PosicaoJogadorHelper.PosicaoGranular(slots, e.PosicaoX, e.PosicaoY);
                    if (!string.IsNullOrWhiteSpace(granular)) return granular;
                }
            }

            // Fallback: o texto da escalação ("Defensor", "Meia", "Atacante", "Goleiro").
            // "RES" é marcador de reserva, não posição.
            return string.IsNullOrWhiteSpace(e.Posicao) || e.Posicao == "RES" ? null : e.Posicao;
        }

        /// <param name="jogadorIds">null = todos os jogadores dos jogos informados.</param>
        public static async Task<Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo>> CarregarAsync(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            IReadOnlyCollection<int>? jogadorIds = null,
            CancellationToken ct = default)
        {
            if (jogoIds.Count == 0) return new();

            var escalacoes = await Consulta(context, jogoIds, usuarioId, jogadorIds).ToListAsync(ct);
            var formacoes = await FormacoesPorJogoAsync(context, jogoIds, ct);
            var slots = await SlotsPorFormacaoAsync(context, formacoes, ct);

            return Montar(escalacoes, slots, formacoes);
        }

        public static Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> Carregar(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            IReadOnlyCollection<int>? jogadorIds = null)
        {
            if (jogoIds.Count == 0) return new();

            var escalacoes = Consulta(context, jogoIds, usuarioId, jogadorIds).ToList();
            var formacoes = FormacoesPorJogo(context, jogoIds);
            var slots = SlotsPorFormacao(context, formacoes);

            return Montar(escalacoes, slots, formacoes);
        }

        private static IQueryable<Escalacao> Consulta(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            IReadOnlyCollection<int>? jogadorIds)
        {
            // Sem filtro de Titular: quem entrou no decorrer do jogo também precisa
            // do lado (Resolver é que prefere a linha INICIAL para a posição).
            var query = context.Escalacoes
                .AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId.HasValue
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null));

            if (jogadorIds != null)
                query = query.Where(e => jogadorIds.Contains(e.JogadorId!.Value));

            // Escalação do próprio usuário na frente da compartilhada: é a que reflete
            // os ajustes dele para aquela partida.
            return query
                .OrderBy(e => e.UsuarioId == usuarioId ? 0 : 1)
                .Select(e => new Escalacao
                {
                    JogadorId = e.JogadorId,
                    JogoId = e.JogoId,
                    IsTimeCasa = e.IsTimeCasa,
                    Titular = e.Titular,
                    Posicao = e.Posicao,
                    PosicaoX = e.PosicaoX,
                    PosicaoY = e.PosicaoY,
                    FaseEscalacao = e.FaseEscalacao,
                });
        }

        private static IQueryable<Jogo> ConsultaFormacoes(FutebolContext context, IReadOnlyCollection<int> jogoIds) =>
            context.Jogos.AsNoTracking().Where(j => jogoIds.Contains(j.Id));

        private static Dictionary<int, (int? Casa, int? Visitante)> Mapear(IEnumerable<Jogo> jogos) =>
            jogos.ToDictionary(j => j.Id, j => (j.FormacaoCasaId, j.FormacaoVisitanteId));

        private static async Task<Dictionary<int, (int? Casa, int? Visitante)>> FormacoesPorJogoAsync(
            FutebolContext context, IReadOnlyCollection<int> jogoIds, CancellationToken ct) =>
            Mapear(await ConsultaFormacoes(context, jogoIds)
                .Select(j => new Jogo { Id = j.Id, FormacaoCasaId = j.FormacaoCasaId, FormacaoVisitanteId = j.FormacaoVisitanteId })
                .ToListAsync(ct));

        private static Dictionary<int, (int? Casa, int? Visitante)> FormacoesPorJogo(
            FutebolContext context, IReadOnlyCollection<int> jogoIds) =>
            Mapear(ConsultaFormacoes(context, jogoIds)
                .Select(j => new Jogo { Id = j.Id, FormacaoCasaId = j.FormacaoCasaId, FormacaoVisitanteId = j.FormacaoVisitanteId })
                .ToList());

        private static Dictionary<int, List<PosicaoFormacao>> Agrupar(IEnumerable<PosicaoFormacao> slots) =>
            slots.GroupBy(p => p.FormacaoId).ToDictionary(g => g.Key, g => g.ToList());

        private static async Task<Dictionary<int, List<PosicaoFormacao>>> SlotsPorFormacaoAsync(
            FutebolContext context,
            IReadOnlyDictionary<int, (int? Casa, int? Visitante)> formacoes,
            CancellationToken ct)
        {
            var ids = IdsDeFormacao(formacoes);
            if (ids.Count == 0) return new();
            return Agrupar(await context.PosicoesFormacao.AsNoTracking()
                .Where(p => ids.Contains(p.FormacaoId)).ToListAsync(ct));
        }

        private static Dictionary<int, List<PosicaoFormacao>> SlotsPorFormacao(
            FutebolContext context,
            IReadOnlyDictionary<int, (int? Casa, int? Visitante)> formacoes)
        {
            var ids = IdsDeFormacao(formacoes);
            if (ids.Count == 0) return new();
            return Agrupar(context.PosicoesFormacao.AsNoTracking()
                .Where(p => ids.Contains(p.FormacaoId)).ToList());
        }

        private static List<int> IdsDeFormacao(IReadOnlyDictionary<int, (int? Casa, int? Visitante)> formacoes) =>
            formacoes.Values
                .SelectMany(f => new[] { f.Casa, f.Visitante })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
    }
}
