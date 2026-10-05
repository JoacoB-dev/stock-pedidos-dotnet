using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Dominio.Excepciones;

namespace Stock.Api.Infra;

/// <summary>
/// Traduce las excepciones a respuestas ProblemDetails (RFC 9457). Los errores inesperados
/// se loguean y se devuelven como 500 sin detalles internos.
/// </summary>
public class ManejadorDeErrores(IProblemDetailsService problemDetails, ILogger<ManejadorDeErrores> log)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (estado, titulo) = ex switch
        {
            NoEncontradoException => (StatusCodes.Status404NotFound, "No encontrado"),
            StockInsuficienteException => (StatusCodes.Status409Conflict, "Stock insuficiente"),
            ConflictoDeConcurrenciaException or DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Conflicto de concurrencia"),
            ReglaDeNegocioException => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (estado == StatusCodes.Status500InternalServerError)
            log.LogError(ex, "Error no controlado en {Metodo} {Ruta}", http.Request.Method, http.Request.Path);

        var detalle = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = estado == 500 ? "Ocurrió un error inesperado. Probá de nuevo más tarde." : ex switch
            {
                DbUpdateConcurrencyException => "Otro usuario modificó el dato mientras lo editabas. Recargá y volvé a intentar.",
                _ => ex.Message
            }
        };
        if (ex is StockInsuficienteException sinStock)
            detalle.Extensions["faltantes"] = sinStock.Faltantes;

        http.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = detalle,
            Exception = ex
        });
    }
}
