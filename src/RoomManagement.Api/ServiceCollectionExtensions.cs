using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using RoomManagement.Api.Errors;

namespace RoomManagement.Api;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            // Enums travel as their names ("Admin"), never as numbers.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            // A body that leaves out a property is a bad request, not a request with default values.
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
        });

        // Unreadable requests (broken JSON, missing body, ...) are thrown as exceptions in every environment, so
        // ApiExceptionHandler can answer them with problem details instead of an empty 400.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Room Management API";
            document.Info.Description = "Rooms, users, bookings and NFC card check-in.";
            return Task.CompletedTask;
        }));

        return services;
    }

    public static string GetRequiredConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "No database configured: set the connection string 'ConnectionStrings:Default' " +
            "(environment variable ConnectionStrings__Default).");
}
