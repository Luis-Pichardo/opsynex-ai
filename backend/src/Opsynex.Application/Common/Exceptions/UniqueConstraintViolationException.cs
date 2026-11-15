namespace Opsynex.Application.Common.Exceptions;

/// <summary>Se intentó guardar un registro duplicado (restricción única de la base de datos).</summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
        : base("Ya existe un registro con los mismos valores únicos.")
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
