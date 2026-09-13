using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>Uma linha agregada, pronta para virar estatística, e o aviso que ela carrega.</summary>
    /// <param name="Estatistica">A linha somada a partir das marcações.</param>
    /// <param name="MinutosAssumidos">Os minutos foram um palpite (jogo inteiro) por
    /// falta de marcação de entrada/saída — ver <see cref="MarcacaoVideoService"/>.</param>
    public sealed record LinhaAgregada(EstatisticaJogador Estatistica, bool MinutosAssumidos);

    /// <param name="Gravadas">Linhas escritas em EstatisticasJogador.</param>
    /// <param name="Puladas">Jogadores que já tinham estatística de outra fonte e foram preservados.</param>
    /// <param name="MinutosAssumidos">Nomes de quem ficou com minutos por palpite.</param>
    public sealed record ResultadoConsolidacao(
        int Gravadas, IReadOnlyList<string> Puladas, IReadOnlyList<string> MinutosAssumidos);

    /// <summary>
    /// Transforma as marcações feitas sobre o vídeo (<see cref="MarcacaoVideo"/>) na
    /// linha de <see cref="EstatisticaJogador"/> que o resto do sistema já sabe ler —
    /// é o ponto em que o trabalho manual entra no mesmo trilho das fontes importadas
    /// e passa a gerar nota automática (RatingAutomaticoService) sem nenhum caminho
    /// especial.
    ///
    /// A agregação é só uma soma: cada marcação aplica o incremento que o catálogo
    /// (<see cref="AcoesMarcacaoVideo"/>) define para a tecla. A parte que exige
    /// decisão é MINUTOS, e ela importa muito: o rating divide quase tudo por minuto
    /// jogado, então errar o divisor distorce a nota inteira. A regra é:
    ///
    ///   entrada = minuto da marcação "entrou", ou 0 para quem começou jogando;
    ///   saída   = minuto da marcação "saiu",   ou o fim do jogo;
    ///   duração = 90, ou o maior minuto marcado quando passou disso (prorrogação).
    ///
    /// Quem não é titular na escalação e não teve entrada marcada fica com o jogo
    /// inteiro — e é DEVOLVIDO na lista MinutosAssumidos, para a tela dizer de quem
    /// os minutos são palpite. Assumir o jogo inteiro erra para o lado de diluir a
    /// nota (mais minutos, menos ação por minuto); o contrário inflaria a nota de
    /// quem entrou no fim, que é o erro pior.
    /// </summary>
    public class MarcacaoVideoService
    {
        private const int DuracaoPadrao = 90;

        private readonly FutebolContext _context;

        public MarcacaoVideoService(FutebolContext context) => _context = context;

        /// <summary>
        /// Soma as marcações em linhas estatísticas, uma por jogador. Puro: não toca
        /// no banco, para a prévia da tela e a gravação usarem exatamente o mesmo
        /// cálculo e nunca divergirem.
        /// </summary>
        /// <param name="marcacoes">Marcações do jogo, de um usuário só.</param>
        /// <param name="titulares">Quem começou jogando (escalação INICIAL do usuário).</param>
        public static IReadOnlyList<LinhaAgregada> Agregar(
            IEnumerable<MarcacaoVideo> marcacoes, IReadOnlySet<int> titulares)
        {
            var lista = marcacoes.ToList();
            if (lista.Count == 0) return Array.Empty<LinhaAgregada>();

            // Prorrogação: se alguém marcou algo depois dos 90, o jogo durou mais.
            var duracao = Math.Max(DuracaoPadrao, lista.Max(m => m.MinutoJogo ?? 0));

            var resultado = new List<LinhaAgregada>();

            foreach (var grupo in lista.GroupBy(m => m.JogadorId))
            {
                var e = new EstatisticaJogador
                {
                    JogoId = grupo.First().JogoId,
                    JogadorId = grupo.Key,
                    Fonte = FonteEstatistica.Video,
                };

                foreach (var m in grupo)
                    AcoesMarcacaoVideo.PorId(m.AcaoId)?.Aplicar?.Invoke(e);

                // Primeira entrada e última saída: quem marcou entrada ou saída duas
                // vezes por engano ainda tem uma janela de minutos coerente.
                var entrouEm = grupo
                    .Where(m => m.AcaoId == AcoesMarcacaoVideo.Entrou && m.MinutoJogo != null)
                    .Select(m => m.MinutoJogo!.Value).DefaultIfEmpty(-1).Min();
                var saiuEm = grupo
                    .Where(m => m.AcaoId == AcoesMarcacaoVideo.Saiu && m.MinutoJogo != null)
                    .Select(m => m.MinutoJogo!.Value).DefaultIfEmpty(-1).Max();

                var titular = titulares.Contains(grupo.Key);
                var entrada = entrouEm >= 0 ? entrouEm : 0;
                var saida = saiuEm >= 0 ? saiuEm : duracao;

                e.EntrouDoBanco = entrouEm >= 0 || !titular;
                e.Minutos = Math.Clamp(saida - entrada, 1, duracao);

                // Palpite: não era titular, ninguém marcou quando ela entrou, e mesmo
                // assim ficou com o jogo inteiro.
                resultado.Add(new LinhaAgregada(e, !titular && entrouEm < 0));
            }

            return resultado;
        }

        /// <summary>Marcações de um jogo feitas por um usuário, da primeira à última.</summary>
        public Task<List<MarcacaoVideo>> ListarAsync(int jogoId, string usuarioId, CancellationToken ct = default) =>
            _context.MarcacoesVideo.AsNoTracking()
                .Where(m => m.JogoId == jogoId && m.UsuarioId == usuarioId)
                .OrderBy(m => m.SegundoVideo).ThenBy(m => m.Id)
                .ToListAsync(ct);

        /// <summary>Quem o usuário escalou como titular neste jogo (fase INICIAL).</summary>
        public async Task<HashSet<int>> TitularesAsync(int jogoId, string usuarioId, CancellationToken ct = default) =>
            (await _context.Escalacoes.AsNoTracking()
                .Where(esc => esc.JogoId == jogoId && esc.UsuarioId == usuarioId
                           && esc.FaseEscalacao == "INICIAL" && esc.Titular && esc.JogadorId != null)
                .Select(esc => esc.JogadorId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        /// <summary>Prévia do que a consolidação gravaria, sem gravar.</summary>
        public async Task<IReadOnlyList<LinhaAgregada>> PreviaAsync(
            int jogoId, string usuarioId, CancellationToken ct = default) =>
            Agregar(await ListarAsync(jogoId, usuarioId, ct), await TitularesAsync(jogoId, usuarioId, ct));

        /// <summary>
        /// Grava as marcações do usuário como estatísticas do jogo. As linhas de
        /// vídeo anteriores são substituídas (consolidar de novo depois de corrigir
        /// marcações é o fluxo normal, não um caso raro).
        /// </summary>
        /// <param name="sobrescreverOutrasFontes">
        /// Por padrão, jogador que já tem estatística importada de uma API é
        /// preservado e volta na lista de pulados: o dado da fonte é mais completo
        /// que a marcação a olho, e apagá-lo por engano custaria caro. Com true, a
        /// marcação manda.
        /// </param>
        public async Task<ResultadoConsolidacao> ConsolidarAsync(
            int jogoId, string usuarioId, bool sobrescreverOutrasFontes, CancellationToken ct = default)
        {
            var linhas = await PreviaAsync(jogoId, usuarioId, ct);
            if (linhas.Count == 0)
                return new ResultadoConsolidacao(0, Array.Empty<string>(), Array.Empty<string>());

            var existentes = await _context.EstatisticasJogador
                .Where(e => e.JogoId == jogoId)
                .ToListAsync(ct);

            var ids = linhas.Select(l => l.Estatistica.JogadorId).ToList();
            var nomes = await _context.Jogadores.AsNoTracking()
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome, ct);

            string Nome(int id) => nomes.GetValueOrDefault(id, $"#{id}");

            var puladas = new List<string>();
            var minutosAssumidos = new List<string>();
            var gravadas = 0;

            foreach (var linha in linhas)
            {
                var jogadorId = linha.Estatistica.JogadorId;
                var doJogador = existentes.Where(e => e.JogadorId == jogadorId).ToList();

                if (doJogador.Any(e => e.Fonte != FonteEstatistica.Video) && !sobrescreverOutrasFontes)
                {
                    puladas.Add(Nome(jogadorId));
                    continue;
                }

                // Reconsolidação: as linhas antigas saem inteiras em vez de serem
                // atualizadas campo a campo — marcação apagada tem que sumir da soma.
                _context.EstatisticasJogador.RemoveRange(doJogador);
                _context.EstatisticasJogador.Add(linha.Estatistica);
                gravadas++;

                if (linha.MinutosAssumidos) minutosAssumidos.Add(Nome(jogadorId));
            }

            await _context.SaveChangesAsync(ct);
            return new ResultadoConsolidacao(gravadas, puladas, minutosAssumidos);
        }
    }
}
