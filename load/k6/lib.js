import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export const BASE = __ENV.BASE_URL || 'http://localhost:8080';

export function headers(customer, key) {
  return {
    'X-Customer-Id': customer, 'X-Actor-Kind': 'CUSTOMER', 'X-Actor-Id': customer,
    'Content-Type': 'application/json', 'Idempotency-Key': key || uuidv4(),
  };
}

export function admin(path, body, key) {
  return http.put(`${BASE}${path}`, JSON.stringify(body), {
    headers: { 'X-Actor-Kind': 'ADMIN', 'X-Actor-Id': 'k6', 'Content-Type': 'application/json', 'Idempotency-Key': key || uuidv4() },
  });
}

export function submit(customer, product, region, zone, lines, onShortage) {
  const res = http.post(`${BASE}/v1/reservations`,
    JSON.stringify({ product, region, zone, lines, onShortage }), { headers: headers(customer) });
  check(res, { 'decided (201/202/409/422/429)': (r) => [201, 202, 409, 422, 429].includes(r.status) });
  return res;
}

// Every customer id is unique, so quotas and per-customer limits do not mask the pool under test.
export function customer() { return `k6-${__VU}-${__ITER}-${uuidv4().slice(0, 8)}`; }

export function widenQuota(customerId, resourceClass, region, limit) {
  return admin(`/v1/admin/quotas/${customerId}/${resourceClass}/${region}`, { limit });
}
