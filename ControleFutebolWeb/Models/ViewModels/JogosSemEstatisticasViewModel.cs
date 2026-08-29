namespace ControleFutebolWeb.Models.ViewModels
{
    // Tela /Servicos/JogosSemEstatisticas: jogos encerrados que ficaram sem os dados
    // da api-football e são candidatos ao preenchimento por uma das fontes de reserva
    // (ESPN e, para o que ela não cobre, FotMob).
    public class JogosSemEstatisticasViewModel
    {
        public List<JogoSemEstatisticaItem> Jogos { get; set; } = new();
        public List<CompeticaoSemEstatisticaItem> Competicoes { get; set; } = new();

        public int? CompeticaoId { get; set; }
        public int? Temporada { get; set; }
        public List<int> Temporadas { get; set; } = new();

        // Total no filtro atual, que pode ser maior que Jogos.Count (a listagem é limitada).
        public int Total { get; set; }
        public int LimiteLote { get; set; }
        public int LimiteLoteFotMob { get; set; }
    }

    public class JogoSemEstatisticaItem
    {
        public int Id { get; set; }
        public DateTime? Data { get; set; }
        public int CompeticaoId { get; set; }
        public string Competicao { get; set; } = "";
        public string TimeCasa { get; set; } = "";
        public string TimeVisitante { get; set; } = "";
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }

        public bool TemEstatisticasTime { get; set; }
        public bool TemEstatisticasJogador { get; set; }

        // Null quando a competição não tem correspondente na ESPN — a linha aparece
        // assim mesmo, para o buraco ficar visível em vez de sumir do relatório.
        public string? SlugEspn { get; set; }

        // A competição existe no FotMob? É a terceira fonte, oferecida principalmente
        // para as ligas que a ESPN não cataloga (a do Catar é o caso que motivou).
        public bool TemFotMob { get; set; }
    }

    public class CompeticaoSemEstatisticaItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public int Quantidade { get; set; }
        public bool TemEspn { get; set; }
        public bool TemFotMob { get; set; }
    }
}
