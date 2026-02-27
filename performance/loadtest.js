import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuration for the load test
export const options = {
    // 1. Define the traffic profile
    stages: [
        { duration: '10s', target: 50 },  // Ramp-up to 50 virtual users (VUs) over 10 seconds
        { duration: '30s', target: 50 },  // Hold at 50 VUs for 30 seconds
        { duration: '10s', target: 0 },   // Ramp-down to 0 VUs over 10 seconds
    ],
    
    // 2. Define CI Gating Thresholds
    thresholds: {
        // 95% of requests must complete within 500ms. 
        // (Thanks to your caching, this should actually be < 50ms in practice!)
        http_req_duration: ['p(95)<500'], 
        
        // Error rate must be less than 1%
        http_req_failed: ['rate<0.01'],   
    },
};

// The actual test scenario executed by each Virtual User
export default function () {
    // Determine the base URL (defaults to localhost:5000 if not provided via env var)
    const baseUrl = __ENV.API_BASE_URL || 'http://localhost:5000';
    
    // Hit the versioned Best Stories endpoint
    const res = http.get(`${baseUrl}/api/v1/best-stories?n=15`);

    // Validate the response
    check(res, {
        'is status 200': (r) => r.status === 200,
        'response is array': (r) => Array.isArray(r.json()),
        'array has elements': (r) => r.json().length > 0,
    });

    // Pause for 1 second between iterations per VU
    sleep(1);
}