public static class PosicionamentoHelper
{
    public static string ObterFaixa(string posicao)
    {
        return posicao switch
        {
            "Goleiro" => "faixa-goleiro",
            "Defesa" => "faixa-defesa",
            "Meio" => "faixa-meio",
            "Ataque" => "faixa-ataque",
            _ => "faixa-meio" // fallback
        };
    }
}
