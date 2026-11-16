using Microsoft.Extensions.DependencyInjection;
using Arvan.Capacity.Application.Flows.Reservations;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Events;
using Arvan.Capacity.Application.Operations.Implementations.Events;
using Arvan.Capacity.Application.Operations.Implementations.Pools;
using Arvan.Capacity.Application.Operations.Implementations.Quotas;
using Arvan.Capacity.Application.Operations.Implementations.Reconciliation;
using Arvan.Capacity.Application.Operations.Pools;
using Arvan.Capacity.Application.Operations.Quotas;
using Arvan.Capacity.Application.Operations.Reconciliation;
using Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;
using Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;
using Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;
using Arvan.Capacity.Application.UseCases.Admin.GetReconciliation;
using Arvan.Capacity.Application.UseCases.Admin.PutInventory;
using Arvan.Capacity.Application.UseCases.Admin.PutPool;
using Arvan.Capacity.Application.UseCases.Admin.PutProduct;
using Arvan.Capacity.Application.UseCases.Admin.PutQuota;
using Arvan.Capacity.Application.UseCases.Admin.ReconcileLedger;
using Arvan.Capacity.Application.UseCases.Capacity.FitCheck;
using Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;
using Arvan.Capacity.Application.UseCases.Events.RelayEvents;
using Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;
using Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Implementations.Capacity;
using Arvan.Capacity.Application.Operations.Implementations.Catalog;
using Arvan.Capacity.Application.Operations.Implementations.Idempotency;
using Arvan.Capacity.Application.Operations.Implementations.Reservations;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Application.UseCases.Reservations.CancelReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ConfirmReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;
using Arvan.Capacity.Application.UseCases.Reservations.ExtendReservation;
using Arvan.Capacity.Application.UseCases.Reservations.GetReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ListReservations;
using Arvan.Capacity.Application.UseCases.Reservations.PurgeIdempotencyKeys;
using Arvan.Capacity.Application.UseCases.Reservations.ReleaseReservation;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;

namespace Arvan.Capacity.Application;

/// <summary>
/// Wires the Capacity Application layer into the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers every Operation, Flow and UseCase. Operations register interface to
    /// implementation; Flows and UseCases register as themselves. Repository implementations are
    /// registered separately by Infrastructure's <c>AddCapacityPersistence</c>.
    /// </summary>
    public static void AddCapacityApplication(this IServiceCollection services)
    {
        services.AddSingleton<SoldOutCache>();
        services.AddScoped<IReservationOperations, ReservationOperations>();
        services.AddScoped<ICapacityOperations, CapacityOperations>();
        services.AddScoped<ICatalogOperations, CatalogOperations>();
        services.AddScoped<IIdempotencyOperations, IdempotencyOperations>();
        services.AddScoped<IPoolOperations, PoolOperations>();
        services.AddScoped<IQuotaOperations, QuotaOperations>();
        services.AddScoped<IReconciliationOperations, ReconciliationOperations>();
        services.AddScoped<IOutboxOperations, OutboxOperations>();

        services.AddScoped<ExpireHeldFlow>();

        services.AddScoped<SubmitReservationUseCase>();
        services.AddScoped<ConfirmReservationUseCase>();
        services.AddScoped<CancelReservationUseCase>();
        services.AddScoped<ExtendReservationUseCase>();
        services.AddScoped<ReleaseReservationUseCase>();
        services.AddScoped<GetReservationUseCase>();
        services.AddScoped<ListReservationsUseCase>();
        services.AddScoped<ExpireHoldsUseCase>();
        services.AddScoped<PurgeIdempotencyKeysUseCase>();
        services.AddScoped<PromoteWaitingUseCase>();
        services.AddScoped<TimeOutWaitingUseCase>();
        services.AddScoped<RelayEventsUseCase>();

        services.AddScoped<GetCapacityLevelsUseCase>();
        services.AddScoped<FitCheckUseCase>();

        services.AddScoped<PutPoolUseCase>();
        services.AddScoped<ChangePoolStateUseCase>();
        services.AddScoped<PutInventoryUseCase>();
        services.AddScoped<PutProductUseCase>();
        services.AddScoped<PutQuotaUseCase>();
        services.AddScoped<AdjustLedgerUseCase>();
        services.AddScoped<GetReconciliationUseCase>();
        services.AddScoped<ReconcileLedgerUseCase>();
        services.AddScoped<CancelOpenReservationsUseCase>();
    }
}
