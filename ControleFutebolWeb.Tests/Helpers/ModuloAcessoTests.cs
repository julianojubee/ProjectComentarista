using System.Reflection;
using ControleFutebolWeb.Controllers;
using ControleFutebolWeb.Filters;
using ControleFutebolWeb.Models.Campeonatos;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class ModuloAcessoTests
    {
        private static ControllerActionDescriptor Acao<T>() => new()
        {
            ControllerName = typeof(T).Name.Replace("Controller", ""),
            ControllerTypeInfo = typeof(T).GetTypeInfo()
        };

        [Theory]
        [InlineData(ModuloSistema.Analise, ModuloSistema.Analise, true)]
        [InlineData(ModuloSistema.Analise, ModuloSistema.Campeonatos, false)]
        [InlineData(ModuloSistema.Campeonatos, ModuloSistema.Analise, false)]
        [InlineData(ModuloSistema.Completo, ModuloSistema.Campeonatos, true)]
        [InlineData(ModuloSistema.Completo, ModuloSistema.Analise, true)]
        [InlineData(ModuloSistema.Nenhum, ModuloSistema.Analise, false)]
        [InlineData(ModuloSistema.Completo, ModuloSistema.Nenhum, false)]
        public void Tem(ModuloSistema doUsuario, ModuloSistema exigido, bool esperado)
        {
            Assert.Equal(esperado, ModuloAcesso.Tem(doUsuario, exigido));
        }

        [Fact]
        public void ControllersDoModuloDeCampeonatos_ExigemCampeonatos()
        {
            Assert.Equal(ModuloSistema.Campeonatos, ModuloAcesso.Exigido(Acao<CampeonatosController>()));
            Assert.Equal(ModuloSistema.Campeonatos, ModuloAcesso.Exigido(Acao<TimesPropriosController>()));
            Assert.Equal(ModuloSistema.Campeonatos, ModuloAcesso.Exigido(Acao<JogadoresPropriosController>()));
        }

        [Fact]
        public void ControllerSemAtributo_EhDoModuloDeAnalise()
        {
            Assert.Equal(ModuloSistema.Analise, ModuloAcesso.Exigido(Acao<JogosController>()));
            Assert.Equal(ModuloSistema.Analise, ModuloAcesso.Exigido(Acao<HomeController>()));
        }

        [Fact]
        public void ProxyDeImagens_ServeAosDoisModulos()
        {
            Assert.Null(ModuloAcesso.Exigido(Acao<MediaProxyController>()));
        }

        // Pega o esquecimento mais provável: um controller novo do módulo de
        // campeonatos (namespace de modelos Campeonatos) sem o atributo, que cairia
        // no módulo de análise e ficaria fechado para quem só tem campeonatos.
        [Fact]
        public void ControllerQueUsaModelosDeCampeonato_TemOAtributo()
        {
            var semAtributo = typeof(CampeonatosController).Assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Controller") && !t.IsAbstract)
                .Where(t => t.Name is "TimesPropriosController" or "JogadoresPropriosController"
                            || (t.Name.StartsWith("Campeonato") && t.Name != "CampeonatoPublicoController"))
                .Where(t => t.GetCustomAttribute<ModuloRequeridoAttribute>() == null)
                .Select(t => t.Name)
                .ToList();

            Assert.Empty(semAtributo);
        }
    }
}
