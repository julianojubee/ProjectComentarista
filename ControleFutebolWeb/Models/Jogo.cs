using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public int Rodada {  get; set; }
        public DateTime Data { get; set; }
        public int PartidaApiId { get; set; } // ID da partida na API

        public int EventKey { get; set; }
        public int TimeCasaId { get; set; }
        [ValidateNever]

        public Time TimeCasa { get; set; }
        [ValidateNever]

        public int? PlacarCasa {  get; set; }
        [ValidateNever]

        public int? PlacarVisitante { get; set; }
        [ValidateNever]

        public int TimeVisitanteId { get; set; }
        [ValidateNever]

        public Time TimeVisitante { get; set; }

        public int? FormacaoCasaId { get; set; }

        [ValidateNever]
        public Formacao FormacaoCasa { get; set; }

        public int? FormacaoVisitanteId { get; set; }

        [ValidateNever]
        public Formacao FormacaoVisitante { get; set; }

        [ValidateNever]
        public ICollection<Escalacao> Escalacoes { get; set; }  // única lista

        [ValidateNever]
        public ICollection<Gol> Gols { get; set; }

        [ValidateNever]
        public ICollection<Cartao> Cartoes { get; set; } = new List<Cartao>();

        [ValidateNever]
        public int CompeticaoId { get; set; }

        public string? Grupo { get; set; }
        public string? Observacoes { get; set; }

        
        public string? Status { get; set; }
        


    }

}

