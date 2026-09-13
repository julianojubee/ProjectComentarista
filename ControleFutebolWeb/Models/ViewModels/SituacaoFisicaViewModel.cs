namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Lesão em curso de um jogador, como o FotMob publica no perfil dele.
    ///
    /// É um retrato do momento, não histórico: o FotMob só informa a lesão ATUAL e a
    /// apaga quando o jogador volta. Por isso nada disso é gravado — vale enquanto a
    /// resposta estiver em cache e some sozinho quando a fonte parar de informar.
    /// Guardar significaria manter no banco um jogador "lesionado" para sempre, já que
    /// não existe evento de alta para desfazer o registro.
    /// </summary>
    public class SituacaoFisicaViewModel
    {
        /// <summary>Nome da lesão em português quando conhecido; o texto original do FotMob quando não.</summary>
        public string Lesao { get; set; } = "";

        /// <summary>Previsão de retorno ("Fora por toda a temporada", "Retorno esperado: 12/10/2026"). Null quando a fonte não arrisca.</summary>
        public string? Retorno { get; set; }

        /// <summary>Quando o FotMob atualizou essa informação pela última vez (hora local).</summary>
        public DateTime? AtualizadoEm { get; set; }
    }
}
