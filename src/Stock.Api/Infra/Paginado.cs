using Microsoft.EntityFrameworkCore;

namespace Stock.Api.Infra;

public record Pagina<T>(IReadOnlyList<T> Items, int Total, int NumeroPagina, int Tamanio)
{
    public int TotalPaginas => Tamanio == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamanio);
}

public static class PaginadoExtensions
{
    public const int TamanioMaximo = 100;

    public static async Task<Pagina<T>> PaginarAsync<T>(this IQueryable<T> consulta, int pagina, int tamanio,
        CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanio = Math.Clamp(tamanio, 1, TamanioMaximo);
        var total = await consulta.CountAsync(ct);
        var items = await consulta.Skip((pagina - 1) * tamanio).Take(tamanio).ToListAsync(ct);
        return new Pagina<T>(items, total, pagina, tamanio);
    }
}
