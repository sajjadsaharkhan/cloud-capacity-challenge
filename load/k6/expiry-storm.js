// Expiry storm: many holds expiring within a minute. Create them with the shortest TTL (120 s),
// then watch capacity_expiry_lag_seconds and the sweeper's drain rate in Prometheus.
import { admin, submit, customer, widenQuota } from './lib.js';

export const options = {
  scenarios: { fill: { executor: 'constant-arrival-rate', rate: 500, timeUnit: '1s', duration: '100s', preAllocatedVUs: 300, maxVUs: 800 } },
};

export function setup() {
  admin('/v1/admin/products/vm-storm', { status: 'Active', resourceClasses: ['Vcpu'], holdTtlSeconds: 120, waitMaxSeconds: 0, maxActiveHolds: 1000, maxWaiting: 0, maxPoolSharePct: 100 });
}

export default function () {
  const c = customer();
  widenQuota(c, 'Vcpu', 'ir-thr', 1000000);
  submit(c, 'vm-storm', 'ir-thr', 'ir-thr-2', [{ resourceClass: 'Vcpu', quantity: 500 }]);
}
