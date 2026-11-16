using Arvan.Capacity.ViewModels.Reservations;
using FluentValidation;

namespace Arvan.Capacity.Validators.Reservations;

/// <summary>
/// Synchronous shape rules for <see cref="ReleaseReservationRequest"/>.
/// </summary>
public sealed class ReleaseReservationRequestValidator : AbstractValidator<ReleaseReservationRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public ReleaseReservationRequestValidator() => RuleFor(x => x.Reason).IsInEnum();
}
