

namespace ControleFutebolWeb.Helpers
{
    public static class FlagHelper
    {
        private static readonly Dictionary<string, string> Bandeiras = new()
        {
            // América do Sul
            { "brasil", "🇧🇷" },
            { "argentina", "🇦🇷" },
            { "uruguai", "🇺🇾" },
            { "chile", "🇨🇱" },
            { "paraguai", "🇵🇾" },
            { "bolívia", "🇧🇴" },
            { "peru", "🇵🇪" },
            { "equador", "🇪🇨" },
            { "colômbia", "🇨🇴" },
            { "venezuela", "🇻🇪" },
            { "guiana", "🇬🇾" },
            { "suriname", "🇸🇷" },

            // América do Norte e Central
            { "méxico", "🇲🇽" },
            { "estados unidos", "🇺🇸" },
            { "canadá", "🇨🇦" },
            { "panamá", "🇵🇦" },
            { "porto rico", "🇵🇷" },
            { "república dominicana", "🇩🇴" },
            { "costa rica", "🇨🇷" },
            { "guatemala", "🇬🇹" },
            { "nicarágua", "🇳🇮" },
            { "honduras", "🇭🇳" },
            { "cuba", "🇨🇺" },
            { "el salvador", "🇸🇻" },
            { "aruba", "🇦🇼" },

            // Europa
            { "frança", "🇫🇷" },
            { "alemanha", "🇩🇪" },
            { "itália", "🇮🇹" },
            { "espanha", "🇪🇸" },
            { "portugal", "🇵🇹" },
            { "inglaterra", "🇬🇧" },
            { "escócia", "🇬🇧" },
            { "país de gales", "🇬🇧" },
            { "irlanda", "🇮🇪" },
            { "irlanda do norte", "🇬🇧" },
            { "suécia", "🇸🇪" },
            { "noruega", "🇳🇴" },
            { "dinamarca", "🇩🇰" },
            { "finlândia", "🇫🇮" },
            { "holanda", "🇳🇱" },
            { "bélgica", "🇧🇪" },
            { "suíça", "🇨🇭" },
            { "áustria", "🇦🇹" },
            { "polônia", "🇵🇱" },
            { "hungria", "🇭🇺" },
            { "romênia", "🇷🇴" },
            { "bulgária", "🇧🇬" },
            { "república checa", "🇨🇿" },
            { "eslováquia", "🇸🇰" },
            { "eslovênia", "🇸🇮" },
            { "croácia", "🇭🇷" },
            { "bósnia-herzegovina", "🇧🇦" },
            { "sérvia", "🇷🇸" },
            { "montenegro", "🇲🇪" },
            { "macedônia do norte", "🇲🇰" },
            { "albânia", "🇦🇱" },
            { "grécia", "🇬🇷" },
            { "turquia", "🇹🇷" },
            { "chipre", "🇨🇾" },
            { "luxemburgo", "🇱🇺" },
            { "lituânia", "🇱🇹" },
            { "letônia", "🇱🇻" },
            { "estônia", "🇪🇪" },
            { "bielorrússia", "🇧🇾" },
            { "rússia", "🇷🇺" },
            { "geórgia", "🇬🇪" },
            { "armênia", "🇦🇲" },
            { "azerbaijão", "🇦🇿" },

            // África
            { "marrocos", "🇲🇦" },
            { "egito", "🇪🇬" },
            { "argélia", "🇩🇿" },
            { "tunísia", "🇹🇳" },
            { "líbia", "🇱🇾" },
            { "nigéria", "🇳🇬" },
            { "gana", "🇬🇭" },
            { "senegal", "🇸🇳" },
            { "camarões", "🇨🇲" },
            { "costa do marfim", "🇨🇮" },
            { "áfrica do sul", "🇿🇦" },
            { "moçambique", "🇲🇿" },
            { "guiné-bissau", "🇬🇼" },
            { "guiné equatorial", "🇬🇶" },
            { "república democrática do congo", "🇨🇩" },
            { "congo", "🇨🇬" },
            { "burkina faso", "🇧🇫" },
            { "mali", "🇲🇱" },
            { "gabão", "🇬🇦" },
            { "libéria", "🇱🇷" },
            { "gâmbia", "🇬🇲" },
            { "angola", "🇦🇴" },
            { "cabo verde", "🇨🇻" },

            // Ásia
            { "china", "🇨🇳" },
            { "japão", "🇯🇵" },
            { "coreia do sul", "🇰🇷" },
            { "irã", "🇮🇷" },
            { "israel", "🇮🇱" },
            { "uzbequistão", "🇺🇿" },
            { "cazaquistão", "🇰🇿" },
            { "síria", "🇸🇾" },
            { "malásia", "🇲🇾" },
            { "indonésia", "🇮🇩" },
            { "filipinas", "🇵🇭" },
            { "qatar", "🇶🇦" },
            { "emirados árabes unidos", "🇦🇪" },

            // Oceania
            { "austrália", "🇦🇺" },
            { "nova zelândia", "🇳🇿" }
        };

        public static string GetFlagEmoji(string pais)
        {
            if (string.IsNullOrWhiteSpace(pais)) return "";
            pais = pais.Trim().ToLowerInvariant();
            return Bandeiras.TryGetValue(pais, out var emoji) ? emoji : "";
        }
    }
}
