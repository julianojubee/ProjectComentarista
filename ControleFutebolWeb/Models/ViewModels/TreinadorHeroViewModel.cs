namespace ControleFutebolWeb.Models.ViewModels
{
    // Cabeçalho compartilhado das telas de treinador (ficha, anotações, histórico salvo
    // e pré-visualização da importação). Existia copiado em cada .cshtml — e era por isso
    // que as telas divergiam com o tempo.
    public class TreinadorHeroViewModel
    {
        public Treinador Treinador { get; set; } = null!;

        /// <summary>Rótulo pequeno acima do nome ("Treinador", "Anotações", "Histórico"…).</summary>
        public string Kicker { get; set; } = "Treinador";

        /// <summary>Item extra na linha de metadados (ex.: "Fonte: api-football").</summary>
        public string? MetaExtra { get; set; }

        public List<TreinadorHeroAcao> Acoes { get; set; } = new();
    }

    public class TreinadorHeroAcao
    {
        public string Texto { get; set; } = "";
        public string Url { get; set; } = "#";

        /// <summary>Chave do ícone desenhado no partial: voltar, editar, anotacoes, mais, importar.</summary>
        public string? Icone { get; set; }

        /// <summary>Botão de destaque (dourado). Só um por tela.</summary>
        public bool Destaque { get; set; }
    }
}
