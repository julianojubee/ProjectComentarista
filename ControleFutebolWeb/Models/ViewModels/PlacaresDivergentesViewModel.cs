namespace ControleFutebolWeb.Models.ViewModels
{
    // Tela /Servicos/PlacaresDivergentes: jogos em que o placar gravado não fecha com os
    // gols cadastrados. São dois defeitos diferentes na mesma lista, distinguidos por
    // MotivoTotal — ver JogoPlacarDivergenteItem.
    public class PlacaresDivergentesViewModel
    {
        public List<JogoPlacarDivergenteItem> Jogos { get; set; } = new();
        public List<CompeticaoDivergenteItem> Competicoes { get; set; } = new();

        public int? CompeticaoId { get; set; }
        public int? Temporada { get; set; }
        public List<int> Temporadas { get; set; } = new();

        // Total no filtro atual, que pode ser maior que Jogos.Count (a listagem é limitada).
        public int Total { get; set; }
        public int TotalReimportaveis { get; set; }
        public int LimiteLote { get; set; }
    }

    public class JogoPlacarDivergenteItem
    {
        public int Id { get; set; }
        public DateTime? Data { get; set; }
        public int CompeticaoId { get; set; }
        public int Temporada { get; set; }
        public string Competicao { get; set; } = "";
        public string TimeCasa { get; set; } = "";
        public string TimeVisitante { get; set; } = "";

        public int PlacarCasa { get; set; }
        public int PlacarVisitante { get; set; }

        // Placar remontado a partir dos gols cadastrados, com o lado de cada autor tirado
        // da escalação daquele jogo (ver LadoJogadorHelper).
        public int GolsCasa { get; set; }
        public int GolsVisitante { get; set; }

        /// <summary>
        /// true = a soma dos gols cadastrados não bate com o placar: falta (ou sobra) gol
        /// no banco. false = a soma bate e só a divisão casa×visitante difere, o que
        /// aponta para o lado de algum gol estar errado — autor sem escalação no jogo, ou
        /// escalação gravada com o lado trocado.
        /// </summary>
        public bool MotivoTotal { get; set; }

        /// <summary>
        /// Tem gol depois dos 90 (prorrogação). A api-football às vezes deixa o placar do
        /// jogo no resultado do tempo normal e manda os gols da prorrogação nos eventos —
        /// aí a soma "sobra" sem que falte nada, e a divergência é da fonte.
        /// </summary>
        public bool TemProrrogacao { get; set; }

        // Sem escalação salva, o lado do gol cai no clube atual do cadastro do jogador —
        // o dado que a transferência estraga. Coluna para explicar a divergência.
        public bool TemEscalacao { get; set; }

        // Só quem tem LinkDetalhes "apifoot:{fixtureId}" pode ser reimportado.
        public bool PodeReimportar { get; set; }
    }

    public class CompeticaoDivergenteItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public int Quantidade { get; set; }
    }
}
