namespace ControleFutebolWeb.Helpers
{
    public class TimesSulamericanaHelper
    {
        public static readonly Dictionary<string, string> mapaTimesNomes = 
            new (StringComparer.OrdinalIgnoreCase)
        {
             // ── Carabobo FC ──────────────────────────────────────────
            { "Carabobo FC",                    "Carabobo FC" },
            { "Carabobo",                       "Carabobo FC" },

            // ── Caracas ──────────────────────────────────────────────
            { "Caracas FC",                     "Caracas" },
            { "Caracas",                        "Caracas" },

            // ── Nacional Asuncion ────────────────────────────────────
            { "Club Nacional Asunción",         "Nacional Asuncion" },
            { "Nacional Asunción",              "Nacional Asuncion" },
            { "Nacional Asuncion",              "Nacional Asuncion" },
            { "Club Nacional de Football",      "Nacional Asuncion" },

            // ── CR Vasco da Gama ─────────────────────────────────────
            { "CR Vasco da Gama",               "CR Vasco da Gama" },
            { "Vasco da Gama",                  "CR Vasco da Gama" },
            { "Vasco",                          "CR Vasco da Gama" },

            // ── São Paulo FC ─────────────────────────────────────────
            { "São Paulo FC",                   "São Paulo FC" },
            { "FC São Paulo",                   "São Paulo FC" },
            { "São Paulo",                      "São Paulo FC" },
            { "Sao Paulo FC",                   "São Paulo FC" },

            // ── Macara ───────────────────────────────────────────────
            { "CD Macarà",                     "Macara" },
            { "CD Macarã¡",                    "Macara" },
            { "CD Macarâ",                     "Macara" },
            { "CD Macar&#225;",                 "Macara" },
            { "CD Macarã",                      "Macara" },
            { "Macará",                         "Macara" },
            { "CD Macará",                      "Macara" },
            { "Macara",                         "Macara" },

            // ── Olimpia Asuncion ─────────────────────────────────────
            { "Olimpia Asunción",               "Olimpia Asuncion" },
            { "Olimpia AsunciÃ³n",              "Olimpia Asuncion" },
            { "Olimpia Asunci&#243;n",          "Olimpia Asuncion" },
            { "Club Olimpia",                   "Olimpia Asuncion" },
            { "Olimpia",                        "Olimpia Asuncion" },

            // ── Orense ───────────────────────────────────────────────
            { "Orenses SC",                     "Orense" },
            { "Orense SC",                      "Orense" },
            { "Orense",                         "Orense" },

            // ── Santos FC ────────────────────────────────────────────
            { "Santos FC",                      "Santos FC" },
            { "Santos",                         "Santos FC" },

            // ── Millonarios ──────────────────────────────────────────
            { "Millonarios FC",                 "Millonarios" },
            { "Millonarios Bogotá",             "Millonarios" },
            { "Millonarios BogotÃ¡",            "Millonarios" },
            { "Millonarios Bogot&#225;",        "Millonarios" },
            { "Millonarios",                    "Millonarios" },

            // ── America De Cali ──────────────────────────────────────
            { "CD América de Cali",             "America De Cali" },
            { "CD AmÃ©rica de Cali",            "America De Cali" },
            { "CD Am&#233;rica de Cali",        "America De Cali" },
            { "America de Cali",                "America De Cali" },
            { "América de Cali",                "America De Cali" },
            { "Atletico Bucaramanga",           "Bucaramanga" },  // estava errado antes

            // ── Palestino ────────────────────────────────────────────
            { "CD Palestino",                   "Palestino" },
            { "Palestino",                      "Palestino" },

            // ── Dep. Cuenca ──────────────────────────────────────────
            { "Deportivo Cuenca",               "Dep. Cuenca" },
            { "Dep. Cuenca",                    "Dep. Cuenca" },
            { "CD Deportivo Cuenca",            "Dep. Cuenca" },

            // ── FBC Melgar ───────────────────────────────────────────
            { "FBC Melgar",                     "FBC Melgar" },
            { "Melgar",                         "FBC Melgar" },

            // ── Juventud de Las Piedras ──────────────────────────────
            { "CA Juventud",                    "Juventud de Las Piedras" },
            { "Juventud",                       "Juventud de Las Piedras" },
            { "Juventud de Las Piedras",        "Juventud de Las Piedras" },

            // ── River Plate ──────────────────────────────────────────
            { "CA River Plate",                 "River Plate" },
            { "River Plate",                    "River Plate" },

            // ── Atl. Nacional ────────────────────────────────────────
            { "Atlético Nacional",              "Atl. Nacional" },
            { "AtlÃ©tico Nacional",             "Atl. Nacional" },
            { "Atl&#233;tico Nacional",         "Atl. Nacional" },
            { "Atletico Nacional",              "Atl. Nacional" },
            { "Atl. Nacional",                  "Atl. Nacional" },

            // ── Botafogo FR ──────────────────────────────────────────
            { "Botafogo FR",                    "Botafogo FR" },
            { "Botafogo Rio de Janeiro",        "Botafogo FR" },
            { "Botafogo",                       "Botafogo FR" },

            // ── Barra FC ─────────────────────────────────────────────
            { "Barra FC",                       "Barra FC" },
            { "Barra",                          "Barra FC" },

            // ── A. Italiano ──────────────────────────────────────────
            { "Audax Italiano",                 "A. Italiano" },
            { "A. Italiano",                    "A. Italiano" },
            { "Audax",                          "A. Italiano" },
            { "CD Cobresal",                    "Cobresal" },   // Cobresal entra aqui

            // ── Puerto Cabello ───────────────────────────────────────
            { "Academia Puerto Cabello",        "Puerto Cabello" },
            { "Puerto Cabello",                 "Puerto Cabello" },

            // ── Dep. Riestra ─────────────────────────────────────────
            { "Club Deportivo Riestra",         "Dep. Riestra" },
            { "CD Riestra",                     "Dep. Riestra" },
            { "Dep. Riestra",                   "Dep. Riestra" },

            // ── Cobresal ─────────────────────────────────────────────
            { "Cobresal",                       "Cobresal" },

            // ── Metropolitanos ───────────────────────────────────────
            { "Metropolitanos FC",              "Metropolitanos" },
            { "Metropolitanos",                 "Metropolitanos" },
            { "Monagas SC",                     "Metropolitanos" }, // confirme se é o mesmo

            // ── Recoleta ─────────────────────────────────────────────
            { "Club Deportivo Recoleta",        "Recoleta" },
            { "Recoleta FC",                    "Recoleta" },
            { "Recoleta",                       "Recoleta" },

            // ── CA Mineiro ───────────────────────────────────────────
            { "CA Mineiro",                     "CA Mineiro" },
            { "Atletico Mineiro",               "CA Mineiro" },
            { "Atlético Mineiro",               "CA Mineiro" },

            // ── Club Libertad Asuncion ───────────────────────────────
            { "Club Libertad",                  "Club Libertad Asuncion" },
            { "Libertad",                       "Club Libertad Asuncion" },
            { "Libertad FC",                    "Club Libertad Asuncion" },

            // ── Cienciano ────────────────────────────────────────────
            { "Club Cienciano",                 "Cienciano" },
            { "Cienciano",                      "Cienciano" },

            // ── Sportivo Trinidense ──────────────────────────────────
            { "Club Sportivo Trinidense",       "Sportivo Trinidense" },
            { "Sportivo Trinidense",            "Sportivo Trinidense" },

            // ── Deportivo Garcilaso ──────────────────────────────────
            { "Deportivo Garcilaso",            "Deportivo Garcilaso" },
            { "Garcilaso",                      "Deportivo Garcilaso" },

            // ── SA Bulo Bulo ─────────────────────────────────────────
            { "Club Independiente Petrolero",   "SA Bulo Bulo" },
            { "San Antonio Bulo Bulo",          "SA Bulo Bulo" },
            { "SA Bulo Bulo",                   "SA Bulo Bulo" },
            { "Bulo Bulo",                      "SA Bulo Bulo" },

            // ── RB Bragantino ────────────────────────────────────────
            { "RB Bragantino",                  "RB Bragantino" },
            { "Red Bull Bragantino",            "RB Bragantino" },
            { "Bragantino",                     "RB Bragantino" },

            // ── Alianza Atl. ─────────────────────────────────────────
            { "Alianza Atlético Sullana",       "Alianza Atl." },
            { "Alianza AtlÃ©tico Sullana",      "Alianza Atl." },
            { "Alianza Atl&#233;tico Sullana",  "Alianza Atl." },
            { "Alianza Atletico Sullana",       "Alianza Atl." },
            { "Alianza Atl.",                   "Alianza Atl." },

            // ── San Lorenzo ──────────────────────────────────────────
            { "CA San Lorenzo de Almagro",      "San Lorenzo" },
            { "Club Atlético San Lorenzo",      "San Lorenzo" },
            { "San Lorenzo",                    "San Lorenzo" },

            // ── Guabira ──────────────────────────────────────────────
            { "Club Deportivo Guabirá",         "Guabira" },
            { "Club Deportivo Guabir&#225;",    "Guabira" },
            { "Guabirá",                        "Guabira" },
            { "Guabira",                        "Guabira" },

            // ── Montevideo City ──────────────────────────────────────
            { "Montevideo City Torque",         "Montevideo City" },
            { "Mvd City Torque",                "Montevideo City" },
            { "Montevideo City",                "Montevideo City" },

            // ── Blooming ─────────────────────────────────────────────
            { "Blooming Santa Cruz",            "Blooming" },
            { "Blooming",                       "Blooming" },

            // ── Racing Montevideo ────────────────────────────────────
            { "Racing Club de Montevideo",      "Racing Montevideo" },
            { "Racing Montevideo",              "Racing Montevideo" },

            // ── Bucaramanga ──────────────────────────────────────────
            { "Bucaramanga",                    "Bucaramanga" },
            { "Club Atlético Bucaramanga",      "Bucaramanga" },

            // ── U. De Chile ──────────────────────────────────────────
            { "CF Universidad de Chile",        "U. De Chile" },
            { "Universidad de Chile",           "U. De Chile" },
            { "U. De Chile",                    "U. De Chile" },

            // ── CS Independiente Rivadavia ───────────────────────────
            { "CS Independiente Rivadavia",     "CS Independiente Rivadavia" },
            { "Independiente Rivadavia",        "CS Independiente Rivadavia" },

            // ── Boston River ─────────────────────────────────────────
            { "CA Boston River",                "Boston River" },
            { "Boston River",                   "Boston River" },

            // ── Grêmio FBPA ──────────────────────────────────────────
            { "Grêmio FBPA",                    "Grêmio FBPA" },
            { "GrÃªmio FBPA",                   "Grêmio FBPA" },
            { "Gr&#234;mio FBPA",               "Grêmio FBPA" },
            { "Grêmio Porto Alegre",            "Grêmio FBPA" },
            { "Grêmio",                         "Grêmio FBPA" },
            { "Gremio Porto Alegre",            "Grêmio FBPA" },

            // ── GAS ──────────────────────────────────────────────────
            { "GAS",                            "GAS" },

            // ── Defensor Sp. ─────────────────────────────────────────
            { "Defensor Sporting Club",         "Defensor Sp." },
            { "Defensor Sporting",              "Defensor Sp." },
            { "Defensor Sp.",                   "Defensor Sp." },
            { "Defensor",                       "Defensor Sp." },

            // ── O'Higgins FC ─────────────────────────────────────────
            { "CD O'Higgins",                   "O'Higgins FC" },
            { "CD O&#039;Higgins",              "O'Higgins FC" },
            { "O'Higgins",                      "O'Higgins FC" },
            { "O&#039;Higgins",                 "O'Higgins FC" },
            { "OHiggins",                       "O'Higgins FC" },
            { "O'Higgins FC",                   "O'Higgins FC" },

            // ── Tigre ────────────────────────────────────────────────
            { "Club Atlético Tigre",            "Tigre" },
            { "CA Tigre",                       "Tigre" },
            { "Tigre",                          "Tigre" },

            // ── Racing Club ──────────────────────────────────────────
            { "Racing Club",                    "Racing Club" },
            { "Racing",                         "Racing Club" },
        };
        /// Normaliza o nome vindo do Transfermarkt para o padrão do banco
        public static string NormalizarNome(string nomeTransfermarkt)
        {
            if (string.IsNullOrWhiteSpace(nomeTransfermarkt))
                return string.Empty;

            var nome = nomeTransfermarkt.Trim();

            // Primeiro tenta direto
            if (mapaTimesNomes.TryGetValue(nome, out var nomeBanco))
                return nomeBanco;

            // Se não achou, tenta normalizar (remover acentos, etc.)
            var norm = NormalizarTexto(nome);
            var encontrado = mapaTimesNomes
                .FirstOrDefault(kv => NormalizarTexto(kv.Key) == norm);

            return !string.IsNullOrEmpty(encontrado.Value)
                ? encontrado.Value
                : nome; // fallback: retorna o próprio nome
        }

        // Exemplo de normalização simples (acentos, espaços, etc.)
        private static string NormalizarTexto(string texto)
        {
            return texto
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Aggregate("", (s, c) => s + c)
                .ToLower()
                .Replace(" ", "");
        }
    }
}
