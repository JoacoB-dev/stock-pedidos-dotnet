namespace Stock.Dominio;

/// <summary>Validación de CUIT argentino (11 dígitos con dígito verificador).</summary>
public static class Cuit
{
    public static string Normalizar(string? cuit) =>
        new((cuit ?? "").Where(char.IsDigit).ToArray());

    public static bool EsValido(string? cuit)
    {
        var digitos = Normalizar(cuit);
        if (digitos.Length != 11) return false;

        int[] pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var suma = 0;
        for (var i = 0; i < 10; i++)
            suma += (digitos[i] - '0') * pesos[i];

        var resto = 11 - suma % 11;
        // Si da 10 el número no es válido (AFIP asigna otro prefijo en ese caso)
        var verificador = resto switch { 11 => 0, 10 => -1, _ => resto };
        return verificador == digitos[10] - '0';
    }
}
