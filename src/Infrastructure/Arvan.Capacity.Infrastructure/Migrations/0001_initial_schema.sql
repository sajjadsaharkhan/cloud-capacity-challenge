-- Capacity schema, PostgreSQL 17. All quantities are integers in the resource class's base unit.

CREATE TABLE pools (
    id          int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    region      text        NOT NULL,
    zone        text,
    host_ref    text,
    admin_state text        NOT NULL CHECK (admin_state IN ('REGISTERED', 'AVAILABLE', 'OFFLINE', 'RETIRED')),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX pools_region ON pools (region);

CREATE TABLE inventories (
    pool_id          int           NOT NULL REFERENCES pools (id),
    resource_class   text          NOT NULL CHECK (resource_class IN ('VCPU', 'RAM_MB', 'DISK_GB', 'IPV4', 'GPU')),
    total            bigint        NOT NULL,
    reserved         bigint        NOT NULL DEFAULT 0,
    allocation_ratio numeric(6, 3) NOT NULL DEFAULT 1,
    held             bigint        NOT NULL DEFAULT 0,
    used             bigint        NOT NULL DEFAULT 0,
    min_unit         bigint        NOT NULL DEFAULT 1,
    step_size        bigint        NOT NULL DEFAULT 1,
    max_unit         bigint        NOT NULL,
    PRIMARY KEY (pool_id, resource_class),
    -- The invariant behind "never oversell": enforced here as well as in the conditional UPDATE.
    CONSTRAINT inventories_never_oversold CHECK (
        held >= 0 AND used >= 0 AND held + used <= floor((total - reserved) * allocation_ratio)),
    CONSTRAINT inventories_shape CHECK (
        allocation_ratio >= 1 AND reserved >= 0 AND reserved <= total
        AND min_unit > 0 AND step_size > 0 AND max_unit >= min_unit)
) WITH (fillfactor = 50, autovacuum_vacuum_scale_factor = 0, autovacuum_vacuum_threshold = 1000);

CREATE TABLE products (
    code              text PRIMARY KEY,
    status            text   NOT NULL CHECK (status IN ('ACTIVE', 'RETIRED')),
    resource_classes  text[] NOT NULL,
    hold_ttl_seconds  int    NOT NULL DEFAULT 900 CHECK (hold_ttl_seconds BETWEEN 120 AND 3600),
    wait_max_seconds  int    NOT NULL DEFAULT 900
);

CREATE TABLE reservations (
    id               uuid        PRIMARY KEY,
    customer_id      text        NOT NULL,
    product_code     text        NOT NULL REFERENCES products (code),
    region           text        NOT NULL,
    zone             text,
    stored_status    text        NOT NULL CHECK (stored_status IN ('WAITING', 'HELD', 'CONFIRMED', 'REJECTED', 'CANCELLED', 'EXPIRED')),
    reason_code      text,
    on_shortage      text        NOT NULL CHECK (on_shortage IN ('REJECT', 'WAIT')),
    expires_at       timestamptz,
    wait_deadline    timestamptz,
    extended         boolean     NOT NULL DEFAULT false,
    allocation_state text        CHECK (allocation_state IN ('ACTIVE', 'RELEASED')),
    client_reference text,
    version          int         NOT NULL,
    created_at       timestamptz NOT NULL,
    updated_at       timestamptz NOT NULL,
    CONSTRAINT reservations_held_has_deadline    CHECK (stored_status <> 'HELD' OR expires_at IS NOT NULL),
    CONSTRAINT reservations_waiting_has_deadline CHECK (stored_status <> 'WAITING' OR wait_deadline IS NOT NULL),
    CONSTRAINT reservations_allocation_iff_confirmed CHECK ((allocation_state IS NOT NULL) = (stored_status = 'CONFIRMED'))
);

CREATE UNIQUE INDEX reservations_client_reference_live
    ON reservations (customer_id, client_reference)
    WHERE client_reference IS NOT NULL AND stored_status IN ('WAITING', 'HELD', 'CONFIRMED');
CREATE INDEX reservations_expiry ON reservations (expires_at) WHERE stored_status = 'HELD';
CREATE INDEX reservations_wait_fifo ON reservations (region, product_code, created_at) WHERE stored_status = 'WAITING';
CREATE INDEX reservations_wait_deadline ON reservations (wait_deadline) WHERE stored_status = 'WAITING';
CREATE INDEX reservations_history ON reservations (customer_id, created_at DESC, id);

CREATE TABLE reservation_lines (
    reservation_id uuid   NOT NULL REFERENCES reservations (id),
    resource_class text   NOT NULL,
    pool_id        int    REFERENCES pools (id),
    quantity       bigint NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (reservation_id, resource_class)
);

CREATE INDEX reservation_lines_pool ON reservation_lines (pool_id, resource_class);

CREATE TABLE reservation_events (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reservation_id uuid        NOT NULL REFERENCES reservations (id),
    version        int         NOT NULL,
    event          text        NOT NULL,
    from_status    text,
    to_status      text        NOT NULL,
    actor_kind     text        NOT NULL,
    actor_id       text        NOT NULL,
    reason_code    text,
    payload        jsonb,
    at             timestamptz NOT NULL,
    published_at   timestamptz
);

CREATE INDEX reservation_events_unpublished ON reservation_events (id) WHERE published_at IS NULL;
CREATE INDEX reservation_events_history ON reservation_events (reservation_id, version);

-- Append-only: every change of held or used is a row, and every counter is reproducible from them.
CREATE TABLE ledger_entries (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    pool_id        int         NOT NULL REFERENCES pools (id),
    resource_class text        NOT NULL,
    reservation_id uuid        REFERENCES reservations (id),
    customer_id    text,
    region         text        NOT NULL,
    entry_type     text        NOT NULL CHECK (entry_type IN ('HOLD', 'COMMIT', 'RETURN', 'RELEASE', 'ADJUST')),
    delta_held     bigint      NOT NULL DEFAULT 0,
    delta_used     bigint      NOT NULL DEFAULT 0,
    actor_id       text,
    reason         text,
    at             timestamptz NOT NULL
);

CREATE INDEX ledger_entries_inventory ON ledger_entries (pool_id, resource_class);

CREATE TABLE idempotency_keys (
    customer_id    text        NOT NULL,
    idem_key       text        NOT NULL,
    operation      text        NOT NULL,
    request_hash   bytea       NOT NULL,
    reservation_id uuid        REFERENCES reservations (id),
    response_body  jsonb,
    expires_at     timestamptz NOT NULL,
    PRIMARY KEY (customer_id, idem_key)
);

CREATE INDEX idempotency_keys_expiry ON idempotency_keys (expires_at);

-- What a reader is told right now. A stored intermediate state past its deadline reads as its
-- final state before anything has stored it; reads never write.
CREATE VIEW reservation_effective AS
SELECT r.*,
       CASE
           WHEN r.stored_status = 'HELD'    AND r.expires_at    <= now() THEN 'EXPIRED'
           WHEN r.stored_status = 'WAITING' AND r.wait_deadline <= now() THEN 'REJECTED'
           ELSE r.stored_status
       END AS effective_status,
       CASE
           WHEN r.stored_status = 'HELD'    AND r.expires_at    <= now() THEN 'HOLD_EXPIRED'
           WHEN r.stored_status = 'WAITING' AND r.wait_deadline <= now() THEN 'WAIT_TIMEOUT'
           ELSE r.reason_code
       END AS effective_reason_code,
       NOT (   (r.stored_status = 'HELD'    AND r.expires_at    <= now())
            OR (r.stored_status = 'WAITING' AND r.wait_deadline <= now())) AS settled
FROM reservations AS r;
