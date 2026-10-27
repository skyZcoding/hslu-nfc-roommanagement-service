using FluentValidation;
using FluentValidation.Results;

namespace RoomManagement.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Validates <paramref name="instance"/> and returns a validation <see cref="Error"/>, or null if valid.</summary>
    public static async Task<Error?> ValidateToErrorAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid ? null : ToError(result);
    }

    private static Error ToError(ValidationResult result) =>
        Error.Validation(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
}
