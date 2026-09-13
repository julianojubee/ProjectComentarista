namespace ControleFutebolWeb.Helpers
{
    // Categorias das anotações de competição. Ficam num só lugar porque a mesma lista
    // alimenta os chips, o resumo e os combos de /AnotacoesCompeticao — se divergirem,
    // um chip passa a contar uma categoria que o formulário nem oferece.
    public static class CategoriaAnotacaoCompeticao
    {
        public static readonly string[] Todas =
            { "Regulamento", "Calendário", "Favoritos", "Curiosidade", "Informação" };
    }
}
