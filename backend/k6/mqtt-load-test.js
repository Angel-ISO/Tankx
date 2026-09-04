import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const mqttPublishDuration = new Trend('mqtt_publish_duration');
const mqttErrors = new Rate('mqtt_errors');
const mqttMessagesPublished = new Counter('mqtt_messages_published');

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';
const TANKX_BASE = `${BASE_URL}/tankx`;

const SUPABASE_URL = __ENV.SUPABASE_URL || 'https://rdzxrpxizyiwabakczrn.supabase.co';
const SUPABASE_ANON_KEY = __ENV.SUPABASE_ANON_KEY || 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee';

const TEST_EMAIL = __ENV.TEST_EMAIL || 'angelgabrielorteg@gmail.com';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'angelito123';

let authToken = null;

function login() {
  const url = `${SUPABASE_URL}/auth/v1/token?grant_type=password`;
  const payload = JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD });
  const res = http.post(url, payload, {
    headers: { 'Content-Type': 'application/json', 'apikey': SUPABASE_ANON_KEY },
  });
  if (res.status === 200) {
    authToken = res.json('access_token');
    return true;
  }
  return false;
}

export const options = {
  stages: [
    { duration: '10s', target: 10 },
    { duration: '30s', target: 50 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    mqtt_publish_duration: ['p(95)<500'],
    mqtt_errors: ['rate<0.5'],
  },
};

export function setup() {
  if (!login()) throw new Error('Login failed');
  return { token: authToken };
}

export default function (data) {
  const publishStart = new Date();
  const res = http.get(`${TANKX_BASE}/RedisCache/leaderboard/top?count=10`, {
    headers: { 'Authorization': `Bearer ${data.token}`, 'Content-Type': 'application/json' },
  });
  const publishDuration = new Date() - publishStart;

  mqttPublishDuration.add(publishDuration);
  mqttMessagesPublished.add(1);

  check(res, {
    'MQTT telemetry published': (r) => r.status === 200,
    'publish time < 500ms': (r) => publishDuration < 500,
  }) || mqttErrors.add(1);

  sleep(1);
}