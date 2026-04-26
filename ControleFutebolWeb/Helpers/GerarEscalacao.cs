using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;

public static class EscalacaoHelper
{
    public static List<Escalacao> GerarEscalacao(FutebolContext context, int formacaoId, List<Jogador> jogadores, bool isTimeCasa)
    {
        var posicoes = context.PosicoesFormacao
                              .Where(p => p.FormacaoId == formacaoId)
                              .OrderBy(p => p.Id)
                              .ToList();

        var escalacoes = new List<Escalacao>();

        for (int i = 0; i < jogadores.Count && i < posicoes.Count; i++)
        {
            var jogador = jogadores[i];
            var posicao = posicoes[i];

            double left;
            if (isTimeCasa)
            {
                // Casa ocupa de 0 a 50% (lado esquerdo)
                left = posicao.PosicaoY / 2.0;
            }
            else
            {
                // Visitante ocupa de 50 a 100% (lado direito), espelhando
                left = 100 - (posicao.PosicaoY / 2.0);
            }

            // Mantém eixo vertical
            var top = posicao.PosicaoX;

            escalacoes.Add(new Escalacao
            {
                JogadorId = jogador.Id,
                Posicao = posicao.NomePosicao,
                Titular = true,
                IsTimeCasa = isTimeCasa,
                PosicaoX = top,
                PosicaoY = left
            });
        }

        return escalacoes;
    }
}