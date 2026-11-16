using Arvan.Capacity.Domain.Common;

namespace Arvan.Capacity.Http;

/// <summary>
/// The identity the gateway attached to a request.
/// </summary>
/// <param name="CustomerId">The customer the call concerns.</param>
/// <param name="Actor">Who is calling: the customer, a service acting for one, or an operator.</param>
public sealed record Caller(string CustomerId, Actor Actor);
