using System.Text.Json.Serialization;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Jogador oferecido no dropdown de "@" e usado para transformar as menções
    // "@Nome" em links. O contrato JSON (id/nome) é o esperado por
    // wwwroot/js/mencao-jogador.js.
    public class JogadorMencaoViewModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";
    }
}
