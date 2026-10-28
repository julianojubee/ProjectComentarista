using ControleFutebolWeb.Filters;

namespace ControleFutebolWeb.Tests.Filters
{
    /// <summary>
    /// Qual página pública conta como qual ferramenta em /Admin/Acessos. A chave
    /// leva o controller porque /creators e /NBA têm, os dois, uma ação Index — se
    /// fosse só pela ação, a home da NBA seria somada no "hub" dos creators.
    /// </summary>
    public class AcessoPublicoFilterTests
    {
        [Theory]
        [InlineData("Creators", "Index", "hub")]
        [InlineData("Creators", "JogosHoje", "jogos-hoje")]
        [InlineData("Nba", "Index", "nba-jogos")]
        [InlineData("Nba", "Jogador", "nba-jogador")]
        [InlineData("nba", "classificacao", "nba-classificacao")]
        public void Mapeia_a_pagina_pela_dupla_controller_e_acao(string controller, string acao, string esperado)
        {
            Assert.Equal(esperado, AcessoPublicoFilter.Ferramenta(controller, acao));
        }

        [Theory]
        [InlineData("Nba", "Indisponivel")]
        [InlineData("Jogos", "Index")]
        [InlineData("Creators", "EscalacaoDados")]
        public void Pagina_fora_da_lista_nao_conta(string controller, string acao)
        {
            Assert.Null(AcessoPublicoFilter.Ferramenta(controller, acao));
        }
    }
}
