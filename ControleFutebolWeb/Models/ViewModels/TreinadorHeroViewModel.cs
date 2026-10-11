namespace ControleFutebolWeb.Models.ViewModels
{
    // Cabeçalho compartilhado das telas de treinador (ficha, anotações, histórico salvo
    // e pré-visualização da importação). Existia copiado em cada .cshtml — e era por isso
    // que as telas divergiam com o tempo.
    public class TreinadorHeroViewModel
    {
        public Treinador Treinador { get; set; } = null!;

        /// <summary>
        /// Nome da tela atual na trilha acima do nome ("Histórico", "Anotações"…). Null na
        /// própria ficha: a trilha mostra só "Treinadores".
        /// </summary>
        public string? Kicker { get; set; }

        /// <summary>Item extra na linha de metadados (ex.: "Fonte: api-football").</summary>
        public string? MetaExtra { get; set; }

        public List<TreinadorHeroAcao> Acoes { get; set; } = new();

        /// <summary>Ações secundárias, num menu "Mais" ao lado dos botões.</summary>
        public List<TreinadorHeroAcao> Menu { get; set; } = new();
    }

    public class TreinadorHeroAcao
    {
        public string Texto { get; set; } = "";
        public string Url { get; set; } = "#";

        /// <summary>Chave do ícone desenhado no partial: voltar, editar, anotacoes, mais, importar.</summary>
        public string? Icone { get; set; }

        /// <summary>Botão de destaque (dourado). Só um por tela.</summary>
        public bool Destaque { get; set; }

        /// <summary>Linha explicativa embaixo do item (só no menu).</summary>
        public string? Detalhe { get; set; }

        /// <summary>Item do menu que envia POST (com antiforgery) em vez de navegar.</summary>
        public bool Post { get; set; }

        /// <summary>Pergunta de confirmação antes do POST.</summary>
        public string? Confirmar { get; set; }

        /// <summary>Ação destrutiva: texto em vermelho.</summary>
        public bool Perigo { get; set; }

        /// <summary>Campos ocultos enviados junto no POST.</summary>
        public Dictionary<string, string> Campos { get; set; } = new();
    }
}
