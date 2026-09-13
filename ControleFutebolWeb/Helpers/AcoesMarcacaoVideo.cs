using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>Uma tecla da marcação por vídeo e o que ela soma na linha estatística.</summary>
    /// <param name="Id">Id gravado em <see cref="MarcacaoVideo.AcaoId"/>.</param>
    /// <param name="Rotulo">Como a ação aparece na legenda e no histórico.</param>
    /// <param name="Grupo">Bloco da legenda na tela (Passe, Ataque, Defesa…).</param>
    /// <param name="Tecla">Letra da tecla. O front casa com KeyboardEvent.code ("KeyP"),
    /// não com o caractere, para o atalho valer em qualquer layout de teclado.</param>
    /// <param name="Shift">A tecla precisa de Shift (a variante "negativa" da ação:
    /// passe errado, finalização fora, duelo perdido).</param>
    /// <param name="Aplicar">Incremento na linha estatística. Null nas ações de
    /// controle, que existem só para calcular minutos e não viram estatística.</param>
    public sealed record AcaoMarcacao(
        string Id, string Rotulo, string Grupo, string Tecla, bool Shift,
        Action<EstatisticaJogador>? Aplicar)
    {
        public bool EhEstatistica => Aplicar != null;
    }

    /// <summary>
    /// Catálogo das ações que a marcação por vídeo (/MarcacaoVideo/Marcar) sabe
    /// registrar. É a única fonte da verdade das três pontas: a legenda e os
    /// atalhos da tela saem daqui serializados, o POST de registro valida o
    /// AcaoId contra esta lista, e a consolidação em EstatisticaJogador aplica
    /// exatamente o <see cref="AcaoMarcacao.Aplicar"/> de cada uma.
    ///
    /// As teclas foram escolhidas para caber sob a mão esquerda enquanto a direita
    /// digita o número da camisa, e a variante ruim de cada ação é sempre a MESMA
    /// tecla com Shift — quem marca decora um par, não dois atalhos soltos.
    ///
    /// Algumas ações incrementam mais de um campo porque o lance é o mesmo evento
    /// visto por duas métricas: um gol também é finalização no gol, uma assistência
    /// também é passe certo e passe-chave, um desarme também é duelo vencido.
    /// Marcar as duas coisas separadamente contaria o lance duas vezes.
    /// </summary>
    public static class AcoesMarcacaoVideo
    {
        public const string Entrou = "entrou";
        public const string Saiu = "saiu";

        public static readonly IReadOnlyList<AcaoMarcacao> Todas = new List<AcaoMarcacao>
        {
            // ── Passe ────────────────────────────────────────────────────────
            new("passe_certo",  "Passe certo",  "Passe", "P", false, e => { e.PassesTotal++; e.PassesCertos++; }),
            new("passe_errado", "Passe errado", "Passe", "P", true,  e => { e.PassesTotal++; }),
            // Passe-chave é um passe certo que termina em finalização do companheiro:
            // conta nas três colunas, senão a precisão de passe cairia a cada bom passe.
            new("passe_chave",  "Passe-chave",  "Passe", "C", false, e => { e.PassesTotal++; e.PassesCertos++; e.PassesChave++; }),

            // ── Ataque ───────────────────────────────────────────────────────
            new("finalizacao_gol",  "Finalização no gol",  "Ataque", "F", false, e => { e.FinalizacoesTotal++; e.FinalizacoesNoGol++; }),
            new("finalizacao_fora", "Finalização para fora","Ataque", "F", true,  e => { e.FinalizacoesTotal++; }),
            new("gol",              "Gol",                 "Ataque", "G", false, e => { e.Gols++; e.FinalizacoesTotal++; e.FinalizacoesNoGol++; }),
            new("assistencia",      "Assistência",         "Ataque", "A", false, e => { e.Assistencias++; e.PassesTotal++; e.PassesCertos++; e.PassesChave++; }),
            new("impedimento",      "Impedimento",         "Ataque", "M", false, e => { e.Offsides++; }),

            // ── Drible ───────────────────────────────────────────────────────
            new("drible_certo",   "Drible certo",   "Drible", "D", false, e => { e.DriblesTentados++; e.DriblesCertos++; }),
            new("drible_perdido", "Drible perdido", "Drible", "D", true,  e => { e.DriblesTentados++; }),
            // Quem foi driblado. Marcada na defensora, não na atacante.
            new("drible_sofrido", "Foi driblada",   "Drible", "Z", false, e => { e.DriblesSofridos++; }),

            // ── Defesa e duelo ───────────────────────────────────────────────
            new("desarme",       "Desarme",       "Defesa", "T", false, e => { e.Desarmes++; e.DuelosTotal++; e.DuelosVencidos++; }),
            new("interceptacao", "Interceptação", "Defesa", "I", false, e => { e.Interceptacoes++; }),
            new("bloqueio",      "Bloqueio",      "Defesa", "B", false, e => { e.Bloqueios++; }),
            // Disputa que não foi desarme: dividida, bola aérea, corpo a corpo.
            new("duelo_vencido", "Duelo vencido", "Defesa", "Q", false, e => { e.DuelosTotal++; e.DuelosVencidos++; }),
            new("duelo_perdido", "Duelo perdido", "Defesa", "Q", true,  e => { e.DuelosTotal++; }),

            // ── Disciplina ───────────────────────────────────────────────────
            new("falta_sofrida",   "Falta sofrida",   "Disciplina", "L", false, e => { e.FaltasSofridas++; }),
            new("falta_cometida",  "Falta cometida",  "Disciplina", "L", true,  e => { e.FaltasCometidas++; }),
            new("cartao_amarelo",  "Cartão amarelo",  "Disciplina", "K", false, e => { e.CartoesAmarelos++; }),
            new("cartao_vermelho", "Cartão vermelho", "Disciplina", "K", true,  e => { e.CartoesVermelhos++; }),

            // ── Goleira ──────────────────────────────────────────────────────
            new("defesa",      "Defesa (goleira)", "Goleira", "X", false, e => { e.Defesas++; }),
            new("gol_sofrido", "Gol sofrido",      "Goleira", "X", true,  e => { e.GolsSofridos++; }),

            // ── Pênalti ──────────────────────────────────────────────────────
            new("penalti_sofrido",    "Pênalti sofrido",    "Pênalti", "W", false, e => { e.PenaltiSofrido++; }),
            new("penalti_cometido",   "Pênalti cometido",   "Pênalti", "W", true,  e => { e.PenaltiCometido++; }),
            // O gol do pênalti convertido é marcado à parte (tecla G): aqui só a
            // cobrança, para a conversão de pênaltis não depender do placar.
            new("penalti_convertido", "Pênalti convertido", "Pênalti", "J", false, e => { e.PenaltiConvertido++; }),
            new("penalti_perdido",    "Pênalti perdido",    "Pênalti", "J", true,  e => { e.PenaltiPerdido++; }),
            new("penalti_defendido",  "Pênalti defendido",  "Pênalti", "H", false, e => { e.PenaltiDefendido++; }),

            // ── Controle (não vira estatística) ──────────────────────────────
            // Entrada e saída da jogadora. Não somam nada na linha: servem para a
            // consolidação saber quantos minutos ela jogou, que é o divisor de
            // tudo no rating automático (RatingAutomaticoHelper).
            new(Entrou, "Entrou em campo", "Controle", "E", false, null),
            new(Saiu,   "Saiu de campo",   "Controle", "E", true,  null),
        };

        private static readonly Dictionary<string, AcaoMarcacao> PorIdMapa =
            Todas.ToDictionary(a => a.Id, StringComparer.Ordinal);

        public static AcaoMarcacao? PorId(string? id) =>
            id != null && PorIdMapa.TryGetValue(id, out var a) ? a : null;

        public static bool Existe(string? id) => PorId(id) != null;
    }
}
