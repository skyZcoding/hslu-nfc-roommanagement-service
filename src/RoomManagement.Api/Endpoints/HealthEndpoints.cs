using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace RoomManagement.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness: the process is up and answering. No dependency is checked, so a database outage never
        // makes an orchestrator restart a healthy container.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        // Readiness: the app can do its job, which needs the database.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        return app;
    }
}
