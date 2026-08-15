namespace ControleFutebolWeb.Helpers
{
    // Categorias das anotações de treinador. Ficam num só lugar porque três telas
    // dependem da mesma lista (chips e resumo em /AnotacoesTreinador, combo do
    // formulário e o resumo no /Treinadores/Details) — se divergirem, um chip
    // passa a contar uma categoria que o formulário nem oferece.
    public static class CategoriaAnotacaoTreinador
    {
        public static readonly string[] Todas =
            { "Estilo de Jogo", "Contrato", "Desempenho", "Curiosidade", "Informação" };
    }
}
