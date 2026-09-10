using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Problems;

/// <summary>
/// Problem Details payload for 500 responses. Exposing the raised exception's details is a
/// deliberate trade-off for ease of debugging in this API, mirroring code-rag-api's contract,
/// and should be disabled or redacted in a hardened deployment.
/// </summary>
public sealed class ServerErrorProblemDetails : ProblemDetails
{
    /// <summary>Details of the unhandled exception that caused the request to fail.</summary>
    public ExceptionDetails Exception { get; init; } = null!;

    /// <summary>Details of the unhandled exception that caused the request to fail.</summary>
    /// <param name="ExceptionType">Fully-qualified type name of the exception that was raised.</param>
    /// <param name="Message">The exception's message.</param>
    /// <param name="StackTrace">The exception's captured stack trace.</param>
    public sealed record ExceptionDetails(string ExceptionType, string Message, string? StackTrace);
}
