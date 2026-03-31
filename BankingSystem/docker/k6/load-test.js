// ทดสอบ Banking API ด้วย k6

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const depositDuration = new Trend('deposit_duration');

// Test configuration
export const options = {
    stages: [
        { duration: '30s', target: 10 },   // Ramp up: 0 → 10 users ใน 30 วินาที
        { duration: '1m', target: 50 },   // Ramp up: 10 → 50 users ใน 1 นาที
        { duration: '2m', target: 50 },   // Hold: 50 users คงที่ 2 นาที
        { duration: '30s', target: 100 },  // Spike: 50 → 100 users
        { duration: '1m', target: 100 },  // Hold: 100 users คงที่ 1 นาที
        { duration: '30s', target: 0 },    // Ramp down: 100 → 0
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],  // 95% ของ requests ต้องเสร็จใน 500ms
        http_req_failed: ['rate<0.01'],    // Error rate < 1%
        errors: ['rate<0.05'],             // Custom error rate < 5%
    },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:80';

// Shared test data
let authToken = '';
let testAccountId = '';

export function setup() {
    // Register a test user
    const uniqueId = Date.now();
    const registerRes = http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({
        firstName: 'LoadTest',
        lastName: `User${uniqueId}`,
        email: `loadtest-${uniqueId}@test.com`,
        phone: `08${String(uniqueId).slice(-8)}`,
        password: 'Password1',
        confirmPassword: 'Password1',
    }), { headers: { 'Content-Type': 'application/json' } });

    const data = JSON.parse(registerRes.body);
    return {
        token: data.data.accessToken,
        userId: data.data.userId,
    };
}

export default function (data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
    };

    group('Health Check', () => {
        const res = http.get(`${BASE_URL}/health`);
        check(res, {
            'health status 200': (r) => r.status === 200,
        });
    });

    group('Get Accounts', () => {
        const res = http.get(
            `${BASE_URL}/api/accounts?userId=${data.userId}`,
            { headers }
        );
        check(res, {
            'accounts status 200': (r) => r.status === 200,
        });

        if (res.status === 200) {
            const body = JSON.parse(res.body);
            if (body.data && body.data.length > 0) {
                testAccountId = body.data[0].id;
            }
        }
    });

    group('Get Balance', () => {
        if (!testAccountId) return;

        const res = http.get(
            `${BASE_URL}/api/accounts/${testAccountId}/balance`,
            { headers }
        );
        check(res, {
            'balance status 200': (r) => r.status === 200,
        });
        errorRate.add(res.status !== 200);
    });

    sleep(1); // Wait 1 second between iterations (simulate real user)
}

export function teardown(data) {
    // Cleanup: logout
    http.post(`${BASE_URL}/api/auth/logout`, null, {
        headers: { 'Authorization': `Bearer ${data.token}` },
    });
}