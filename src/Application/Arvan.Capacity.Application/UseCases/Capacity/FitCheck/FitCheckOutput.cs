using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Capacity.FitCheck;

/// <summary>
/// The output of <see cref="FitCheckUseCase"/>. Advisory: only a hold guarantees capacity, and the
/// answer never states how much is free.
/// </summary>
/// <param name="Fits">Whether a submit now would be held.</param>
/// <param name="Code">The reason it would not, when it would not.</param>
/// <param name="Lines">The reason per short line.</param>
public sealed record FitCheckOutput(bool Fits, ReasonCode? Code, IReadOnlyList<FitCheckLineOutput> Lines);

/// <summary>
/// One short line of a fit-check.
/// </summary>
/// <param name="ResourceClass">The class.</param>
/// <param name="Code">Why it would not be held.</param>
public sealed record FitCheckLineOutput(ResourceClass ResourceClass, ReasonCode Code);
