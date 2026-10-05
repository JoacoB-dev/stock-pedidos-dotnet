namespace Stock.Dominio.Excepciones;

/// <summary>Una regla de negocio no se cumple (la API la devuelve como 422).</summary>
public class ReglaDeNegocioException(string mensaje) : Exception(mensaje);

/// <summary>No alcanza el stock de uno o más productos para confirmar un pedido.</summary>
public class StockInsuficienteException(IReadOnlyList<FaltanteStock> faltantes)
    : ReglaDeNegocioException("No hay stock suficiente para confirmar el pedido.")
{
    public IReadOnlyList<FaltanteStock> Faltantes { get; } = faltantes;
}

public record FaltanteStock(int ProductoId, string Sku, int Pedido, int Disponible);

/// <summary>El recurso buscado no existe (404).</summary>
public class NoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>Otro usuario modificó el mismo dato al mismo tiempo (409).</summary>
public class ConflictoDeConcurrenciaException(string mensaje) : Exception(mensaje);
