import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const errorRate = new Rate('errors');
const registerDuration = new Trend('register_duration');
const registerSuccess = new Counter('register_success');
const registerFail = new Counter('register_fail');
const httpErrors4xx = new Counter('http_errors_4xx');
const httpErrors5xx = new Counter('http_errors_5xx');

const SUPABASE_URL = __ENV.SUPABASE_URL || 'https://rdzxrpxizyiwabakczrn.supabase.co';
const SUPABASE_ANON_KEY = __ENV.SUPABASE_ANON_KEY || 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee';

export const options = {
  stages: [
    { duration: '10s', target: 100 },
    { duration: '30s', target: 100 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.1'],
    errors: ['rate<0.1'],
    register_duration: ['p(95)<3000'],
  },
};

function generateRandomEmail() {
  const timestamp = Date.now();
  const random = Math.floor(Math.random() * 10000);
  return `user_${timestamp}_${random}@test.com`;
}

function generateRandomPassword() {
  const chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*';
  let password = '';
  for (let i = 0; i < 12; i++) {
    password += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return password;
}

export default function () {
  const email = generateRandomEmail();
  const password = generateRandomPassword();
  const start = new Date();

  const url = `${SUPABASE_URL}/auth/v1/signup`;
  const payload = JSON.stringify({
    email: email,
    password: password,
  });
  const params = {
    headers: {
      'Content-Type': 'application/json',
      'apikey': SUPABASE_ANON_KEY,
    },
  };

  const res = http.post(url, payload, params);
  registerDuration.add(new Date() - start);

  if (res.status >= 400 && res.status < 500) {
    httpErrors4xx.add(1);
  } else if (res.status >= 500) {
    httpErrors5xx.add(1);
  }

  const success = check(res, {
    'register status 200': (r) => r.status === 200,
    'register has access_token': (r) => {
      try {
        return r.json('access_token') !== undefined;
      } catch (e) {
        return false;
      }
    },
  });

  if (success) {
    registerSuccess.add(1);
    console.log(`Registration successful: ${email}`);
  } else {
    registerFail.add(1);
    errorRate.add(1);
    console.log(`Registration failed: ${email} - Status: ${res.status} - Body: ${res.body ? res.body.substring(0, 200) : 'no body'}`);
  }

  sleep(1);
}