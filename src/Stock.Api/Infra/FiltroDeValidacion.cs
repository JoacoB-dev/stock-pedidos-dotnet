using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Stock.Api.Infra;

/// <summary>
/// Corre el validador de FluentValidation que corresponda a cada argumento de la acción.
/// Si algo falla, corta y devuelve 400 con un ValidationProblemDetails (errores por campo).
/// </summary>
public class FiltroDeValidacion(IServiceProvider servicios) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate siguiente)
    {
        foreach (var argumento in contexto.ActionArguments.Values.Where(a => a is not null))
        {
            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento!.GetType());
            if (servicios.GetService(tipoValidador) is not IValidator validador) continue;

            var resultado = await validador.ValidateAsync(new ValidationContext<object>(argumento));
            foreach (var error in resultado.Errors)
                contexto.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!contexto.ModelState.IsValid)
        {
            contexto.Result = new BadRequestObjectResult(new ValidationProblemDetails(contexto.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Hay datos inválidos"
            });
            return;
        }

        await siguiente();
    }
}
