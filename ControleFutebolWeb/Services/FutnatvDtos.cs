using System.Text.Json.Serialization;

namespace ControleFutebolWeb.Services
{
    public class FutnatvResponse
    {
        [JsonPropertyName("schedule")]
        public List<FutnatvDia> Schedule { get; set; } = new();
    }

    public class FutnatvDia
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("games")]
        public List<FutnatvJogo> Games { get; set; } = new();
    }

    public class FutnatvJogo
    {
        [JsonPropertyName("sport")]
        public string? Sport { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("competition")]
        public string? Competition { get; set; }

        [JsonPropertyName("home")]
        public string Home { get; set; } = "";

        [JsonPropertyName("away")]
        public string Away { get; set; } = "";

        [JsonPropertyName("broadcast")]
        public string? Broadcast { get; set; }
    }

    // Resultado de uma chamada à API do futnatv, com o que é preciso pra decidir e pra logar.
    public record FutnatvResultado(bool Sucesso, string Url, List<FutnatvJogo> Jogos, string? Erro);
}
