using RoomManagement.Api;
using RoomManagement.Api.Endpoints;
using RoomManagement.Application;
using RoomManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(serviceProvider =>
    serviceProvider.GetRequiredService<IConfiguration>().GetRequiredConnectionString());
builder.Services.AddApi();

var app = builder.Build();

// Fail at startup rather than on the first request if the database is not configured.
_ = app.Configuration.GetRequiredConnectionString();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Room Management API v1"));
}

app.MapHealthEndpoints();

var api = app.MapGroup("/api/v1");
api.MapUserEndpoints();
api.MapRoomEndpoints();
api.MapBookingEndpoints();
api.MapReaderEndpoints();

app.Run();

// Lets the integration tests start the app through WebApplicationFactory<Program>.
public partial class Program;
