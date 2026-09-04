// signalr-test.js - k6 WebSocket load test for TankX SignalR GameHub
// Run: k6 run signalr-test.js
//
// Simulates real-time multiplayer traffic:
// - Connects to /hubs/game with JWT auth
// - Performs SignalR handshake
// - Sends movement/shoot messages periodically
// - Measures latency and message throughput

import ws from 'k6/ws';
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const wsConnectDuration = new Trend('ws_connect_duration');
const wsHandshakeDuration = new Trend('ws_handshake_duration');
const wsMessageLatency = new Trend('ws_message_latency');
const wsErrors = new Rate('ws_errors');
const wsMessagesSent = new Counter('ws_messages_sent');
const wsMessagesReceived = new Counter('ws_messages_received');

// Supabase config
const SUPABASE_URL = __ENV.SUPABASE_URL || 'https://rdzxrpxizyiwabakczrn.supabase.co';
const SUPABASE_ANON_KEY = __ENV.SUPABASE_ANON_KEY || 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee';

// Backend config
const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';
const WS_URL = BASE_URL.replace('http', 'ws') + '/hubs/game';

// Test credentials
const TEST_EMAIL = __ENV.TEST_EMAIL || 'angelgabrielorteg@gmail.com';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'angelito123';

// Test configuration - 20 VUs (simulating 20 players)
export const options = {
  stages: [
    { duration: '20s', target: 20 },   // Ramp up to 20 players
    { duration: '1m', target: 20 },    // Stay at 20 players
    { duration: '20s', target: 0 },    // Ramp down
  ],
  thresholds: {
    ws_connect_duration: ['p(95)<3000'],
    ws_handshake_duration: ['p(95)<1000'],
    ws_message_latency: ['p(95)<200'],
    ws_errors: ['rate<0.10'],
  },
};

// Global token store
let authToken = null;
let tokenExpiry = 0;

function login() {
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

  const success = check(res, {
    'supabase login status 200': (r) => r.status === 200,
  });

  if (success && res.status === 200) {
    try {
      const body = res.json();
      authToken = body.access_token;
      tokenExpiry = Date.now() + (body.expires_in || 3600) * 1000;
      console.log(`Login successful, token expires in ${body.expires_in}s`);
      return true;
    } catch (e) {
      console.log(`Login parse error: ${e.message}`);
      return false;
    }
  } else {
    console.log(`Login failed: ${res.status}`);
    return false;
  }
}

export function setup() {
  if (!login()) {
    throw new Error('Setup login failed');
  }
  return { token: authToken };
}

export default function (data) {
  // Refresh token if needed
  if (!authToken || Date.now() > tokenExpiry - 60000) {
    if (!login()) return;
  }

  const url = `${WS_URL}?access_token=${encodeURIComponent(authToken)}`;
  const connectStart = new Date();

  ws.connect(url, {}, function (socket) {
    wsConnectDuration.add(new Date() - connectStart);

    socket.on('open', function () {
      console.log('WebSocket connected');

      // SignalR handshake
      const handshakeStart = new Date();
      const handshakeMsg = JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e';
      socket.send(handshakeMsg);
      wsHandshakeDuration.add(new Date() - handshakeStart);
      wsMessagesSent.add(1);
    });

    socket.on('message', function (msg) {
      wsMessagesReceived.add(1);
      const latencyStart = new Date();

      const messages = msg.split('\u001e').filter(m => m.length > 0);
      for (const m of messages) {
        try {
          const parsed = JSON.parse(m);

          // SignalR handshake response
          if (parsed.negotiateVersion !== undefined) {
            console.log('SignalR handshake complete');
            
            // Send ListRooms after handshake
            const listRoomsMsg = JSON.stringify({
              type: 1,
              invocationId: 'list-rooms',
              target: 'ListRooms',
              arguments: [],
            }) + '\u001e';
            socket.send(listRoomsMsg);
            wsMessagesSent.add(1);
          }

          // ListRooms response
          if (parsed.type === 3 && parsed.invocationId === 'list-rooms') {
            wsMessageLatency.add(new Date() - latencyStart);
            console.log('ListRooms response received');
          }

          // Other server messages (type 2 = completion)
          if (parsed.type === 2) {
            wsMessageLatency.add(new Date() - latencyStart);
          }
        } catch (e) {
          // Ignore parse errors for non-JSON messages
        }
      }
    });

    socket.on('close', function () {
      console.log('WebSocket closed');
    });

    socket.on('error', function (e) {
      console.log(`WebSocket error: ${e.error()}`);
      wsErrors.add(1);
    });

    // Simulate movement every 2 seconds
    const moveInterval = socket.setInterval(function () {
      if (socket.readyState === 1) {
        const moveMsg = JSON.stringify({
          type: 1,
          invocationId: `move-${Date.now()}`,
          target: 'SendMovement',
          arguments: [{ direction: 'Up' }],
        }) + '\u001e';
        socket.send(moveMsg);
        wsMessagesSent.add(1);
      }
    }, 2000);

    // Simulate shoot every 5 seconds
    const shootInterval = socket.setInterval(function () {
      if (socket.readyState === 1) {
        const shootMsg = JSON.stringify({
          type: 1,
          invocationId: `shoot-${Date.now()}`,
          target: 'Shoot',
          arguments: [],
        }) + '\u001e';
        socket.send(shootMsg);
        wsMessagesSent.add(1);
      }
    }, 5000);

    // Keep connection alive for 90 seconds
    socket.setTimeout(function () {
      socket.clearInterval(moveInterval);
      socket.clearInterval(shootInterval);
      socket.close();
    }, 90000);
  });

  sleep(95);
}