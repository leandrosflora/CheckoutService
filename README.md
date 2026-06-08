# CheckoutService

Checkout Service implemented as a lightweight transactional orchestrator using ASP.NET Core Minimal APIs.

## Responsibilities

- Create checkout sessions from a cart.
- Validate buyer, seller, items, and shipping address inputs.
- Call the Shipping Promise Service for delivery promise and shipping cost.
- Persist checkout state with EF Core/PostgreSQL.
- Enforce idempotency for checkout creation and confirmation.
- Persist integration events through an outbox table.
- Expose checkout status and health checks.

## Out of scope

This service does not calculate routes, freight, SLA, tracking, shipment creation, carrier integrations, labels, or real stock deduction.

## Endpoints

- `POST /checkouts` — creates a checkout session. Requires `Idempotency-Key` header.
- `GET /checkouts/{checkoutId}` — returns checkout status and totals.
- `POST /checkouts/{checkoutId}/confirm` — confirms a checkout. Requires `Idempotency-Key` header.
- `GET /health` — exposes health checks.

## Configuration

```json
{
  "ConnectionStrings": {
    "CheckoutDb": "Host=localhost;Port=5432;Database=checkout;Username=checkout;Password=checkout"
  },
  "Services": {
    "ShippingPromise": "https://shipping-promise.local"
  }
}
```

## Event publication

The checkout flow writes `CheckoutCreated` and `CheckoutConfirmed` messages to `outbox_messages` in the same EF Core unit of work as checkout persistence. A separate worker should publish pending outbox records to Kafka or another broker and then mark them as processed.
