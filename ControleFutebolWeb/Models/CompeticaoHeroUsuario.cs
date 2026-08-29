namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Capa (hero) que o usuário escolheu para uma competição. Sem registro, a
    /// tela de detalhes desenha o degradê padrão do tipo da competição.
    /// É por usuário — como <see cref="CompeticaoTopTierUsuario"/> e
    /// <see cref="CompeticaoHomeUsuario"/> —, então a arte de um não muda a do outro.
    /// </summary>
    public class CompeticaoHeroUsuario
    {
        public int Id { get; set; }

        public int CompeticaoId { get; set; }
        public Competicao Competicao { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // URL relativa da imagem salva em wwwroot (ex.: /images/heroes/la-liga-ab12.jpg).
        // Vazio = sem capa própria; o registro existe só pela cor escolhida.
        public string ImagemUrl { get; set; } = "";

        // Véu escuro por cima da imagem, em % (0–90). Sem ele, foto clara apaga
        // o título e os números que ficam por cima.
        public int Escurecimento { get; set; } = 55;

        // Enquadramento da foto dentro da faixa, no mesmo par que o CSS usa em
        // object-position: % horizontal e vertical do ponto que fica visível
        // (50/50 = centro). Só tem efeito quando a imagem sobra em algum eixo.
        public int PosX { get; set; } = 50;
        public int PosY { get; set; } = 50;

        // Ampliação da foto em % da medida do ajuste escolhido, de 30 a 300.
        // Aproxima/afasta a partir do ponto escolhido em PosX/PosY.
        public int Zoom { get; set; } = 100;

        // COBRIR = a foto preenche a faixa e o que sobra é cortado (bom para arte
        // deitada). CABER = a imagem inteira aparece dentro da faixa e o degradê da
        // competição preenche as laterais (bom para logo quadrado).
        public string Ajuste { get; set; } = "COBRIR";

        // Cor de destaque do hero em #rrggbb (risco lateral, kicker, números e
        // botões), e matiz do degradê de fundo. Nulo = a cor padrão do tipo da
        // competição (verde em pontos corridos, azul em grupos...).
        public string? Cor { get; set; }
    }
}
