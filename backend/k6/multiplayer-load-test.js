import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

const httpErrors = new Rate('http_errors');
const wsErrors = new Rate('ws_errors');
const loginDuration = new Trend('login_duration');
const roomCreateDuration = new Trend('room_create_duration');
const matchStartDuration = new Trend('match_start_duration');
const wsLatency = new Trend('ws_latency');
const activeConnections = new Gauge('active_ws_connections');
const totalRequests = new Counter('total_requests');

export const options = {
  scenarios: {
    http_api: {
      executor: 'ramping-vus',
      stages: [
        { duration: '1m', target: 20 },
        { duration: '3m', target: 20 },
        { duration: '1m', target: 0 },
      ],
      exec: 'httpApiFlow',
    },
    websocket_game: {
      executor: 'ramping-vus',
      stages: [
        { duration: '2m', target: 10 },
        { duration: '4m', target: 10 },
        { duration: '1m', target: 0 },
      ],
      exec: 'websocketGameFlow',
      startTime: '30s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
    http_errors: ['rate<0.01'],
    ws_errors: ['rate<0.05'],
    ws_latency: ['p(95)<100'],
    login_duration: ['p(95)<1000'],
    room_create_duration: ['p(95)<500'],
    match_start_duration: ['p(95)<1000'],
  },
};

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5074';
const TANKX_BASE = `${BASE_URL}/tankx`;
const WS_URL = BASE_URL.replace('http', 'ws') + '/hubs/game';

const TEST_EMAIL = 'angelgabrielorteg@gmail.com';
const TEST_PASSWORD = 'angelito123';

let authTokens = new Map();

function login(vuId) {
  const start = new Date();
  const url = `${BASE_URL}/api/auth/login`;
  const payload = JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD });
  const params = { headers: { 'Content-Type': 'application/json' } };

  const res = http.post(url, payload, params);
  loginDuration.add(new Date() - start);
  totalRequests.add(1);

  const success = check(res, {
    'login 200': (r) => r.status === 200,
    'has token': (r) => r.json('access_token') !== undefined,
  });

  if (success) {
    const token = res.json('access_token');
    authTokens.set(vuId, token);
  } else {
    httpErrors.add(1);
    console.log(`VU ${vuId} login failed: ${res.status}`);
  }
  return success;
}

function authHeaders(vuId) {
  const token = authTokens.get(vuId);
  return {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
  };
}

export function httpApiFlow() {
  const vuId = __VU;
  
  if (!authTokens.has(vuId)) {
    if (!login(vuId)) return;
  }

  while (true) {
    group('API - List Matches', function () {
      const start = new Date();
      const res = http.get(`${TANKX_BASE}/Matches?pageSize=20`, authHeaders(vuId));
      totalRequests.add(1);
      check(res, { 'status 200': (r) => r.status === 200 }) || httpErrors.add(1);
    });

    sleep(2);

    group('API - Leaderboard', function () {
      const res = http.get(`${TANKX_BASE}/RedisCache/leaderboard/top?count=10`, authHeaders(vuId));
      totalRequests.add(1);
      check(res, { 'status 200': (r) => r.status === 200 }) || httpErrors.add(1);
    });

    sleep(2);

    group('API - Online Players', function () {
      const res = http.get(`${TANKX_BASE}/RedisCache/online`, authHeaders(vuId));
      totalRequests.add(1);
      check(res, { 'status 200': (r) => r.status === 200 }) || httpErrors.add(1);
    });

    sleep(3);

    group('API - Benchmark', function () {
      const res = http.get(`${TANKX_BASE}/RedisCache/benchmark`, authHeaders(vuId));
      totalRequests.add(1);
      check(res, { 'status 200': (r) => r.status === 200 }) || httpErrors.add(1);
    });

    sleep(10);
  }
}

export function websocketGameFlow() {
  const vuId = __VU;
  
  if (!authTokens.has(vuId)) {
    if (!login(vuId)) return;
  }
  
  const token = authTokens.get(vuId);
  const url = `${WS_URL}?access_token=${encodeURIComponent(token)}`;

  ws.connect(url, {}, function (socket) {
    activeConnections.add(1);
    let handshakeDone = false;
    let roomJoined = false;
    let matchStarted = false;

    socket.on('open', function () {
      const handshakeMsg = JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e';
      socket.send(handshakeMsg);
    });

    socket.on('message', function (msg) {
      const messages = msg.split('\u001e').filter(m => m.length > 0);
      for (const m of messages) {
        try {
          const parsed = JSON.parse(m);

          if (parsed.negotiateVersion !== undefined && !handshakeDone) {
            handshakeDone = true;
            const createRoomMsg = JSON.stringify({
              type: 1,
              invocationId: 'create-room-1',
              target: 'CreateRoom',
              arguments: [{ maxPlayers: 4, mapName: 'battle-city', password: null }],
            }) + '\u001e';
            socket.send(createRoomMsg);
          }

          if (parsed.type === 3 && parsed.invocationId === 'create-room-1') {
            roomJoined = true;
            const roomCode = parsed.result?.room?.roomCode;
            if (roomCode) {
              setTimeout(function () {
                const startMatchMsg = JSON.stringify({
                  type: 1,
                  invocationId: 'start-match-1',
                  target: 'StartMatch',
                  arguments: [],
                }) + '\u001e';
                socket.send(startMatchMsg);
              }, 2000);
            }
          }

          if (parsed.type === 3 && parsed.invocationId === 'start-match-1') {
            matchStarted = true;
          }

          if (parsed.type === 2 && parsed.target === 'GameStateUpdated') {
            wsLatency.add(1);
          }

        } catch (e) {
        }
      }
    });

    socket.on('error', function (e) {
      wsErrors.add(1);
    });

    socket.on('close', function () {
      activeConnections.add(-1);
    });

    const moveInterval = socket.setInterval(function () {
      if (socket.readyState === 1 && matchStarted) {
        const directions = ['Up', 'Down', 'Left', 'Right'];
        const dir = directions[Math.floor(Math.random() * directions.length)];
        const moveMsg = JSON.stringify({
          type: 1,
          invocationId: `move-${Date.now()}`,
          target: 'SendMovement',
          arguments: [{ direction: dir }],
        }) + '\u001e';
        socket.send(moveMsg);
      }
    }, 100);

    const shootInterval = socket.setInterval(function () {
      if (socket.readyState === 1 && matchStarted && Math.random() < 0.3) {
        const shootMsg = JSON.stringify({
          type: 1,
          invocationId: `shoot-${Date.now()}`,
          target: 'Shoot',
          arguments: [],
        }) + '\u001e';
        socket.send(shootMsg);
      }
    }, 2000);

    socket.setTimeout(function () {
      socket.clearInterval(moveInterval);
      socket.clearInterval(shootInterval);
      socket.close();
    }, 120000);
  });

  sleep(130);
}