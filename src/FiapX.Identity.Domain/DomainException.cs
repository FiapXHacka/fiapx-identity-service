namespace FiapX.Identity.Domain;

/// <summary>A business rule was violated. The API maps it to 400 ProblemDetails.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
