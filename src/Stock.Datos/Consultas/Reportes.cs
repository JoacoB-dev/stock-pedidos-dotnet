namespace Stock.Datos.Consultas;

/// <summary>Una fila de la vista vw_VentasPorMesProducto.</summary>
public class VentaMesProducto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int ProductoId { get; set; }
    public string Sku { get; set; } = "";
    public string Producto { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Unidades { get; set; }
    public decimal Importe { get; set; }
    public int Pedidos { get; set; }
}

/// <summary>Una fila del resultado de sp_ResumenVentasMensual.</summary>
public class ResumenVentasMes
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Pedidos { get; set; }
    public int Unidades { get; set; }
    public decimal Importe { get; set; }
    public decimal TicketPromedio { get; set; }
}
