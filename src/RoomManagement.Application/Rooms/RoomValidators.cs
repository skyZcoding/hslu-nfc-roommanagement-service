using FluentValidation;
using RoomManagement.Domain.Rooms;

namespace RoomManagement.Application.Rooms;

public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Room.MaxNameLength);
        RuleFor(x => x.Building).NotEmpty().MaximumLength(Room.MaxBuildingLength);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}

public sealed class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Room.MaxNameLength);
        RuleFor(x => x.Building).NotEmpty().MaximumLength(Room.MaxBuildingLength);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}

public sealed class AssignReaderRequestValidator : AbstractValidator<AssignReaderRequest>
{
    public AssignReaderRequestValidator()
    {
        // The domain owns the definition of a valid reader id and its error messages.
        RuleFor(x => x.ReaderId).Custom((value, context) =>
        {
            if (!ReaderId.TryCreate(value, out _, out var error))
                context.AddFailure(error);
        });
    }
}
