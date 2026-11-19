namespace Opsynex.Infrastructure.Parsing;

/// <summary>Tolerancia de cuadre: 1 unidad o 0,01 % del valor de referencia, lo que sea mayor.</summary>
public static class Tolerance
{
    public static decimal For(decimal reference) => Math.Max(1m, Math.Abs(reference) * 0.0001m);

    public static bool Equal(decimal a, decimal b) => Math.Abs(a - b) <= For(Math.Max(Math.Abs(a), Math.Abs(b)));
}
