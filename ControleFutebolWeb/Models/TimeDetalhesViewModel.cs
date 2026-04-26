namespace ControleFutebolWeb.Models
{

    public class TimeDetalhesViewModel
    {
        public Time Time { get; set; } // entidade do banco
        public List<Jogador> Elenco { get; set; } // jogadores vinculados ao time
        public List<Jogo> Jogos { get; set; } // todos os jogos do time
        public List<Jogo> JogosPassados { get; set; } // últimos jogos já realizados
        public List<Jogo> JogosFuturos { get; set; } // próximos jogos agendados
        public ICollection<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
        public IEnumerable<Formacao> Formacoes { get; set; } // lista de formações disponíveis

    }
}