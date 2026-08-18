namespace ControleFutebolWeb.Models
{
    // Competição escolhida pelo usuário para entrar na tabela de classificação da home.
    // Independente do TopTier (CompeticaoTopTierUsuario), que só ordena os filtros:
    // aqui o critério é "quero ver esses pontos somados na home".
    public class CompeticaoHomeUsuario
    {
        public int Id { get; set; }

        public int CompeticaoId { get; set; }
        public Competicao Competicao { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;
    }
}
