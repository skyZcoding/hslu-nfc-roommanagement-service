using FluentValidation;

namespace RoomManagement.Application.Bookings;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Start).NotEmpty();
        RuleFor(x => x.End)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .GreaterThan(x => x.Start).WithMessage("'End' must be after 'Start'.");
    }
}

public sealed class ListBookingsQueryValidator : AbstractValidator<ListBookingsQuery>
{
    public ListBookingsQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThan(x => x.From).WithMessage("'To' must be after 'From'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    public AvailabilityQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .GreaterThan(x => x.From).WithMessage("'To' must be after 'From'.");
    }
}
