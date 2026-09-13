namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Uma jogada desenhada na prancheta tática (modal "Jogadas" de /Jogos/Analisar):
    /// a sequência de passos que descreve como o time constrói um lance
    /// (goleiro → lateral → cruzamento → finalização).
    ///
    /// É por jogo E por time: a jogada nasce dentro da análise de uma partida, mas o
    /// TimeId a devolve no "arsenal" de /Times/Details, reunindo tudo que o usuário já
    /// desenhou para aquele time em qualquer análise.
    ///
    /// O storyboard inteiro mora em <see cref="PassosJson"/> — é dado puramente visual,
    /// sempre lido inteiro e nunca filtrado por parte dele, então normalizar em tabelas
    /// de passo/peça só criaria joins sem nenhum ganho de consulta. Mesmo raciocínio
    /// de Jogo.EstatisticasJson. Formato (coordenadas em % do campo horizontal, com o
    /// time atacando da esquerda para a direita):
    ///
    /// {
    ///   "v": 1,
    ///   "ms": 900,                                             // duração de cada transição
    ///   "elenco": [ { "id": 7, "num": "10", "nome": "…", "sigla": "MEI", "adv": false } ],
    ///   "grupos": [ { "id": "gk3f", "nome": "", "ids": [2, 3, 4, 6] } ],   // blocos
    ///   "passos": [
    ///     {
    ///       "legenda": "Goleiro sai jogando",
    ///       "passe":  "passe",                                  // tipo de bola do trecho
    ///       "bola":   { "x": 10.0, "y": 50.0 },
    ///       "pecas":  [ { "id": 7, "x": 8.0, "y": 50.0, "modo": "trote", "atraso": 0 } ],
    ///       "setas":  [ { "x1": 8, "y1": 50, "x2": 30, "y2": 20 } ]
    ///     }
    ///   ]
    /// }
    ///
    /// "grupos" são os blocos táticos: jogadores que a prancheta move juntos, mantendo
    /// a forma (a linha de quatro, o triângulo do meio). Ficam fora dos passos porque o
    /// vínculo é entre as peças e vale a jogada inteira — o que muda a cada passo é
    /// onde o bloco está. Cada jogador entra em no máximo um bloco.
    ///
    /// "elenco" é uma cópia do nome/número dos jogadores usados: o arsenal do time
    /// precisa desenhar a jogada sem recarregar a escalação do jogo de origem, e o
    /// desenho continua fiel mesmo se o jogador trocar de número ou de clube depois.
    /// "adv" marca as peças da marcação adversária (o outro time da partida), que
    /// entram em campo para mostrar quem é arrastado para abrir o espaço.
    /// Todo passo repete a posição de TODAS as peças (inclusive quem não se moveu) —
    /// é o que deixa a interpolação do player trivial.
    /// </summary>
    public class Jogada
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser? Usuario { get; set; }

        // Time que executa a jogada (casa ou visitante do jogo).
        public int TimeId { get; set; }
        public Time Time { get; set; } = null!;

        public string Nome { get; set; } = "";
        public string? Descricao { get; set; }

        // Ordem manual dentro do jogo/time (a lista do modal segue esta ordem).
        public int Ordem { get; set; }

        public string PassosJson { get; set; } = "";

        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }
}
