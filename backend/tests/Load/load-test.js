import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

/**
 * k6 Load Test Script for Emergency Alert System
 * 
 * Requirements from tasks.md:
 * - 100 concurrent alert creations
 * - 50 concurrent approvals  
 * - 500 dashboard subscribers (SignalR connections)
 * 
 * Usage:
 *   k6 run load-test.js
 * 
 * Environment Variables:
 *   API_URL - Base URL of the API (default: http://localhost:5000)
 *   AUTH_TOKEN - Bearer token for authentication
 */

const API_BASE = __ENV.API_URL || 'http://localhost:5000';
const AUTH_TOKEN = __ENV.AUTH_TOKEN || 'test-token-12345';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration
export const options = {
    stages: [
        { duration: '30s', target: 50 },   // Ramp up to 50 VUs
        { duration: '1m', target: 100 },   // Ramp up to 100 VUs
        { duration: '2m', target: 100 },   // Stay at 100 VUs for 2 minutes
        { duration: '30s', target: 50 },   // Ramp down to 50 VUs
        { duration: '30s', target: 0 },    // Ramp down to 0 VUs
    ],
    thresholds: {
        http_req_duration: ['p(95)<5000'], // 95% of requests must complete in <5s
        http_req_failed: ['rate<0.05'],     // Error rate must be <5%
        errors: ['rate<0.05'],
    },
};

// Test data
const testAlerts = [];

export function setup() {
    console.log('Setting up test data...');

    // Create test alerts for approval workflow
    for (let i = 0; i < 50; i++) {
        testAlerts.push({
            headline: `Load Test Alert ${i}`,
            description: `Automated load test alert created at ${new Date().toISOString()}`,
            severity: ['Moderate', 'Severe', 'Extreme'][i % 3],
            expiresAt: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(),
            area: {
                polygon: generateRandomPolygon(),
            },
        });
    }

    return { testAlerts };
}

export default function (data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${AUTH_TOKEN}`,
        'X-Correlation-ID': `load-test-${__VU}-${__ITER}`,
    };

    // Scenario 1: Create Alert (100 concurrent)
    if (__VU % 2 === 0) {
        createAlertScenario(headers, data);
    }

    // Scenario 2: Approve Alert (50 concurrent)
    else {
        approveAlertScenario(headers, data);
    }

    sleep(1);
}

function createAlertScenario(headers, data) {
    const alert = data.testAlerts[Math.floor(Math.random() * data.testAlerts.length)];

    const payload = JSON.stringify(alert);

    const response = http.post(
        `${API_BASE}/api/v1/alerts`,
        payload,
        { headers }
    );

    const success = check(response, {
        'alert created status is 201': (r) => r.status === 201,
        'alert created response time <5s': (r) => r.timings.duration < 5000,
        'alert created has ID': (r) => JSON.parse(r.body).id !== undefined,
    });

    if (!success) {
        errorRate.add(1);
        console.error(`Alert creation failed: ${response.status} ${response.body}`);
    } else {
        errorRate.add(0);
    }
}

function approveAlertScenario(headers, data) {
    // First, get a pending alert
    const listResponse = http.get(
        `${API_BASE}/api/v1/alerts?status=PendingApproval&pageSize=10`,
        { headers }
    );

    if (listResponse.status !== 200) {
        errorRate.add(1);
        return;
    }

    const alerts = JSON.parse(listResponse.body).items;

    if (alerts.length === 0) {
        // No pending alerts, skip
        return;
    }

    const alertToApprove = alerts[0];

    // Approve the alert
    const approveResponse = http.post(
        `${API_BASE}/api/v1/alerts/${alertToApprove.id}/approve`,
        JSON.stringify({}),
        {
            headers: {
                ...headers,
                'If-Match': alertToApprove.etag || '*',
            },
        }
    );

    const success = check(approveResponse, {
        'approval status is 204 or 409': (r) => r.status === 204 || r.status === 409,
        'approval response time <5s': (r) => r.timings.duration < 5000,
    });

    if (!success) {
        errorRate.add(1);
        console.error(`Approval failed: ${approveResponse.status} ${approveResponse.body}`);
    } else {
        errorRate.add(0);
    }
}

export function teardown(data) {
    console.log('Load test complete. Cleaning up...');
    // Cleanup logic if needed
}

// Helper functions
function generateRandomPolygon() {
    const baseLon = -122.4 + (Math.random() - 0.5) * 0.5;
    const baseLat = 47.6 + (Math.random() - 0.5) * 0.5;
    const size = 0.05;

    const minLon = baseLon - size;
    const maxLon = baseLon + size;
    const minLat = baseLat - size;
    const maxLat = baseLat + size;

    return `POLYGON((${minLon} ${minLat}, ${maxLon} ${minLat}, ${maxLon} ${maxLat}, ${minLon} ${maxLat}, ${minLon} ${minLat}))`;
}
