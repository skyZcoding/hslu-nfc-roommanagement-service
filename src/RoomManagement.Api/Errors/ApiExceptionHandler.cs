using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using RoomManagement.Application.Common;
using RoomManagement.Domain.Common;

namespace RoomManagement.Api.Errors;

/// <summary>
/// Turns the exceptions that are really client errors into problem details. Anything else is left to the
/// framework, which logs it and answers 500 without exposing internals.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            // A concurrent request won the race that the handlers check for up front.
            DataConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict.Message, conflict.Code),

            InvalidStateTransitionException invalidState =>
                Problem(StatusCodes.Status409Conflict, invalidState.Message, "invalid_state"),

            // Validators should have caught this; reaching here means a rule exists only in the domain.
            DomainException domain => Problem(StatusCodes.Status400BadRequest, domain.Message, "invalid_request"),

            BadHttpRequestException badRequest =>
                Problem(badRequest.StatusCode, DescribeBadRequest(badRequest), "bad_request"),

            _ => null,
        };

        if (problem is null)
            return false;

        if (exception is DomainException and not InvalidStateTransitionException)
            logger.LogWarning(exception, "A domain rule rejected a request that passed validation");

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string detail, string code) => new()
    {
        Status = status,
        Title = ReasonPhrases.GetReasonPhrase(status),
        Detail = detail,
        Extensions = { ["code"] = code },
    };

    // The JSON error says which property is missing or invalid, which is what a client needs to fix the request.
    private static string DescribeBadRequest(BadHttpRequestException exception) =>
        exception.InnerException is JsonException json
            ? $"The request body is not valid: {json.Message}"
            : "The request could not be understood.";
}
