CREATE TABLE checkouts (
    id UUID PRIMARY KEY,
    buyer_id UUID NOT NULL,
    seller_id UUID NOT NULL,
    status VARCHAR(30) NOT NULL,
    shipping_promise_id VARCHAR(200) NOT NULL,
    shipping_mode VARCHAR(100) NOT NULL,
    carrier VARCHAR(100) NOT NULL,
    estimated_delivery_date DATE NOT NULL,
    items_total NUMERIC(18,2) NOT NULL,
    shipping_cost NUMERIC(18,2) NOT NULL,
    total_amount NUMERIC(18,2) NOT NULL,
    idempotency_key VARCHAR(200) NOT NULL,
    confirmation_idempotency_key VARCHAR(200) NULL,
    payment_intent_id VARCHAR(200) NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    confirmed_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_checkouts_idempotency UNIQUE (idempotency_key),
    CONSTRAINT uq_checkouts_confirmation_idempotency UNIQUE (confirmation_idempotency_key)
);

CREATE TABLE checkout_items (
    id UUID PRIMARY KEY,
    "CheckoutId" UUID NOT NULL REFERENCES checkouts(id),
    sku_id UUID NOT NULL,
    quantity INTEGER NOT NULL,
    unit_price NUMERIC(18,2) NOT NULL
);

CREATE INDEX idx_checkout_items_checkout ON checkout_items ("CheckoutId");

CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    event_type VARCHAR(200) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NULL
);

CREATE INDEX idx_outbox_messages_dispatch ON outbox_messages (processed_at);

CREATE TABLE shipping_promise_projections (
    id UUID PRIMARY KEY,
    event_id UUID NOT NULL,
    correlation_id VARCHAR(200) NOT NULL,
    checkout_id UUID NULL,
    promise_id VARCHAR(200) NOT NULL,
    mode VARCHAR(100) NOT NULL,
    carrier VARCHAR(100) NOT NULL,
    cost NUMERIC(18,2) NOT NULL,
    estimated_delivery_date DATE NULL,
    created_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_shipping_promise_projections_event UNIQUE (event_id),
    CONSTRAINT uq_shipping_promise_projections_key UNIQUE (correlation_id, checkout_id)
);
