namespace ControleFutebolWeb.Models.Api
{
    public class CriterioNotaDto
    {
        public string AcaoId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Peso { get; set; }
        public int Ordem { get; set; }
    }

    public class AnaliseJogoDto
    {
        public int JogoId { get; set; }
        public bool AnalisadoPorMim { get; set; }
        public string? Observacoes { get; set; }
        public List<NotaJogadorDto> Notas { get; set; } = new();
        // Minutos e goleiro decisivo de cada jogador da partida: o app precisa deles
        // para calcular no rascunho a mesma nota que o servidor calcularia.
        public List<ContextoNotaDto> Contextos { get; set; } = new();
    }

    public class ContextoNotaDto
    {
        public int JogadorId { get; set; }
        public int? Minutos { get; set; }
        // Goleiro que defendeu 100% do que o adversário mandou no alvo.
        public bool GoleiroDecisivo { get; set; }
        // Autor do gol que decidiu a partida.
        public bool GolDaVitoria { get; set; }
    }

    public class NotaJogadorDto
    {
        public int JogadorId { get; set; }
        public double Total { get; set; }
        public double? NotaManual { get; set; }
        public string Comentario { get; set; } = string.Empty;
        // Manual quando informada; senão clamp(pesoInicial, 10, pesoInicial + Total).
        public double NotaFinal { get; set; }
        public List<NotaDetalheDto> Detalhes { get; set; } = new();
    }

    public class NotaDetalheDto
    {
        public string AcaoId { get; set; } = string.Empty;
        public string AcaoLabel { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Peso { get; set; }
    }

    public class SalvarNotaApiRequest
    {
        public int JogadorId { get; set; }
        public double Total { get; set; }
        public string? Observacao { get; set; }
        public double? NotaManual { get; set; }
        public List<NotaDetalheDto> Detalhes { get; set; } = new();
    }

    public class StatusAnaliseRequest
    {
        public bool Analisado { get; set; }
        public string? Observacoes { get; set; }
    }

    public class PreenchimentoDto
    {
        public bool Encontrado { get; set; }
        public int? Minutos { get; set; }
        public double? Rating { get; set; }
        // Só as ações com quantidade > 0 (mesmo espírito de ConstruirDetalhes)
        public Dictionary<string, int> QuantidadesPorAcao { get; set; } = new();
    }
}
