using FluentValidation;
using SnapNFix.Application.Resources;
using SnapNFix.Domain.Enums;

namespace SnapNFix.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        // Validate FirstName only if provided
        RuleFor(x => x.FirstName)
            .Length(2, 100)
            .WithMessage(Shared.FirstNameLength)
            .Matches(@"^[a-zA-Z\u0600-\u06FF]+$")
            .WithMessage(Shared.FirstNameInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.FirstName));

        // Validate LastName only if provided
        RuleFor(x => x.LastName)
            .Length(2, 100)
            .WithMessage(Shared.LastNameLength)
            .Matches(@"^[a-zA-Z\u0600-\u06FF]+$")
            .WithMessage(Shared.LastNameInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.LastName));

        // Validate Gender only if provided
        RuleFor(x => x.Gender)
            .IsInEnum()
            .WithMessage(Shared.InvalidGender)
            .When(x => x.Gender.HasValue);

        // Validate BirthDate only if provided
        RuleFor(x => x.BirthDate)
            .Must(BeAValidAge)
            .WithMessage(Shared.InvalidAgeError)
            .Must(NotBeFutureDate)
            .WithMessage(Shared.FutureDateError)
            .When(x => x.BirthDate.HasValue);

        // Ensure at least one field is being updated
        RuleFor(x => x)
            .Must(HaveAtLeastOneField)
            .WithMessage("At least one field must be provided for update")
            .OverridePropertyName("UpdateFields");
    }

    private static bool BeAValidAge(DateOnly? birthDate)
    {
        if (!birthDate.HasValue) return true;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthDate.Value.Year;
        
        if (birthDate.Value > today.AddYears(-age))
            age--;

        return age >= 13 && age <= 120;
    }

    private static bool NotBeFutureDate(DateOnly? birthDate)
    {
        if (!birthDate.HasValue) return true;

        var today = DateOnly.FromDateTime(DateTime.Today);
        return birthDate.Value <= today;
    }

    private static bool HaveAtLeastOneField(UpdateUserCommand command)
    {
        return !string.IsNullOrWhiteSpace(command.FirstName) ||
               !string.IsNullOrWhiteSpace(command.LastName) ||
               command.Gender.HasValue ||
               command.BirthDate.HasValue;
    }
}