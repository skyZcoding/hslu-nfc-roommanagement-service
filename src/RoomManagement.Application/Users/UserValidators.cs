using FluentValidation;
using RoomManagement.Domain.Common;
using RoomManagement.Domain.Users;

namespace RoomManagement.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(User.MaxEmailLength)
            .Must(BeValidEmail).WithMessage("'{PropertyValue}' is not a valid email address.");

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
        RuleFor(x => x.Role).IsInEnum();
    }

    // The domain owns the definition of a valid email; the validator only reports it per field.
    private static bool BeValidEmail(string? email)
    {
        try
        {
            User.NormalizeEmail(email);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
        RuleFor(x => x.Role).IsInEnum();
    }
}
