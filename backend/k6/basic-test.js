import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const errorRate = new Rate('errors');
const loginDuration = new Trend('login_duration');
const matchListDuration = new Trend('match_list_duration');
const leaderboardDuration = new Trend('leaderboard_duration');
const requestsTotal = new Counter('requests_total');

const SUPABASE_URL = __ENV.SUPABASE_URL || 'https://rdzxrpxizyiwabakczrn.supabase.co';
const SUPABASE_ANON_KEY = __ENV.SUPABASE_ANON_KEY || 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee';

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';
const TANKX_BASE = `${BASE_URL}/tankx`;

const TEST_EMAIL = __ENV.TEST_EMAIL || 'angelgabrielorteg@gmail.com';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'angelito123';

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '1m', target: 10 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
    errors: ['rate<0.01'],
    login_duration: ['p(95)<2000'],
    match_list_duration: ['p(95)<500'],
    leaderboard_duration: ['p(95)<500'],
  },
};

let authToken = null;
let tokenExpiry = 0;

function login() {
  const start = new Date();
  const url = `${SUPABASE_URL}/auth/v1/token?grant_type=password`;
  const payload = JSON.stringify({
    email: TEST_EMAIL,
    password: TEST_PASSWORD,
  });
  const params = {
    headers: {
      'Content-Type': 'application/json',
      'apikey': SUPABASE_ANON_KEY,
    },
  };

  const res = http.post(url, payload, params);
  loginDuration.add(new Date() - start);
  requestsTotal.add(1);

  const success = check(res, {
    'supabase login status 200': (r) => r.status === 200,
    'supabase login has access_token': (r) => {
      try {
        return r.json('access_token') !== undefined;
      } catch (e) {
        return false;
      }
    },
  });

  if (success && res.status === 200) {
    try {
      const body = res.json();
      authToken = body.access_token;
      tokenExpiry = Date.now() + (body.expires_in || 3600) * 1000;
      console.log(`Login successful. Token expires in ${body.expires_in}s`);
    } catch (e) {
      console.log(`Login parse error: ${e.message}`);
      errorRate.add(1);
      return false;
    }
  } else {
    errorRate.add(1);
    console.log(`Login failed: ${res.status} ${res.body ? res.body.substring(0, 200) : 'no body'}`);
  }

  return success;
}

function authHeaders() {
  return {
    headers: {
      'Authorization': `Bearer ${authToken}`,
      'Content-Type': 'application/json',
    },
  };
}

export function setup() {
  if (!login()) {
    throw new Error('Setup login failed - check credentials and Supabase connection');
  }
  return { token: authToken };
}

export default function (data) {
  if (!authToken || Date.now() > tokenExpiry - 60000) {
    if (!login()) {
      return;
    }
  }

  let start = new Date();
  let res = http.get(`${TANKX_BASE}/Matches?pageSize=20`, authHeaders());
  matchListDuration.add(new Date() - start);
  requestsTotal.add(1);

  check(res, {
    'matches list status 200': (r) => r.status === 200,
    'matches has registers': (r) => {
      try {
        return r.json('registers') !== undefined;
      } catch (e) {
        return false;
      }
    },
  }) || errorRate.add(1);

  sleep(1);

  start = new Date();
  res = http.get(`${TANKX_BASE}/Profiles?pageSize=20`, authHeaders());
  requestsTotal.add(1);

  check(res, {
    'profiles list status 200': (r) => r.status === 200,
  }) || errorRate.add(1);

  sleep(1);

  start = new Date();
  res = http.get(`${TANKX_BASE}/RedisCache/online`, authHeaders());
  requestsTotal.add(1);

  check(res, {
    'online players status 200': (r) => r.status === 200,
    'online has count': (r) => {
      try {
        return r.json('data.count') !== undefined;
      } catch (e) {
        return false;
      }
    },
  }) || errorRate.add(1);

  sleep(1);

  start = new Date();
  res = http.get(`${TANKX_BASE}/RedisCache/leaderboard/top?count=10`, authHeaders());
  leaderboardDuration.add(new Date() - start);
  requestsTotal.add(1);

  check(res, {
    'leaderboard status 200': (r) => r.status === 200,
    'leaderboard has data': (r) => {
      try {
        return r.json('data') !== undefined;
      } catch (e) {
        return false;
      }
    },
  }) || errorRate.add(1);

  sleep(2);
}

export function teardown(data) {
  console.log('Test completed');
}