// Hot row: ramp submits on ONE pool from 50 to 600 / s and record where p99 latency breaks 150 ms.
// The 'hot-row' pool is the demo zone ir-thr-1; every submit takes 500 millicores of its vCPU.
import { sleep } from 'k6';
import { submit, customer } from './lib.js';

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-arrival-rate', startRate: 50, timeUnit: '1s',
      preAllocatedVUs: 200, maxVUs: 1000,
      stages: [{ target: 100, duration: '30s' }, { target: 300, duration: '30s' },
               { target: 450, duration: '30s' }, { target: 600, duration: '30s' }],
    },
  },
  thresholds: { http_req_duration: ['p(99)<150'], checks: ['rate>0.99'] },
};

export default function () {
  submit(customer(), 'vm-standard', 'ir-thr', 'ir-thr-1', [{ resourceClass: 'Vcpu', quantity: 500 }]);
}
