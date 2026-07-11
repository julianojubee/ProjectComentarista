using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    // Monta o "Match Up" de um time: última escalação titular registrada,
    // convertida para o campo único horizontal onde os dois times ficam de
    // frente um pro outro. Usado pela aba Match Up de /Relatorios e pela aba
    // Match-up do modal Pré-jogo em /Jogos/Analisar (só visual, nada é salvo).
    public static class MatchUpHelper
    {
        // Busca o jogo mais recente (maior Jogo.Data) em que o time tem titulares
        // registrados no lado correto (casa/visitante) e converte as coordenadas
        // do campo individual (Analisar) para o campo compartilhado do Match Up.
        public static async Task<MatchUpTimeViewModel?> MontarTimeAsync(FutebolContext context, int timeId, bool esquerda, string? usuarioId)
        {
            var escalacoesDoTime = await context.Escalacoes
                .AsNoTracking()
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(e => e.Jogador)
                .Where(e => e.Titular && e.JogadorId != null
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null)
                         && ((e.IsTimeCasa && e.Jogo.TimeCasaId == timeId)
                          || (!e.IsTimeCasa && e.Jogo.TimeVisitanteId == timeId)))
                .ToListAsync();

            if (!escalacoesDoTime.Any()) return null;

            // Jogo mais recente com titulares deste time (prioriza quem tem Data
            // preenchida; sem Data nenhuma, cai no maior Id como aproximação).
            var jogoMaisRecente = escalacoesDoTime.Any(e => e.Jogo.Data.HasValue)
                ? escalacoesDoTime.Where(e => e.Jogo.Data.HasValue).OrderByDescending(e => e.Jogo.Data).Select(e => e.Jogo).First()
                : escalacoesDoTime.OrderByDescending(e => e.JogoId).Select(e => e.Jogo).First();

            var escalacoesDoJogo = escalacoesDoTime.Where(e => e.JogoId == jogoMaisRecente.Id).ToList();

            // Fase INICIAL (ou sem fase informada); só usa FINAL se o time não tiver INICIAL neste jogo.
            var temInicial = escalacoesDoJogo.Any(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null);
            var escolhidas = escalacoesDoJogo
                .Where(e => temInicial ? (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null) : e.FaseEscalacao == "FINAL")
                // A mesma escalação pode existir duplicada: a linha "global" (UsuarioId
                // null, importada da API) e a do usuário (ajustada por ele). Fica UMA
                // por jogador, preferindo a do usuário.
                .GroupBy(e => e.JogadorId)
                .Select(g => g.OrderBy(e => e.UsuarioId == null ? 1 : 0).First())
                .ToList();

            bool eraCasa = timeId == jogoMaisRecente.TimeCasaId;
            var timeInfo = eraCasa ? jogoMaisRecente.TimeCasa : jogoMaisRecente.TimeVisitante;

            var jogadores = new List<MatchUpJogadorViewModel>();

            // (0,0) não é posição real (ver PosicaoJogadorHelper.PosicaoGranular) — trata
            // à parte, espalhando numa linha de meio-campo para não empilhar todo mundo
            // no mesmo ponto.
            var comCoordenada = escolhidas.Where(e => !(e.PosicaoX == 0 && e.PosicaoY == 0)).ToList();
            var semCoordenada = escolhidas.Where(e => e.PosicaoX == 0 && e.PosicaoY == 0).ToList();

            foreach (var e in comCoordenada)
            {
                var (x, y) = TransformarCoordenada(e.PosicaoX, e.PosicaoY, esquerda);
                jogadores.Add(new MatchUpJogadorViewModel { Jogador = e.Jogador, PosicaoX = x, PosicaoY = y, Posicao = e.Posicao });
            }

            int n = semCoordenada.Count;
            for (int i = 0; i < n; i++)
            {
                double oldX = n == 1 ? 50 : 15 + i * (70.0 / (n - 1));
                var (x, y) = TransformarCoordenada(oldX, 50, esquerda); // linha do meio-campo
                jogadores.Add(new MatchUpJogadorViewModel { Jogador = semCoordenada[i].Jogador, PosicaoX = x, PosicaoY = y, Posicao = semCoordenada[i].Posicao });
            }

            // Banco: demais jogadores do elenco (fora dos titulares exibidos). Para
            // seleções, o vínculo do jogador fica em SelecaoId (TimeId aponta pro clube).
            var titularesIds = jogadores.Select(x => x.Jogador.Id).ToHashSet();
            bool ehSelecao = timeInfo.EhSelecao;
            var elenco = await context.Jogadores
                .AsNoTracking()
                .Where(j => (j.TimeId == timeId || (ehSelecao && j.SelecaoId == timeId))
                         && !titularesIds.Contains(j.Id))
                .OrderBy(j => j.NumeroCamisa == null).ThenBy(j => j.NumeroCamisa).ThenBy(j => j.Nome)
                .ToListAsync();

            return new MatchUpTimeViewModel
            {
                Time = timeInfo,
                JogoOrigem = jogoMaisRecente,
                JogoOrigemEhCasa = eraCasa,
                Escalacao = jogadores,
                Elenco = elenco
            };
        }

        // Converte as coordenadas do campo individual (Analisar.cshtml: X = posição
        // lateral 0-100, Y = 0 no ataque/meio-campo adversário até 100 perto do
        // próprio gol — ver SeedData, Goleiro fica em Y=85 e Atacante em Y~20-25)
        // para o campo único HORIZONTAL do Match Up, onde os dois times ficam de
        // frente um pro outro: o time da esquerda ataca da esquerda pro centro,
        // o da direita ataca da direita pro centro (espelhado).
        //
        // Eixo X (0-100 no campo do Match Up): cada time ocupa metade do campo,
        // com o próprio gol na borda externa e o ataque perto da linha do meio (50).
        // Eixo Y: mantém a lateralidade original para o time da esquerda; o time da
        // direita é espelhado (como numa transmissão de TV, os dois se encaram).
        private static (double X, double Y) TransformarCoordenada(double oldX, double oldY, bool esquerda)
        {
            double novoX = esquerda ? (50 - oldY / 2) : (50 + oldY / 2);
            double novoY = esquerda ? oldX : (100 - oldX);
            // Comprime o eixo vertical para 10-90%: o slot é ancorado pelo centro
            // e tem sigla acima + nome abaixo do círculo — perto das bordas o
            // conteúdo seria cortado pelo overflow:hidden do campo.
            novoY = 10 + novoY * 0.80;
            return (novoX, novoY);
        }
    }
}
