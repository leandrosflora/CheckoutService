using System.Data;
using System.Reflection;
using CheckoutService.Application;
using CheckoutService.Domain;
using CheckoutService.Infrastructure.Database;
using Dapper;

namespace CheckoutService.Infrastructure.Repositories;

public sealed class CheckoutRepository : ICheckoutRepository
{
    private readonly IDatabaseContext _databaseContext;
    private readonly List<CheckoutSession> _pendingCheckouts = new();

    public CheckoutRepository(IDatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<CheckoutSession?> GetByIdAsync(
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        const string checkoutSql = @"
            select
                id as Id,
                buyer_id as BuyerId,
                seller_id as SellerId,
                status as Status,
                shipping_promise_id as ShippingPromiseId,
                shipping_mode as ShippingMode,
                carrier as Carrier,
                estimated_delivery_date as EstimatedDeliveryDate,
                items_total as ItemsTotal,
                shipping_cost as ShippingCost,
                total_amount as TotalAmount,
                idempotency_key as IdempotencyKey,
                confirmation_idempotency_key as ConfirmationIdempotencyKey,
                payment_intent_id as PaymentIntentId,
                created_at as CreatedAt,
                expires_at as ExpiresAt,
                confirmed_at as ConfirmedAt
            from checkouts
            where id = @CheckoutId";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);

        var checkoutRow = await _databaseContext.Connection
            .QueryFirstOrDefaultAsync<CheckoutRow>(checkoutSql, new { CheckoutId = checkoutId });

        if (checkoutRow is null)
        {
            return null;
        }

        var items = await GetItemsAsync(checkoutId, cancellationToken);
        return BuildCheckoutSession(checkoutRow, items);
    }

    public async Task<CheckoutSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string checkoutSql = @"
            select
                id as Id,
                buyer_id as BuyerId,
                seller_id as SellerId,
                status as Status,
                shipping_promise_id as ShippingPromiseId,
                shipping_mode as ShippingMode,
                carrier as Carrier,
                estimated_delivery_date as EstimatedDeliveryDate,
                items_total as ItemsTotal,
                shipping_cost as ShippingCost,
                total_amount as TotalAmount,
                idempotency_key as IdempotencyKey,
                confirmation_idempotency_key as ConfirmationIdempotencyKey,
                payment_intent_id as PaymentIntentId,
                created_at as CreatedAt,
                expires_at as ExpiresAt,
                confirmed_at as ConfirmedAt
            from checkouts
            where idempotency_key = @IdempotencyKey";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);

        var checkoutRow = await _databaseContext.Connection
            .QueryFirstOrDefaultAsync<CheckoutRow>(checkoutSql, new { IdempotencyKey = idempotencyKey });

        if (checkoutRow is null)
        {
            return null;
        }

        var items = await GetItemsAsync(checkoutRow.Id, cancellationToken);
        return BuildCheckoutSession(checkoutRow, items);
    }

    public async Task<CheckoutSession?> FindConfirmedByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string checkoutSql = @"
            select
                id as Id,
                buyer_id as BuyerId,
                seller_id as SellerId,
                status as Status,
                shipping_promise_id as ShippingPromiseId,
                shipping_mode as ShippingMode,
                carrier as Carrier,
                estimated_delivery_date as EstimatedDeliveryDate,
                items_total as ItemsTotal,
                shipping_cost as ShippingCost,
                total_amount as TotalAmount,
                idempotency_key as IdempotencyKey,
                confirmation_idempotency_key as ConfirmationIdempotencyKey,
                payment_intent_id as PaymentIntentId,
                created_at as CreatedAt,
                expires_at as ExpiresAt,
                confirmed_at as ConfirmedAt
            from checkouts
            where confirmation_idempotency_key = @IdempotencyKey
              and status = @Status";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);

        var checkoutRow = await _databaseContext.Connection
            .QueryFirstOrDefaultAsync<CheckoutRow>(checkoutSql, new { IdempotencyKey = idempotencyKey, Status = CheckoutStatus.Confirmed.ToString() });

        if (checkoutRow is null)
        {
            return null;
        }

        var items = await GetItemsAsync(checkoutRow.Id, cancellationToken);
        return BuildCheckoutSession(checkoutRow, items);
    }

    public Task AddAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken)
    {
        _pendingCheckouts.Add(checkout);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken)
    {
        await _databaseContext.EnsureTransactionAsync(cancellationToken);

        const string updateCheckoutSql = @"
            update checkouts
            set
                status = @Status,
                shipping_promise_id = @ShippingPromiseId,
                shipping_mode = @ShippingMode,
                carrier = @Carrier,
                estimated_delivery_date = @EstimatedDeliveryDate,
                items_total = @ItemsTotal,
                shipping_cost = @ShippingCost,
                total_amount = @TotalAmount,
                confirmation_idempotency_key = @ConfirmationIdempotencyKey,
                payment_intent_id = @PaymentIntentId,
                updated_at = @UpdatedAt,
                expires_at = @ExpiresAt,
                confirmed_at = @ConfirmedAt
            where id = @Id";

        await _databaseContext.Connection.ExecuteAsync(
            updateCheckoutSql,
            new
            {
                checkout.Id,
                Status = checkout.Status.ToString(),
                checkout.ShippingPromiseId,
                checkout.ShippingMode,
                checkout.Carrier,
                checkout.EstimatedDeliveryDate,
                checkout.ItemsTotal,
                checkout.ShippingCost,
                checkout.TotalAmount,
                checkout.ConfirmationIdempotencyKey,
                checkout.PaymentIntentId,
                UpdatedAt = DateTimeOffset.UtcNow,
                checkout.ExpiresAt,
                checkout.ConfirmedAt
            },
            _databaseContext.Transaction);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_pendingCheckouts.Count == 0)
        {
            await _databaseContext.CommitAsync(cancellationToken);
            return;
        }

        await _databaseContext.EnsureTransactionAsync(cancellationToken);

        const string insertCheckoutSql = @"
            insert into checkouts (
                id,
                buyer_id,
                seller_id,
                status,
                shipping_promise_id,
                shipping_mode,
                carrier,
                estimated_delivery_date,
                items_total,
                shipping_cost,
                total_amount,
                idempotency_key,
                confirmation_idempotency_key,
                payment_intent_id,
                created_at,
                updated_at,
                expires_at,
                confirmed_at
            ) values (
                @Id,
                @BuyerId,
                @SellerId,
                @Status,
                @ShippingPromiseId,
                @ShippingMode,
                @Carrier,
                @EstimatedDeliveryDate,
                @ItemsTotal,
                @ShippingCost,
                @TotalAmount,
                @IdempotencyKey,
                @ConfirmationIdempotencyKey,
                @PaymentIntentId,
                @CreatedAt,
                @UpdatedAt,
                @ExpiresAt,
                @ConfirmedAt)";
        const string insertItemSql = @"
            insert into checkout_items (
                id,
                checkout_id,
                sku_id,
                quantity,
                unit_price
            ) values (
                @Id,
                @CheckoutId,
                @SkuId,
                @Quantity,
                @UnitPrice)";

        foreach (var checkout in _pendingCheckouts)
        {
            await _databaseContext.Connection.ExecuteAsync(
                insertCheckoutSql,
                new
                {
                    checkout.Id,
                    checkout.BuyerId,
                    checkout.SellerId,
                    Status = checkout.Status.ToString(),
                    checkout.ShippingPromiseId,
                    checkout.ShippingMode,
                    checkout.Carrier,
                    checkout.EstimatedDeliveryDate,
                    checkout.ItemsTotal,
                    checkout.ShippingCost,
                    checkout.TotalAmount,
                    checkout.IdempotencyKey,
                    checkout.ConfirmationIdempotencyKey,
                    checkout.PaymentIntentId,
                    checkout.CreatedAt,
                    UpdatedAt = checkout.CreatedAt,
                    checkout.ExpiresAt,
                    checkout.ConfirmedAt
                },
                _databaseContext.Transaction);

            var items = checkout.Items.Select(item => new
            {
                item.Id,
                CheckoutId = checkout.Id,
                item.SkuId,
                item.Quantity,
                item.UnitPrice
            });

            await _databaseContext.Connection.ExecuteAsync(insertItemSql, items, _databaseContext.Transaction);
        }

        _pendingCheckouts.Clear();
        await _databaseContext.CommitAsync(cancellationToken);
    }

    private async Task<IEnumerable<CheckoutItemRow>> GetItemsAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                id as Id,
                sku_id as SkuId,
                quantity as Quantity,
                unit_price as UnitPrice
            from checkout_items
            where checkout_id = @CheckoutId";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);
        return await _databaseContext.Connection.QueryAsync<CheckoutItemRow>(sql, new { CheckoutId = checkoutId });
    }

    private static CheckoutSession BuildCheckoutSession(CheckoutRow row, IEnumerable<CheckoutItemRow> items)
    {
        var checkout = (CheckoutSession)Activator.CreateInstance(typeof(CheckoutSession), true)!;

        SetProperty(checkout, nameof(CheckoutSession.Id), row.Id);
        SetProperty(checkout, nameof(CheckoutSession.BuyerId), row.BuyerId);
        SetProperty(checkout, nameof(CheckoutSession.SellerId), row.SellerId);
        SetProperty(checkout, nameof(CheckoutSession.Status), Enum.Parse<CheckoutStatus>(row.Status, ignoreCase: true));
        SetProperty(checkout, nameof(CheckoutSession.ShippingPromiseId), row.ShippingPromiseId);
        SetProperty(checkout, nameof(CheckoutSession.ShippingMode), row.ShippingMode);
        SetProperty(checkout, nameof(CheckoutSession.Carrier), row.Carrier);
        SetProperty(checkout, nameof(CheckoutSession.EstimatedDeliveryDate), row.EstimatedDeliveryDate);
        SetProperty(checkout, nameof(CheckoutSession.ItemsTotal), row.ItemsTotal);
        SetProperty(checkout, nameof(CheckoutSession.ShippingCost), row.ShippingCost);
        SetProperty(checkout, nameof(CheckoutSession.TotalAmount), row.TotalAmount);
        SetProperty(checkout, nameof(CheckoutSession.IdempotencyKey), row.IdempotencyKey);
        SetProperty(checkout, nameof(CheckoutSession.ConfirmationIdempotencyKey), row.ConfirmationIdempotencyKey);
        SetProperty(checkout, nameof(CheckoutSession.PaymentIntentId), row.PaymentIntentId);
        SetProperty(checkout, nameof(CheckoutSession.CreatedAt), row.CreatedAt);
        SetProperty(checkout, nameof(CheckoutSession.ExpiresAt), row.ExpiresAt);
        SetProperty(checkout, nameof(CheckoutSession.ConfirmedAt), row.ConfirmedAt);
        SetProperty(checkout, nameof(CheckoutSession.Items), items.Select(BuildCheckoutItem).ToList());

        return checkout;
    }

    private static CheckoutItem BuildCheckoutItem(CheckoutItemRow itemRow)
    {
        var item = new CheckoutItem(itemRow.SkuId, itemRow.Quantity, itemRow.UnitPrice);
        SetProperty(item, nameof(CheckoutItem.Id), itemRow.Id);
        return item;
    }

    private static void SetProperty<T>(T target, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
        {
            return;
        }

        property.SetValue(target, value);
    }

    private sealed class CheckoutRow
    {
        public Guid Id { get; set; }
        public Guid BuyerId { get; set; }
        public Guid SellerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ShippingPromiseId { get; set; } = string.Empty;
        public string ShippingMode { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
        public DateOnly EstimatedDeliveryDate { get; set; }
        public decimal ItemsTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string? ConfirmationIdempotencyKey { get; set; }
        public string? PaymentIntentId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
    }

    private sealed class CheckoutItemRow
    {
        public Guid Id { get; set; }
        public Guid SkuId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
