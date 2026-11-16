using Arvan.Capacity.ViewModels.Admin;
using FluentValidation;

namespace Arvan.Capacity.Validators.Admin;

/// <summary>
/// Synchronous shape rules for <see cref="PutPoolRequest"/>.
/// </summary>
public sealed class PutPoolRequestValidator : AbstractValidator<PutPoolRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public PutPoolRequestValidator()
    {
        RuleFor(x => x.Region).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Zone).MaximumLength(64);
        RuleFor(x => x.HostRef).MaximumLength(64);
    }
}

/// <summary>
/// Synchronous shape rules for <see cref="ChangePoolStateRequest"/>.
/// </summary>
public sealed class ChangePoolStateRequestValidator : AbstractValidator<ChangePoolStateRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public ChangePoolStateRequestValidator() =>
        RuleFor(x => x.Event).NotEmpty().Must(e => e is "activate" or "take_offline" or "restore" or "retire")
            .WithMessage("Event must be activate, take_offline, restore or retire.");
}

/// <summary>
/// Synchronous shape rules for <see cref="PutInventoryRequest"/>. Ranges that relate fields to one
/// another are the domain's.
/// </summary>
public sealed class PutInventoryRequestValidator : AbstractValidator<PutInventoryRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public PutInventoryRequestValidator()
    {
        RuleFor(x => x.Total).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reserved).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AllocationRatio).GreaterThanOrEqualTo(1);
        RuleFor(x => x.MinUnit).GreaterThan(0);
        RuleFor(x => x.StepSize).GreaterThan(0);
        RuleFor(x => x.MaxUnit).GreaterThan(0);
    }
}

/// <summary>
/// Synchronous shape rules for <see cref="PutProductRequest"/>.
/// </summary>
public sealed class PutProductRequestValidator : AbstractValidator<PutProductRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public PutProductRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ResourceClasses).NotEmpty();
        RuleForEach(x => x.ResourceClasses).IsInEnum();
    }
}

/// <summary>
/// Synchronous shape rules for <see cref="PutQuotaRequest"/>.
/// </summary>
public sealed class PutQuotaRequestValidator : AbstractValidator<PutQuotaRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public PutQuotaRequestValidator() => RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
}

/// <summary>
/// Synchronous shape rules for <see cref="AdjustLedgerRequest"/>.
/// </summary>
public sealed class AdjustLedgerRequestValidator : AbstractValidator<AdjustLedgerRequest>
{
    /// <summary>
    /// Initializes the validation rules.
    /// </summary>
    public AdjustLedgerRequestValidator()
    {
        RuleFor(x => x.ResourceClass).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
