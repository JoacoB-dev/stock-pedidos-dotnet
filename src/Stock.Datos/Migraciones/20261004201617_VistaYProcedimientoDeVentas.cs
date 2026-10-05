using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stock.Datos.Migraciones
{
    /// <summary>
    /// Crea la vista y el stored procedure de reportes. EF no los genera solo,
    /// así que van como SQL dentro de la migración (y se versionan con el resto del esquema).
    /// Cuenta como venta todo pedido confirmado que no se canceló.
    /// </summary>
    public partial class VistaYProcedimientoDeVentas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_VentasPorMesProducto
                AS
                SELECT
                    YEAR(p.FechaConfirmacion)  AS Anio,
                    MONTH(p.FechaConfirmacion) AS Mes,
                    pr.Id                      AS ProductoId,
                    pr.Sku,
                    pr.Nombre                  AS Producto,
                    c.Nombre                   AS Categoria,
                    SUM(r.Cantidad)                              AS Unidades,
                    CAST(SUM(r.Cantidad * r.PrecioUnitario) AS decimal(18,2)) AS Importe,
                    COUNT(DISTINCT p.Id)                         AS Pedidos
                FROM dbo.Pedidos p
                JOIN dbo.RenglonesPedido r ON r.PedidoId = p.Id
                JOIN dbo.Productos pr      ON pr.Id = r.ProductoId
                JOIN dbo.Categorias c      ON c.Id = pr.CategoriaId
                WHERE p.Estado IN (N'Confirmado', N'Preparado', N'Entregado')
                GROUP BY YEAR(p.FechaConfirmacion), MONTH(p.FechaConfirmacion),
                         pr.Id, pr.Sku, pr.Nombre, c.Nombre;
                """);

            migrationBuilder.Sql("""
                CREATE PROCEDURE dbo.sp_ResumenVentasMensual
                    @Desde datetime2,
                    @Hasta datetime2
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @Hasta < @Desde
                        THROW 50001, N'La fecha hasta no puede ser anterior a la fecha desde.', 1;

                    -- Primero el total por pedido y después se agrupa por mes,
                    -- así el ticket promedio es por pedido y no por renglón
                    WITH TotalesPorPedido AS (
                        SELECT p.Id,
                               p.FechaConfirmacion,
                               SUM(r.Cantidad)                    AS Unidades,
                               SUM(r.Cantidad * r.PrecioUnitario) AS Importe
                        FROM dbo.Pedidos p
                        JOIN dbo.RenglonesPedido r ON r.PedidoId = p.Id
                        WHERE p.Estado IN (N'Confirmado', N'Preparado', N'Entregado')
                          AND p.FechaConfirmacion >= @Desde
                          AND p.FechaConfirmacion <  @Hasta
                        GROUP BY p.Id, p.FechaConfirmacion
                    )
                    SELECT YEAR(FechaConfirmacion)                       AS Anio,
                           MONTH(FechaConfirmacion)                      AS Mes,
                           COUNT(*)                                      AS Pedidos,
                           SUM(Unidades)                                 AS Unidades,
                           CAST(SUM(Importe) AS decimal(18,2))           AS Importe,
                           CAST(AVG(Importe) AS decimal(18,2))           AS TicketPromedio
                    FROM TotalesPorPedido
                    GROUP BY YEAR(FechaConfirmacion), MONTH(FechaConfirmacion)
                    ORDER BY Anio, Mes;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ResumenVentasMensual;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_VentasPorMesProducto;");
        }
    }
}
