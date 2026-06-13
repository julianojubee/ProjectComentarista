namespace ControleFutebolWeb.Services
{
    public class TransfermarktPlayerInfo
    {
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? NomeCompleto { get; set; }
        public string? Clube { get; set; }
        public string? Posicao { get; set; }
        public int? NumeroCamisa { get; set; }
        public string? LinkPerfil { get; set; }
    }

    public class JogoTM
    {
        public string NomeTimeCasa { get; set; } = "";
        public string NomeTimeVisitante { get; set; } = "";
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public DateTime? Data { get; set; }
        public string Grupo { get; set; } = "";
        public int Rodada { get; set; }
        public string LinkDetalhes { get; set; } = "";
    }

    public class DetalhesJogoTM
    {
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public string? FormacaoCasa { get; set; }
        public string? FormacaoVisitante { get; set; }
        public string? TreinadorCasaNome { get; set; }
        public string? TreinadorCasaLink { get; set; }
        public string? TreinadorVisitanteNome { get; set; }
        public string? TreinadorVisitanteLink { get; set; }

        public List<JogadorEscalacaoTM> EscalacaoInicialCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoInicialVisitante { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalVisitante { get; set; } = new();

        public List<GolTM> Gols { get; set; } = new();
        public List<TransfermarktEventoInfo> Eventos { get; set; } = new();
    }

    public record JogadorEscalacaoTM
    {
        public string Nome { get; init; } = "";
        public int? Numero { get; init; }
        public string Posicao { get; init; } = "";
        public bool Titular { get; init; }
        public long? IdExterno { get; init; }
        public string Fase { get; init; } = "INICIAL";
        public string? JogadorLink { get; set; }
        public string? FotoUrl { get; set; }
        public float TopPct { get; set; } = 0;
        public float LeftPct { get; set; } = 0;
    }

    public record InfoPerfilJogadorTM(string? Posicao, string? Nacionalidade, string? FotoUrl, DateTime? DataNascimento);

    public class GolTM
    {
        public string NomeJogador { get; set; } = "";
        public long? IdExterno { get; set; }
        public int Minuto { get; set; }
        public bool IsTimeCasa { get; set; }
        public bool Contra { get; set; }
    }

    public class SubstituicaoTM
    {
        public string NomeEntrou { get; set; } = "";
        public long? IdEntrou { get; set; }
        public string NomeSaiu { get; set; } = "";
        public long? IdSaiu { get; set; }
    }

    public class SincronizacaoResultado
    {
        public int JogosEncontradosNaSite { get; set; }
        public int JogosAtualizados { get; set; }
        public int JogosNaoEncontrados { get; set; }
        public int PlacaresAtualizados { get; set; }
        public int EscalacoesImportadas { get; set; }
        public int GolsImportados { get; set; }
        public List<string> Avisos { get; } = new();

        public override string ToString() =>
            $"Site:{JogosEncontradosNaSite} | Atualizados:{JogosAtualizados} | " +
            $"Placares:{PlacaresAtualizados} | Escalações:{EscalacoesImportadas} | " +
            $"Gols:{GolsImportados} | NãoEncontrados:{JogosNaoEncontrados} | " +
            $"Avisos:{Avisos.Count}";
    }

    public class TransfermarktEventoInfo
    {
        public string Tipo { get; set; } = string.Empty;
        public int JogadorId { get; set; }
        public int Minuto { get; set; }
        public bool Contra { get; set; }
        public int? AssistenteId { get; set; }
        public string? Detalhe { get; set; }
        public string? JogadorNome { get; set; }
        public string? JogadorLink { get; set; }
        public string? AssistenteNome { get; set; }
        public string? AssistenteLink { get; set; }
        public bool IsTimeCasa { get; set; }
    }

    public class TransfermarktJogoInfo
    {
        public string NomeTimeCasa { get; set; } = string.Empty;
        public string NomeTimeVisitante { get; set; } = string.Empty;
        public string? LinkTimeCasa { get; set; }
        public string? LinkTimeVisitante { get; set; }
        public string? LinkTimeCasaComEdicao { get; set; }
        public string? LinkTimeVisitanteComEdicao { get; set; }
        public string? EscudoTimeCasa { get; set; }
        public string? EscudoTimeVisitante { get; set; }
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public DateTime? Data { get; set; }
        public int Rodada { get; set; }
        public string? Grupo { get; set; }
        public string? LinkDetalhes { get; set; }
        public string? FormacaoCasa { get; set; }
        public string? FormacaoVisitante { get; set; }
        public List<TransfermarktEventoInfo> Eventos { get; set; } = new();
    }
}
