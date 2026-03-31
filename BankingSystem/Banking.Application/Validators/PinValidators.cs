using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class SetPinRequestValidator : AbstractValidator<SetPinRequest>
{
    public SetPinRequestValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("PIN must contain only digits.");

        RuleFor(x => x.ConfirmPin)
            .Equal(x => x.Pin).WithMessage("PINs do not match.");
    }
}

public class ChangePinRequestValidator : AbstractValidator<ChangePinRequest>
{
    public ChangePinRequestValidator()
    {
        RuleFor(x => x.CurrentPin)
            .NotEmpty().WithMessage("Current PIN is required.")
            .Length(6);

        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("New PIN is required.")
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("PIN must contain only digits.");

        RuleFor(x => x.ConfirmNewPin)
            .Equal(x => x.NewPin).WithMessage("PINs do not match.");

        RuleFor(x => x)
            .Must(x => x.CurrentPin != x.NewPin)
            .WithMessage("New PIN must be different from current PIN.");
    }
}