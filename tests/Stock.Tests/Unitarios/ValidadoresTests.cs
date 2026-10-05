using Stock.Api.Dtos;
using Stock.Dominio.Entidades;

namespace Stock.Tests.Unitarios;

public class ValidadoresTests
{
    [Fact]
    public void Pedido_sin_renglones_ni_cliente_es_invalido()
    {
        var r = new PedidoValidador().Validate(new PedidoRequest(0, null, []));
        Assert.Contains(r.Errors, e => e.PropertyName == "ClienteId");
        Assert.Contains(r.Errors, e => e.PropertyName == "Renglones");
    }

    [Fact]
    public void Pedido_con_producto_repetido_o_cantidad_cero_es_invalido()
    {
        var r = new PedidoValidador().Validate(new PedidoRequest(1, null, [new(5, 1), new(5, 2), new(6, 0)]));
        Assert.Contains(r.Errors, e => e.ErrorMessage == "Hay productos repetidos.");
        Assert.Contains(r.Errors, e => e.PropertyName == "Renglones[2].Cantidad");
    }

    [Theory]
    [InlineData("alm-001", false)] // minúsculas
    [InlineData("ALM 001", false)]
    [InlineData("ALM-001", true)]
    public void Formato_del_sku(string sku, bool valido)
    {
        var r = new ProductoAltaValidador().Validate(new ProductoAltaRequest(sku, "Yerba", 1, 10m, 0, 0));
        Assert.Equal(valido, r.IsValid);
    }

    [Fact]
    public void A_mano_no_se_puede_registrar_una_venta()
    {
        var r = new MovimientoValidador().Validate(new MovimientoRequest(TipoMovimiento.VentaConfirmada, -1, null));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Un_ajuste_necesita_motivo()
    {
        var v = new MovimientoValidador();
        Assert.False(v.Validate(new MovimientoRequest(TipoMovimiento.Ajuste, -2, null)).IsValid);
        Assert.True(v.Validate(new MovimientoRequest(TipoMovimiento.Ajuste, -2, "Rotura")).IsValid);
    }

    [Fact]
    public void Cliente_con_cuit_invalido()
    {
        var r = new ClienteValidador().Validate(new ClienteRequest("Almacén", "30712345672", null, null, null));
        Assert.Contains(r.Errors, e => e.PropertyName == "Cuit");
    }
}
