using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Application.Bookings;
using RoomManagement.Application.Rooms;
using RoomManagement.Application.Scans;
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

        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<GetBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CheckInBookingHandler>();
        services.AddScoped<ListRoomBookingsHandler>();
        services.AddScoped<ListUserBookingsHandler>();
        services.AddScoped<GetRoomAvailabilityHandler>();

        services.AddScoped<ProcessCardScanHandler>();

        return services;
    }
}
