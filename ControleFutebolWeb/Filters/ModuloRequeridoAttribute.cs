using ControleFutebolWeb.Models.Campeonatos;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ControleFutebolWeb.Filters
{
    /// <summary>
    /// Módulo do plano que o controller exige (ver AssinaturaFilter). Sem o atributo o
    /// controller é do módulo de análise — o sistema de sempre —, então só o que é
    /// novo precisa ser marcado.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ModuloRequeridoAttribute : Attribute
    {
        public ModuloSistema Modulo { get; }

        public ModuloRequeridoAttribute(ModuloSistema modulo)
        {
            Modulo = modulo;
        }
    }

    public static class ModuloAcesso
    {
        // Servem aos dois módulos: sem isso as imagens das telas de campeonato
        // (escudos, fotos) seriam barradas para quem só tem o plano de campeonatos.
        private static readonly HashSet<string> ControllersDeQualquerModulo =
            new(StringComparer.OrdinalIgnoreCase) { "MediaProxy" };

        /// <summary>Módulo que a action exige; null = liberada para qualquer plano.</summary>
        public static ModuloSistema? Exigido(ControllerActionDescriptor acao)
        {
            if (ControllersDeQualquerModulo.Contains(acao.ControllerName)) return null;

            var atributo = acao.ControllerTypeInfo
                .GetCustomAttributes(typeof(ModuloRequeridoAttribute), inherit: true)
                .OfType<ModuloRequeridoAttribute>()
                .FirstOrDefault();

            return atributo?.Modulo ?? ModuloSistema.Analise;
        }

        public static bool Tem(ModuloSistema doUsuario, ModuloSistema exigido) =>
            exigido != ModuloSistema.Nenhum && (doUsuario & exigido) == exigido;

        public static string Nome(ModuloSistema modulo) => modulo switch
        {
            ModuloSistema.Campeonatos => "Campeonatos próprios",
            ModuloSistema.Analise => "Análise de jogos",
            _ => "este recurso"
        };
    }
}
