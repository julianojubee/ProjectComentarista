namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Lê o par tipo/motivo cru da api-football (ver JogoIndisponivel) e devolve o que
    /// a tela precisa: se o jogador está fora ou em dúvida, se é lesão ou suspensão, e
    /// o motivo em português.
    ///
    /// Fica aqui, e não na importação, porque é interpretação: a fonte manda texto
    /// livre em inglês, a lista de motivos cresce sozinha, e uma tradução nova não
    /// pode exigir reimportar partida antiga para valer.
    /// </summary>
    public static class IndisponibilidadeHelper
    {
        public const string CategoriaLesao = "lesao";
        public const string CategoriaSuspensao = "suspensao";
        public const string CategoriaOutro = "outro";

        /// <summary>
        /// Lesão, suspensão ou outro motivo (convocação, decisão técnica, "Inactive").
        ///
        /// Decide pelo MOTIVO e não pelo tipo: o tipo só diz se ele está fora ou em
        /// dúvida. Um "Questionable / Hamstring Injury" é dúvida por lesão, e as duas
        /// coisas precisam aparecer na tela.
        /// </summary>
        public static string Categoria(string? motivo)
        {
            var m = (motivo ?? "").ToLowerInvariant();

            if (m.Contains("card") || m.Contains("suspend") || m.Contains("ban"))
                return CategoriaSuspensao;

            // "Injury" cobre a maioria; o resto são motivos médicos que a fonte às
            // vezes escreve sem a palavra (o joelho vem como "Knee Injury", mas a
            // virilha aparece só como "Groin").
            if (m.Contains("injur") || m.Contains("strain") || m.Contains("knock") ||
                m.Contains("fracture") || m.Contains("surgery") || m.Contains("ill") ||
                m.Contains("virus") || m.Contains("covid") || m.Contains("fitness") ||
                m.Contains("groin") || m.Contains("hamstring") || m.Contains("achilles") ||
                m.Contains("broken") || m.Contains("contusion") || m.Contains("hernia") ||
                m.Contains("concussion") || m.Contains("health") || m.Contains("heart"))
                return CategoriaLesao;

            return CategoriaOutro;
        }

        /// <summary>Está fora mesmo, ou é dúvida? "Questionable" é o único tipo de dúvida da fonte.</summary>
        public static bool EhDuvida(string? tipo) =>
            (tipo ?? "").Contains("questionable", StringComparison.OrdinalIgnoreCase);

        /// <summary>Rótulo curto do tipo, para o selo da linha.</summary>
        public static string Tipo(string? tipo) =>
            EhDuvida(tipo) ? "Dúvida" : "Fora";

        /// <summary>
        /// Motivo em português. O que não estiver no de-para sai como veio: um motivo
        /// em inglês ainda diz por que o jogador está fora, e adivinhar tradução para
        /// texto livre de fonte externa erra mais do que acerta.
        /// </summary>
        public static string Motivo(string? motivo)
        {
            var m = (motivo ?? "").Trim();
            if (m.Length == 0) return "Motivo não informado";

            return m.ToLowerInvariant() switch
            {
                // Suspensão
                "yellow cards" or "yellow card" => "Suspenso (cartões amarelos)",
                "red card" or "red cards" => "Suspenso (cartão vermelho)",
                "suspended" => "Suspenso",

                // Lesão — genéricas
                "injury" => "Lesão",
                "muscle injury" => "Lesão muscular",
                "knock" => "Pancada",
                "contusion" => "Contusão",
                "illness" => "Doença",
                "health problems" => "Problema de saúde",
                "heart problems" => "Problema cardíaco",
                "fitness" => "Condicionamento físico",
                "lacking match fitness" => "Sem ritmo de jogo",
                "surgery" => "Cirurgia",
                "concussion" => "Concussão",
                "hernia" => "Hérnia",
                "abdominal strain" => "Estiramento abdominal",

                // Lesão — por região
                "knee injury" => "Lesão no joelho",
                "leg injury" => "Lesão na perna",
                "broken leg" => "Fratura na perna",
                "broken calfbone" => "Fratura na fíbula",
                "broken nose" => "Fratura no nariz",
                "toe injury" => "Lesão no dedo do pé",
                "heel injury" => "Lesão no calcanhar",
                "wrist injury" => "Lesão no punho",
                "hand injury" => "Lesão na mão",
                "arm injury" => "Lesão no braço",
                "jaw injury" => "Lesão na mandíbula",
                "eye injury" => "Lesão no olho",
                "ribs injury" => "Lesão nas costelas",
                "collarbone injury" => "Lesão na clavícula",
                "tendon injury" => "Lesão no tendão",
                "ankle injury" => "Lesão no tornozelo",
                "thigh injury" => "Lesão na coxa",
                "hamstring injury" => "Lesão na posterior da coxa",
                "calf injury" => "Lesão na panturrilha",
                "groin injury" or "groin strain" => "Lesão na virilha",
                "back injury" => "Lesão nas costas",
                "lower back injury" => "Lesão lombar",
                "shoulder injury" => "Lesão no ombro",
                "foot injury" => "Lesão no pé",
                "ankle/foot injury" => "Lesão no tornozelo/pé",
                "hip injury" => "Lesão no quadril",
                "head injury" => "Traumatismo craniano",
                "cruciate ligament injury" => "Lesão do ligamento cruzado",
                "achilles tendon injury" => "Lesão no tendão de Aquiles",

                // Outros
                "inactive" or "off the roster" => "Fora dos relacionados",
                "coach decision" or "coach's decision" => "Decisão do treinador",
                "national team" or "international duty" => "Servindo a seleção",
                "personal reasons" => "Motivos pessoais",
                "rest" => "Poupado",
                "transfer" => "Situação de mercado",
                "transfer negotiations" => "Em negociação de transferência",
                // Emprestado pelo adversário: o contrato o impede de jogar contra o dono do passe
                "loan agreement" => "Cláusula de empréstimo",

                _ => m,
            };
        }
    }
}
