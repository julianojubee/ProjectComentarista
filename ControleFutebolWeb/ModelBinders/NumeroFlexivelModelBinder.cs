using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ControleFutebolWeb.ModelBinders
{
    // Binder de números com decimal flexível: aceita tanto "10.5" quanto "10,5"
    // como dez e meio, independente da cultura da thread do servidor. Sem ele,
    // o binding padrão depende da cultura e "10,5" pode ser lido como 105
    // (vírgula tratada como separador de milhar) — era o que jogava os bonecos
    // para fora do campo quando a formação tinha coordenadas quebradas.
    // Vírgula e ponto são sempre separador decimal; separador de milhar não é aceito.
    public class NumeroFlexivelModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valores = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valores == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valores);

            var tipoAlvo = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;
            var ehNullable = tipoAlvo != bindingContext.ModelType;

            var raw = valores.FirstValue?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                if (ehNullable)
                    bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            var normalizado = raw.Replace(',', '.');

            if (double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            {
                object convertido =
                    tipoAlvo == typeof(float) ? (float)valor :
                    tipoAlvo == typeof(decimal) ? (decimal)valor :
                    valor;
                bindingContext.Result = ModelBindingResult.Success(convertido);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName, $"Valor numérico inválido: '{raw}'.");
            }

            return Task.CompletedTask;
        }
    }

    public class NumeroFlexivelModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            var tipo = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
            if (tipo == typeof(double) || tipo == typeof(float) || tipo == typeof(decimal))
                return new NumeroFlexivelModelBinder();
            return null;
        }
    }
}
