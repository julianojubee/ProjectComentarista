using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers

{
    public static class FormacaoHelper
    {
        // Modelos de formação
        public static Dictionary<string, string[]> Modelos = new()
        {
            { "4-3-3", new[] { "Goleiro", "Defesa", "Defesa", "Defesa", "Defesa", "Meio", "Meio", "Meio", "Ataque", "Ataque", "Ataque" } },
            { "4-4-2", new[] { "Goleiro", "Defesa", "Defesa", "Defesa", "Defesa", "Meio", "Meio", "Meio", "Meio", "Ataque", "Ataque" } },
            { "3-5-2", new[] { "Goleiro", "Defesa", "Defesa", "Defesa", "Meio", "Meio", "Meio", "Meio", "Meio", "Ataque", "Ataque" } }
        };

        public static Dictionary<string, List<Escalacao>> Distribuir(IEnumerable<Escalacao> escalacoes, string formacao)
        {
            var modelo = Modelos[formacao];
            var jogadores = escalacoes.OrderBy(e => e.Posicao).ToList();
            var resultado = new Dictionary<string, List<Escalacao>>();

            for (int i = 0; i < modelo.Length && i < jogadores.Count; i++)
            {
                var faixa = PosicionamentoHelper.ObterFaixa(modelo[i]);
                if (!resultado.ContainsKey(faixa))
                    resultado[faixa] = new List<Escalacao>();

                resultado[faixa].Add(jogadores[i]);
            }

            return resultado;
        }
    }
}
