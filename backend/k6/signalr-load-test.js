import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

const wsConnectDuration = new Trend('ws_connect_duration');
const wsHandshakeDuration = new Trend('ws_handshake_duration');
const wsLatency = new Trend('ws_latency');
const wsErrors = new Rate('ws_errors');
const wsMovementSent = new Counter('ws_movement_sent');
const wsMovementReceived = new Counter('ws_movement_received');
const activeConnections = new Gauge('active_connections');
const congestionEvents = new Counter('congestion_events');

const SUPABASE_URL = __ENV.SUPABASE_URL || 'https://rdzxrpxizyiwabakczrn.supabase.co';
const SUPABASE_ANON_KEY = __ENV.SUPABASE_ANON_KEY || 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee';
const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';
const WS_URL = BASE_URL.replace('http', 'ws') + '/hubs/game';

export const options = {
  stages: [
    { duration: '10s', target: 20 },
    { duration: '60s', target: 20 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    ws_connect_duration: ['p(95)<2000'],
    ws_handshake_duration: ['p(95)<1000'],
    ws_latency: ['p(95)<100'],
    ws_errors: ['rate<0.05'],
  },
};

function registerAndGetToken() {
  const email = `player_${__VU}_loadtest@test.com`;
  const password = 'LoadTest123!';
  
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
  if (res.status === 200) {
    try {
      return res.json('access_token');
    } catch (e) {
      return null;
    }
  }
  return null;
}

export default function () {
  const token = registerAndGetToken();
  if (!token) {
    console.log(`VU ${__VU}: Failed to register and get auth token`);
    wsErrors.add(1);
    return;
  }

  const url = `${WS_URL}?access_token=${encodeURIComponent(token)}`;
  const connectStart = new Date();

  ws.connect(url, {}, function (socket) {
    activeConnections.add(1);
    wsConnectDuration.add(new Date() - connectStart);
    let handshakeDone = false;
    let lastMovementTime = Date.now();
    let congestionDetected = false;

    socket.on('open', function () {
      const handshakeStart = new Date();
      const handshakeMsg = JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e';
      socket.send(handshakeMsg);
      wsHandshakeDuration.add(new Date() - handshakeStart);
    });

    socket.on('message', function (msg) {
      const messages = msg.split('\u001e').filter(m => m.length > 0);
      for (const m of messages) {
        try {
          const parsed = JSON.parse(m);
          
          if (parsed.negotiateVersion !== undefined && !handshakeDone) {
            handshakeDone = true;
            console.log(`VU ${__VU}: SignalR handshake complete`);
          }
          
          if (parsed.type === 2 && parsed.target === 'GameStateUpdated') {
            const currentLatency = Date.now() - lastMovementTime;
            wsLatency.add(currentLatency);
            wsMovementReceived.add(1);
            
            if (currentLatency > 200) {
              congestionEvents.add(1);
              if (!congestionDetected) {
                console.log(`VU ${__VU}: Congestion detected - latency: ${currentLatency}ms`);
                congestionDetected = true;
              }
            }
          }
        } catch (e) {
        }
      }
    });

    socket.on('error', function (e) {
      console.log(`VU ${__VU}: WebSocket error`);
      wsErrors.add(1);
    });

    socket.on('close', function () {
      activeConnections.add(-1);
    });

    const moveInterval = socket.setInterval(function () {
      if (socket.readyState === 1 && handshakeDone) {
        const directions = ['Up', 'Down', 'Left', 'Right'];
        const dir = directions[Math.floor(Math.random() * directions.length)];
        const moveMsg = JSON.stringify({
          type: 1,
          invocationId: `move-${Date.now()}`,
          target: 'SendMovement',
          arguments: [{ direction: dir }],
        }) + '\u001e';
        socket.send(moveMsg);
        wsMovementSent.add(1);
        lastMovementTime = Date.now();
      }
    }, 100);

    socket.setTimeout(function () {
      socket.clearInterval(moveInterval);
      socket.close();
    }, 80000);
  });

  sleep(85);
}