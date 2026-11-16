using Arvan.Capacity.Domain.Constants;
using Arvan.Capacity.ViewModels.Capacity;
using FluentValidation;

namespace Arvan.Capacity.Validators.Capacity;

/// <summary>
/// Synchronous shape rules for <see cref="FitCheckRequest"/>.
/// </summary>
public sealed class FitCheckRequestValidator : AbstractValidator<FitCheckRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public FitCheckRequestValidator()
    {
        RuleFor(x => x.Product).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Zone).MaximumLength(64);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= ReservationConstant.MaxLines)
            .Must(l => l.Select(x => x.ResourceClass).Distinct().Count() == l.Count)
            .WithMessage("Each resource class may appear once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ResourceClass).IsInEnum();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}
