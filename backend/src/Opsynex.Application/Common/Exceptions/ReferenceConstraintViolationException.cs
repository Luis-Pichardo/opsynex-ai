namespace Opsynex.Application.Common.Exceptions;

/// <summary>Se intentó borrar un registro que otros registros referencian (clave foránea).</summary>
public sealed class ReferenceConstraintViolationException : Exception
{
    public ReferenceConstraintViolationException()
        : base("El registro está en uso por otros datos y no se puede eliminar.")
    {
    }

    public ReferenceConstraintViolationException(string message)
        : base(message)
    {
    }

    public ReferenceConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
