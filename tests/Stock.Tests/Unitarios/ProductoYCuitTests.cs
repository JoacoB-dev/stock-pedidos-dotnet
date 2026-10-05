using Stock.Dominio;
using Stock.Dominio.Entidades;
using Stock.Dominio.Excepciones;

namespace Stock.Tests.Unitarios;

public class ProductoTests
{
    private static readonly DateTime Ahora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Un_ajuste_negativo_no_puede_dejar_stock_negativo()
    {
        var p = new Producto("A-1", "A", 1, 10m, 1);
        p.AplicarMovimiento(TipoMovimiento.Ingreso, 3, 1, Ahora);

        var ex = Assert.Throws<ReglaDeNegocioException>(() =>
            p.AplicarMovimiento(TipoMovimiento.Ajuste, -4, 1, Ahora, motivo: "rotura"));
        Assert.Contains("3 unidades", ex.Message);
        Assert.Equal(3, p.Stock);
    }

    [Theory]
    [InlineData(TipoMovimiento.Ingreso, -1)]
    [InlineData(TipoMovimiento.CancelacionPedido, -1)]
    [InlineData(TipoMovimiento.VentaConfirmada, 1)]
    [InlineData(TipoMovimiento.Ajuste, 0)]
    public void El_signo_tiene_que_corresponder_al_tipo(TipoMovimiento tipo, int cantidad)
    {
        var p = new Producto("A-1", "A", 1, 10m, 1);
        p.AplicarMovimiento(TipoMovimiento.Ingreso, 10, 1, Ahora);
        Assert.Throws<ReglaDeNegocioException>(() => p.AplicarMovimiento(tipo, cantidad, 1, Ahora));
    }

    [Fact]
    public void El_movimiento_guarda_el_stock_resultante()
    {
        var p = new Producto("A-1", "A", 1, 10m, 5);
        p.AplicarMovimiento(TipoMovimiento.Ingreso, 10, 1, Ahora);
        var m = p.AplicarMovimiento(TipoMovimiento.Ajuste, -6, 2, Ahora, motivo: "Inventario");

        Assert.Equal(4, m.StockResultante);
        Assert.Equal(-6, m.Cantidad);
        Assert.True(p.StockBajo); // 4 <= 5
    }
}

public class CuitTests
{
    [Theory]
    [InlineData("30712345671", true)]
    [InlineData("30-71234567-1", true)]
    [InlineData("20-28765432-5", true)]
    [InlineData("30712345672", false)] // dígito verificador incorrecto
    [InlineData("3071234567", false)]  // le falta un dígito
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valida_el_digito_verificador(string? cuit, bool esperado) =>
        Assert.Equal(esperado, Cuit.EsValido(cuit));
}
