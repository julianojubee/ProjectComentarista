namespace ControleFutebolWeb.Models.ViewModels
{
    // Uma linha dos rankings de jogador da competição (artilharia e assistências).
    // Genérico no "Valor" porque os dois rankings são a mesma lista com contagens
    // diferentes — ver Helpers.RankingCompeticaoHelper.
    public class RankingJogadorViewModel
    {
        public Jogador Jogador { get; set; } = null!;
        public int Valor { get; set; }
    }
}
