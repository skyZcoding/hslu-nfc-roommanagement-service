using System.Text.Json;
using RoomManagement.Application.Common;

namespace RoomManagement.Api.Errors;

/// <summary>Maps the outcome of an application handler to an HTTP response; failures become problem details.</summary>
internal static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error!.ToProblem();

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                CamelCaseKeys(error.ValidationErrors ?? new Dictionary<string, string[]>()), extensions: extensions),
            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: error.Message, extensions: extensions),
            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: error.Message, extensions: extensions),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unknown error type."),
        };
    }

    // Field names are reported the way the JSON body spells them: "displayName", not "DisplayName".
    private static Dictionary<string, string[]> CamelCaseKeys(IReadOnlyDictionary<string, string[]> errors) =>
        errors.ToDictionary(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value);
}
