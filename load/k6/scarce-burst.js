// Scarce burst: 300 / s for 2 minutes on a product with 200 units. Records the rejection mix and
// p99; oversell must be 0 (checked afterwards with GET /v1/admin/reconciliation).
import { check } from 'k6';
import { admin, submit, customer, widenQuota } from './lib.js';

export const options = {
  scenarios: { burst: { executor: 'constant-arrival-rate', rate: 300, timeUnit: '1s', duration: '2m', preAllocatedVUs: 300, maxVUs: 800 } },
  thresholds: { http_req_duration: ['p(99)<150'] },
};

export function setup() {
  // 200 units on one region pool: the IPv4 pool of ir-ahv.
  admin('/v1/admin/pools/9/inventories/Ipv4', { total: 200, reserved: 0, allocationRatio: 1, minUnit: 1, stepSize: 1, maxUnit: 16 });
}

export default function () {
  const c = customer();
  widenQuota(c, 'Ipv4', 'ir-ahv', 100);
  const res = submit(c, 'public-ip', 'ir-ahv', null, [{ resourceClass: 'Ipv4', quantity: 1 }]);
  check(res, { 'never a 5xx': (r) => r.status < 500 });
}
