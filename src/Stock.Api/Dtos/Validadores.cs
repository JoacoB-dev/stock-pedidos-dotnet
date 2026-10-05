using FluentValidation;
using Stock.Dominio;
using Stock.Dominio.Entidades;

namespace Stock.Api.Dtos;

public class LoginValidador : AbstractValidator<LoginRequest>
{
    public LoginValidador()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Ingresá el email.");
        RuleFor(x => x.Clave).NotEmpty().WithMessage("Ingresá la contraseña.");
    }
}

public class CategoriaValidador : AbstractValidator<CategoriaRequest>
{
    public CategoriaValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
    }
}

public class ProductoAltaValidador : AbstractValidator<ProductoAltaRequest>
{
    public ProductoAltaValidador()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(30)
            .Matches("^[A-Z0-9-]+$").WithMessage("El SKU solo admite mayúsculas, números y guiones.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoriaId).GreaterThan(0);
        RuleFor(x => x.Precio).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.StockMinimo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockInicial).GreaterThanOrEqualTo(0);
    }
}

public class ProductoEdicionValidador : AbstractValidator<ProductoEdicionRequest>
{
    public ProductoEdicionValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoriaId).GreaterThan(0);
        RuleFor(x => x.Precio).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.StockMinimo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("Falta el token de concurrencia (RowVersion).");
    }
}

public class MovimientoValidador : AbstractValidator<MovimientoRequest>
{
    public MovimientoValidador()
    {
        RuleFor(x => x.Tipo).Must(t => t is TipoMovimiento.Ingreso or TipoMovimiento.Ajuste)
            .WithMessage("A mano solo se pueden registrar ingresos o ajustes.");
        RuleFor(x => x.Cantidad).NotEqual(0);
        RuleFor(x => x.Cantidad).GreaterThan(0).When(x => x.Tipo == TipoMovimiento.Ingreso)
            .WithMessage("Un ingreso tiene que sumar unidades.");
        RuleFor(x => x.Motivo).NotEmpty().When(x => x.Tipo == TipoMovimiento.Ajuste)
            .WithMessage("Los ajustes necesitan un motivo.");
        RuleFor(x => x.Motivo).MaximumLength(300);
    }
}

public class ClienteValidador : AbstractValidator<ClienteRequest>
{
    public ClienteValidador()
    {
        RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Cuit).Must(Cuit.EsValido).WithMessage("El CUIT no es válido.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Telefono).MaximumLength(40);
        RuleFor(x => x.Direccion).MaximumLength(200);
    }
}

public class PedidoValidador : AbstractValidator<PedidoRequest>
{
    public PedidoValidador()
    {
        RuleFor(x => x.ClienteId).GreaterThan(0).WithMessage("Elegí un cliente.");
        RuleFor(x => x.Observaciones).MaximumLength(500);
        RuleFor(x => x.Renglones).NotEmpty().WithMessage("Agregá al menos un producto.");
        RuleFor(x => x.Renglones)
            .Must(r => r.Select(x => x.ProductoId).Distinct().Count() == r.Count)
            .When(x => x.Renglones is not null)
            .WithMessage("Hay productos repetidos.");
        RuleForEach(x => x.Renglones).ChildRules(r =>
        {
            r.RuleFor(x => x.ProductoId).GreaterThan(0);
            r.RuleFor(x => x.Cantidad).InclusiveBetween(1, 100_000);
        });
    }
}

public class CancelarValidador : AbstractValidator<CancelarRequest>
{
    public CancelarValidador()
    {
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Indicá el motivo de la cancelación.").MaximumLength(300);
    }
}
