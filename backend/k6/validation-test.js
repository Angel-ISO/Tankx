import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 1,
  iterations: 3,
  thresholds: {
    http_req_duration: ['p(95)<15000'],
    http_req_failed: ['rate<0.5'],
  },
};

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';

export default function () {
  const start = new Date();
  const res = http.get(`${BASE_URL}/api/benchmark/indexes`, {
    timeout: '20s',
  });
  const duration = new Date() - start;
  
  check(res, {
    'backend is responding (200 or 400)': (r) => r.status === 200 || r.status === 400,
    'response received': (r) => r.body && r.body.length > 0,
  });
  
  console.log(`Request completed: status=${res.status} duration=${duration}ms`);
  sleep(2);
}