using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Onde a partida foi jogada, para exibição. Jogo.Estadio é o local que a fonte
    /// externa informou, e ele costuma faltar: a api-football (e a FIFA) só publicam o
    /// venue perto do jogo, e o que foi importado antes disso fica em branco para
    /// sempre. Quando falta, o estádio do MANDANTE cadastrado no time (Time.EstadioNome,
    /// que vem do /teams da api-football) é a melhor aproximação — na esmagadora maioria
    /// dos jogos o mandante joga em casa mesmo.
    ///
    /// É só apresentação: nada é gravado no jogo, e por isso uma reimportação que traga
    /// o venue de verdade continua mandando (inclusive num jogo em campo neutro, onde o
    /// palpite do mandante estaria errado — daí o <see cref="DoMandante"/>, que as telas
    /// usam para avisar de onde saiu o nome).
    /// </summary>
    public static class EstadioJogoHelper
    {
        /// <summary>Estádio da partida, ou o do mandante quando a fonte não informou.
        /// Null quando não há nem um nem outro.</summary>
        public static string? Local(Jogo? jogo)
        {
            if (!string.IsNullOrWhiteSpace(jogo?.Estadio)) return jogo!.Estadio!.Trim();

            var doTime = jogo?.TimeCasa?.EstadioNome;
            return string.IsNullOrWhiteSpace(doTime) ? null : doTime!.Trim();
        }

        /// <summary>O nome devolvido por <see cref="Local"/> é o palpite do mandante
        /// (e não o local que a fonte informou)?</summary>
        public static bool DoMandante(Jogo? jogo) =>
            string.IsNullOrWhiteSpace(jogo?.Estadio) &&
            !string.IsNullOrWhiteSpace(jogo?.TimeCasa?.EstadioNome);
    }
}
