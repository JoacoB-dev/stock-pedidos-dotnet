namespace Stock.Dominio.Entidades;

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string HashClave { get; set; } = "";
    public Rol Rol { get; set; }
    public bool Activo { get; set; } = true;
}
