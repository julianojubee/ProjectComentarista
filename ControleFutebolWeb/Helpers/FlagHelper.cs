

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
            { "haiti", "🇭🇹" },
            { "jamaica", "🇯🇲" },
            { "curaçao", "🇨🇼" },
            { "guadalupe", "🇬🇵" },
            { "martinica", "🇲🇶" },
            { "trinidad e tobago", "🇹🇹" },
            { "granada", "🇬🇩" },
            { "bermudas", "🇧🇲" },
            { "guiana francesa", "🇬🇫" },

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
            { "bósnia e herzegovina", "🇧🇦" },
            { "kosovo", "🇽🇰" },
            { "moldávia", "🇲🇩" },
            { "islândia", "🇮🇸" },
            { "malta", "🇲🇹" },
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
            { "guiné", "🇬🇳" },
            { "benin", "🇧🇯" },
            { "togo", "🇹🇬" },
            { "serra leoa", "🇸🇱" },
            { "mauritânia", "🇲🇷" },
            { "sudão", "🇸🇩" },
            { "quênia", "🇰🇪" },
            { "etiópia", "🇪🇹" },
            { "tanzânia", "🇹🇿" },
            { "uganda", "🇺🇬" },
            { "zâmbia", "🇿🇲" },
            { "zimbábue", "🇿🇼" },
            { "madagascar", "🇲🇬" },
            { "comores", "🇰🇲" },
            { "burundi", "🇧🇮" },
            { "níger", "🇳🇪" },
            { "chade", "🇹🇩" },
            { "malaui", "🇲🇼" },
            { "botsuana", "🇧🇼" },
            { "namíbia", "🇳🇦" },

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
            { "emirados árabes", "🇦🇪" },
            { "iraque", "🇮🇶" },
            { "jordânia", "🇯🇴" },
            { "arábia saudita", "🇸🇦" },
            { "líbano", "🇱🇧" },
            { "palestina", "🇵🇸" },
            { "bahrein", "🇧🇭" },
            { "kuwait", "🇰🇼" },
            { "omã", "🇴🇲" },
            { "iêmen", "🇾🇪" },
            { "índia", "🇮🇳" },
            { "tailândia", "🇹🇭" },
            { "vietnã", "🇻🇳" },
            { "coreia do norte", "🇰🇵" },
            { "taiwan", "🇹🇼" },
            { "hong kong", "🇭🇰" },
            { "singapura", "🇸🇬" },
            { "tadjiquistão", "🇹🇯" },
            { "quirguistão", "🇰🇬" },
            { "turcomenistão", "🇹🇲" },
            { "afeganistão", "🇦🇫" },
            { "paquistão", "🇵🇰" },

            // Oceania
            { "austrália", "🇦🇺" },
            { "nova zelândia", "🇳🇿" },
            { "fiji", "🇫🇯" },
            { "papua-nova guiné", "🇵🇬" },
            { "taiti", "🇵🇫" },
            { "nova caledônia", "🇳🇨" }
        };

        // Subdivisões do Reino Unido têm bandeira própria no flagcdn, mas o emoji
        // é o mesmo (🇬🇧) — resolvidas por nome antes da conversão emoji→ISO.
        private static readonly Dictionary<string, string> CodigosEspeciais = new()
        {
            { "inglaterra", "gb-eng" },
            { "escócia", "gb-sct" },
            { "país de gales", "gb-wls" },
            { "irlanda do norte", "gb-nir" },
        };

        public static string GetFlagEmoji(string pais)
        {
            if (string.IsNullOrWhiteSpace(pais)) return "";
            pais = pais.Trim().ToLowerInvariant();
            return Bandeiras.TryGetValue(pais, out var emoji) ? emoji : "";
        }

        /// <summary>
        /// URL de imagem da bandeira (flagcdn) para o país em português, ou null se
        /// desconhecido. O código ISO alpha-2 é derivado do próprio emoji (cada
        /// bandeira emoji é o par de "regional indicators" do código do país).
        /// Preferir isto a GetFlagEmoji em HTML: navegadores no Windows não
        /// renderizam emojis de bandeira (mostram só as letras do código).
        /// </summary>
        public static string? GetFlagImageUrl(string pais)
        {
            if (string.IsNullOrWhiteSpace(pais)) return null;
            var chave = pais.Trim().ToLowerInvariant();

            if (CodigosEspeciais.TryGetValue(chave, out var especial))
                return $"https://flagcdn.com/h24/{especial}.png";

            var emoji = GetFlagEmoji(chave);
            if (string.IsNullOrEmpty(emoji)) return null;

            var letras = emoji.EnumerateRunes()
                .Where(r => r.Value >= 0x1F1E6 && r.Value <= 0x1F1FF)
                .Select(r => (char)('a' + (r.Value - 0x1F1E6)))
                .ToArray();

            return letras.Length == 2 ? $"https://flagcdn.com/h24/{new string(letras)}.png" : null;
        }
    }
}
