using Arvan.Capacity.ViewModels.Reservations;
using FluentValidation;

namespace Arvan.Capacity.Validators.Reservations;

/// <summary>
/// Synchronous shape rules for <see cref="ListReservationsRequest"/>.
/// </summary>
public sealed class ListReservationsRequestValidator : AbstractValidator<ListReservationsRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public ListReservationsRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleForEach(x => x.Status).IsInEnum();
    }
}
