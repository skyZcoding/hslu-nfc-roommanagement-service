using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Users;

namespace RoomManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>(ServiceLifetime.Singleton);

        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<DeactivateUserHandler>();

        services.AddScoped<CreateRoomHandler>();
        services.AddScoped<GetRoomHandler>();
        services.AddScoped<GetRoomByReaderHandler>();
        services.AddScoped<ListRoomsHandler>();
        services.AddScoped<UpdateRoomHandler>();
        services.AddScoped<DeactivateRoomHandler>();
        services.AddScoped<AssignReaderHandler>();
        services.AddScoped<RemoveReaderHandler>();

        return services;
    }
}
