// Mixed: the load model's peak x 10 for 30 minutes, with confirms and cancels behind the holds.
import http from 'k6/http';
import { check } from 'k6';
import { BASE, headers, submit, customer, widenQuota } from './lib.js';

export const options = {
  scenarios: { mixed: { executor: 'constant-arrival-rate', rate: 60, timeUnit: '1s', duration: '30m', preAllocatedVUs: 100, maxVUs: 400 } },
  thresholds: { http_req_duration: ['p(99)<150'], http_req_failed: ['rate<0.0005'] },
};

export default function () {
  const c = customer();
  widenQuota(c, 'Vcpu', 'ir-thr', 1000000);
  const res = submit(c, 'vm-standard', 'ir-thr', null, [{ resourceClass: 'Vcpu', quantity: 500 }]);
  if (res.status !== 201) return;

  const id = res.json('data.id');
  const verb = Math.random() < 0.7 ? 'confirm' : 'cancel';
  const done = http.post(`${BASE}/v1/reservations/${id}/${verb}`, null, { headers: headers(c) });
  check(done, { [`${verb} ok`]: (r) => r.status === 200 });
}
