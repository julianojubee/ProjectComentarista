namespace ControleFutebolWeb.Models
{
    public class TransfermarktSincronizacaoLog
    {
        public int Id { get; set; }
        public Guid CicloId { get; set; }
        public DateTime Data { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Acao { get; set; } = string.Empty;
        public string? CompeticaoNome { get; set; }
        public string? TimeNome { get; set; }
        public string? JogoDescricao { get; set; }
        public string? Detalhes { get; set; }
    }
}
