using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Um gol pronto para exibir: de que lado do placar ele conta, quem fez e quem deu
    /// a assistência.
    /// </summary>
    /// <param name="EhCasa">
    /// Lado do PLACAR, não do autor: gol contra do zagueiro da casa vem com false,
    /// porque quem pontuou foi o visitante.
    /// </param>
    public record GolResumo(
        int Id, int Minuto, string NomeJogador, string? NomeAssistencia, bool Contra, bool EhCasa);

    /// <summary>
    /// A regra de "de quem é este gol e quem deu a assistência" — a mesma da timeline da
    /// tela Analisar (JogosEventosController.BuscarEventos), que é onde ela nasceu.
    ///
    /// Fica aqui fora porque a tela Jogos/Hoje mostra os mesmos gols nos cards e as duas
    /// precisam concordar: um gol contra que caísse para lados diferentes em cada tela
    /// deixaria os placares brigando entre si.
    /// </summary>
    public static class GolResumoHelper
    {
        /// <summary>
        /// Monta os gols do jogo em ordem de minuto. Espera as listas já materializadas
        /// com Gol.Jogador e Assistencia.Jogador carregados.
        /// </summary>
        /// <param name="escalacoes">
        /// Escalações daquele jogo, quando disponíveis: é delas que sai o lado em que o
        /// jogador atuou NAQUELA partida. Sem elas o lado volta a sair do clube atual do
        /// cadastro, que muda quando o jogador é transferido.
        /// </param>
        public static List<GolResumo> Montar(
            Jogo jogo, IEnumerable<Gol> gols, IEnumerable<Assistencia> assistencias,
            IEnumerable<Escalacao>? escalacoes = null)
        {
            var assists = assistencias.ToList();
            var atuacoes = LadoJogadorHelper.Montar(escalacoes ?? Array.Empty<Escalacao>());

            // Cada assistência serve a um gol só. Dois gols no mesmo minuto e do mesmo
            // lado (acontece quando a importação arredonda os acréscimos, ex.: 45+1 e
            // 45+3 viram os dois "45") pegavam ambos a primeira assistência, e o segundo
            // assistente sumia da timeline.
            var usadas = new HashSet<Assistencia>();
            var resumo = new List<GolResumo>();

            foreach (var g in gols.OrderBy(g => g.Minuto))
                resumo.Add(new GolResumo(
                    g.Id,
                    g.Minuto,
                    // Jogador.Nome é o nome que aparece no campinho da análise; qualquer
                    // outro campo faria a mesma pessoa ter dois nomes no sistema.
                    g.Jogador?.Nome ?? "?",
                    NomeDaAssistencia(g, assists, jogo, atuacoes, usadas),
                    g.Contra,
                    // Gol contra troca o lado: quem marcou no próprio gol pontuou para o
                    // adversário.
                    LadoJogadorHelper.EhDoTimeDaCasa(g.Jogador, jogo, atuacoes) != g.Contra));

            return resumo;
        }

        /// <summary>
        /// Assistência do gol: a importação não liga uma à outra por id, então o casamento
        /// é por minuto + mesmo lado do autor. Gol contra não tem assistência. A assistência
        /// escolhida entra em <paramref name="usadas"/> para não ser repetida em outro gol
        /// do mesmo minuto.
        /// </summary>
        private static string? NomeDaAssistencia(
            Gol gol, List<Assistencia> assistencias, Jogo jogo,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> atuacoes,
            HashSet<Assistencia> usadas)
        {
            if (gol.Contra || gol.Jogador == null) return null;

            var ladoDoAutor = LadoJogadorHelper.EhDoTimeDaCasa(gol.Jogador, jogo, atuacoes);

            var assistencia = assistencias.FirstOrDefault(
                a => a.Minuto == gol.Minuto && a.Jogador != null && !usadas.Contains(a)
                  && LadoJogadorHelper.EhDoTimeDaCasa(a.Jogador, jogo, atuacoes) == ladoDoAutor);

            if (assistencia == null) return null;

            usadas.Add(assistencia);
            return assistencia.Jogador!.Nome;
        }
    }
}
