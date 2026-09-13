namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Quantas vezes cada ferramenta pública (/creators) foi aberta em um dia.
    ///
    /// É um CONTADOR AGREGADO, não um registro de visita: uma linha por
    /// (ferramenta, dia), sem IP, sem user-agent, sem nada que identifique quem
    /// entrou. O que se quer saber é "está entrando alguém, e em quê?" — e essa
    /// pergunta não precisa de dado pessoal nenhum.
    ///
    /// Alimentado pelo AcessoPublicoFilter; lido em /Admin/Acessos.
    /// </summary>
    public class AcessoPublico
    {
        public int Id { get; set; }

        /// <summary>Ferramenta aberta: "hub", "escalacao", "selecao", "tabelas", "jogos-hoje", "simulador".</summary>
        public string Ferramenta { get; set; } = string.Empty;

        /// <summary>
        /// Dia no calendário do Brasil (UTC-3) — é assim que o dono do site pensa
        /// o movimento do dia. DateOnly (coluna date) fica fora da conversão para
        /// UTC que o contexto aplica em DateTime.
        /// </summary>
        public DateOnly Dia { get; set; }

        /// <summary>Aberturas de página no dia (a mesma pessoa recarregando soma).</summary>
        public int Visitas { get; set; }

        /// <summary>
        /// Aproximação de "quantas pessoas": a primeira visita de cada
        /// navegador/IP no dia (ver AcessoPublicoService — a memória do
        /// deduplicador é do processo, então reiniciar o app pode contar alguém
        /// duas vezes no mesmo dia).
        /// </summary>
        public int Visitantes { get; set; }
    }
}
