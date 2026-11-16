-- Demo catalog: three regions with two zones each; zone pools for VCPU, RAM and disk; one IPv4
-- pool per region; four GPU host pools in ir-thr-1 with 8 GPUs each; three products; three demo
-- customers with different quotas. Small enough that the demo reaches every shortage code.
-- Runs once, on an empty database.

INSERT INTO products (code, status, resource_classes, hold_ttl_seconds, wait_max_seconds, max_active_holds, max_waiting, max_pool_share_pct) VALUES
    ('vm-standard', 'ACTIVE', ARRAY['VCPU', 'RAM_MB', 'DISK_GB', 'IPV4'], 900, 900, 20, 4, 100),
    ('vm-gpu',      'ACTIVE', ARRAY['VCPU', 'RAM_MB', 'GPU'],             120, 300,  4, 4,  25),
    ('public-ip',   'ACTIVE', ARRAY['IPV4'],                              900, 900, 20, 4, 100);

INSERT INTO pools (region, zone, host_ref, admin_state) VALUES
    ('ir-thr', 'ir-thr-1', NULL, 'AVAILABLE'),
    ('ir-thr', 'ir-thr-2', NULL, 'AVAILABLE'),
    ('ir-tbz', 'ir-tbz-1', NULL, 'AVAILABLE'),
    ('ir-tbz', 'ir-tbz-2', NULL, 'AVAILABLE'),
    ('ir-ahv', 'ir-ahv-1', NULL, 'AVAILABLE'),
    ('ir-ahv', 'ir-ahv-2', NULL, 'AVAILABLE'),
    ('ir-thr', NULL,       NULL, 'AVAILABLE'),
    ('ir-tbz', NULL,       NULL, 'AVAILABLE'),
    ('ir-ahv', NULL,       NULL, 'AVAILABLE'),
    ('ir-thr', 'ir-thr-1', 'gpu-host-1', 'AVAILABLE'),
    ('ir-thr', 'ir-thr-1', 'gpu-host-2', 'AVAILABLE'),
    ('ir-thr', 'ir-thr-1', 'gpu-host-3', 'AVAILABLE'),
    ('ir-thr', 'ir-thr-1', 'gpu-host-4', 'AVAILABLE');

-- Zone pools: CPU, memory and disk, with a 10% fragmentation margin kept out of sale.
INSERT INTO inventories (pool_id, resource_class, total, reserved, allocation_ratio, min_unit, step_size, max_unit)
SELECT p.id, c.resource_class, c.total, c.total / 10, c.ratio, c.min_unit, c.step_size, c.max_unit
FROM pools p
CROSS JOIN (VALUES
    ('VCPU',    4096000::bigint, 2.000::numeric, 500::bigint, 500::bigint, 64000::bigint),
    ('RAM_MB',  8388608::bigint, 1.000::numeric, 512::bigint, 512::bigint, 524288::bigint),
    ('DISK_GB', 102400::bigint,  1.000::numeric, 10::bigint,  10::bigint,  4096::bigint)
) AS c (resource_class, total, ratio, min_unit, step_size, max_unit)
WHERE p.zone IS NOT NULL AND p.host_ref IS NULL;

-- Region pools: public IPv4 addresses.
INSERT INTO inventories (pool_id, resource_class, total, reserved, allocation_ratio, min_unit, step_size, max_unit)
SELECT p.id, 'IPV4', 1024, 100, 1, 1, 1, 16
FROM pools p
WHERE p.zone IS NULL;

-- GPU host pools: 8 GPUs per host, one request served from one host.
INSERT INTO inventories (pool_id, resource_class, total, reserved, allocation_ratio, min_unit, step_size, max_unit)
SELECT p.id, 'GPU', 8, 0, 1, 1, 1, 8
FROM pools p
WHERE p.host_ref IS NOT NULL;

-- Demo customers. GPU has a default quota of 0, so a contract is required.
INSERT INTO quotas (customer_id, resource_class, region, quota_limit)
SELECT c.customer_id, q.resource_class, 'ir-thr', q.quota_limit * c.factor
FROM (VALUES ('demo-small', 1), ('demo-medium', 4), ('demo-large', 16)) AS c (customer_id, factor)
CROSS JOIN (VALUES
    ('VCPU', 16000::bigint), ('RAM_MB', 32768::bigint), ('DISK_GB', 500::bigint), ('IPV4', 2::bigint), ('GPU', 2::bigint)
) AS q (resource_class, quota_limit);
