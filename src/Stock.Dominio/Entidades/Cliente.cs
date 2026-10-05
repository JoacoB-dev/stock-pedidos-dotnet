namespace Stock.Dominio.Entidades;

public class Cliente
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = "";
    /// <summary>CUIT sin guiones (11 dígitos).</summary>
    public string Cuit { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
}
