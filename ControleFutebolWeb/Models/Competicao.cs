using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Competicao
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Regiao { get; set; }
        public string Tipo { get; set; }
        public ICollection<Jogo> Jogos { get; set; }
    }

}
