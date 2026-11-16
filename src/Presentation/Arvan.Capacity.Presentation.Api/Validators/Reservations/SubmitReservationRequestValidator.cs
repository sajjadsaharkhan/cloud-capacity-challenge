using Arvan.Capacity.Domain.Constants;
using Arvan.Capacity.ViewModels.Reservations;
using FluentValidation;

namespace Arvan.Capacity.Validators.Reservations;

/// <summary>
/// Synchronous shape rules for <see cref="SubmitReservationRequest"/>. Whether a quantity meets
/// its class's minimum, step and maximum depends on the inventory, so the domain decides that.
/// </summary>
public sealed class SubmitReservationRequestValidator : AbstractValidator<SubmitReservationRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public SubmitReservationRequestValidator()
    {
        RuleFor(x => x.Product).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Zone).MaximumLength(64);
        RuleFor(x => x.ClientReference).MaximumLength(ReservationConstant.ClientReferenceMaxLength);
        RuleFor(x => x.OnShortage).IsInEnum();

        RuleFor(x => x.Lines)
            .NotEmpty()
            .Must(lines => lines.Count <= ReservationConstant.MaxLines)
            .WithMessage($"A reservation carries at most {ReservationConstant.MaxLines} lines.")
            .Must(lines => lines.Select(l => l.ResourceClass).Distinct().Count() == lines.Count)
            .WithMessage("Each resource class may appear once.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ResourceClass).IsInEnum();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}
