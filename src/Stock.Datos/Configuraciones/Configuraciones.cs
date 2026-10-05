using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Datos.Consultas;
using Stock.Dominio.Entidades;

namespace Stock.Datos.Configuraciones;

internal class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.Property(u => u.Email).HasMaxLength(150).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.HashClave).HasMaxLength(200).IsRequired();
    }
}

internal class CategoriaConfig : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
        b.HasIndex(c => c.Nombre).IsUnique();
    }
}

internal class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.Property(p => p.Sku).HasMaxLength(30).IsRequired();
        b.HasIndex(p => p.Sku).IsUnique();
        b.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        b.Property(p => p.Precio).HasPrecision(18, 2);
        // El setter es privado: EF lo escribe igual a través del campo de respaldo
        b.Property(p => p.Stock);
        b.Property(p => p.RowVersion).IsRowVersion();
        b.Ignore(p => p.StockBajo);
        b.HasOne(p => p.Categoria).WithMany(c => c.Productos)
            .HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);

        // Última línea de defensa: aunque haya un bug en el código, la base no acepta stock negativo
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Productos_Stock_NoNegativo", "[Stock] >= 0");
            t.HasCheckConstraint("CK_Productos_Precio_Positivo", "[Precio] >= 0");
            t.HasCheckConstraint("CK_Productos_StockMinimo", "[StockMinimo] >= 0");
        });
    }
}

internal class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.Property(c => c.RazonSocial).HasMaxLength(150).IsRequired();
        b.Property(c => c.Cuit).HasMaxLength(11).IsFixedLength().IsRequired();
        b.HasIndex(c => c.Cuit).IsUnique();
        b.Property(c => c.Email).HasMaxLength(150);
        b.Property(c => c.Telefono).HasMaxLength(40);
        b.Property(c => c.Direccion).HasMaxLength(200);
    }
}

internal class PedidoConfig : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.Property(p => p.Observaciones).HasMaxLength(500);
        b.Property(p => p.MotivoCancelacion).HasMaxLength(300);
        b.Property(p => p.RowVersion).IsRowVersion();
        b.Ignore(p => p.Total);
        b.Ignore(p => p.EstadosSiguientes);
        b.Ignore(p => p.DescontoStock);
        b.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Vendedor).WithMany().HasForeignKey(p => p.VendedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Renglones).WithOne().HasForeignKey(r => r.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.Estado, p.FechaCreacion });
        b.HasIndex(p => p.FechaConfirmacion);
    }
}

internal class RenglonConfig : IEntityTypeConfiguration<RenglonPedido>
{
    public void Configure(EntityTypeBuilder<RenglonPedido> b)
    {
        b.ToTable("RenglonesPedido", t => t.HasCheckConstraint("CK_Renglones_Cantidad", "[Cantidad] > 0"));
        b.Property(r => r.PrecioUnitario).HasPrecision(18, 2);
        b.Ignore(r => r.Subtotal);
        b.HasOne(r => r.Producto).WithMany().HasForeignKey(r => r.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.PedidoId, r.ProductoId }).IsUnique();
    }
}

internal class MovimientoConfig : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> b)
    {
        b.ToTable("MovimientosStock");
        b.Property(m => m.Motivo).HasMaxLength(300);
        b.HasOne(m => m.Producto).WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pedido>().WithMany().HasForeignKey(m => m.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => new { m.ProductoId, m.Fecha });
    }
}

internal class VentaMesProductoConfig : IEntityTypeConfiguration<VentaMesProducto>
{
    public void Configure(EntityTypeBuilder<VentaMesProducto> b)
    {
        // Entidad sin clave mapeada a una vista: EF no la crea, la crea la migración con SQL
        b.HasNoKey().ToView("vw_VentasPorMesProducto");
        b.Property(v => v.Importe).HasPrecision(18, 2);
    }
}
