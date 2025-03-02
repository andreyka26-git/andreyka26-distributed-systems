import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '10s', target: 10 }, // Ramp up to 10 users over 10s
        { duration: '30s', target: 10 }, // Stay at 10 users for 30s
        { duration: '10s', target: 0 }   // Ramp down to 0 users
    ],
    rps: 50 // Requests per second limit
};

export default function () {
    let userId = `user${__VU}`; // Assign a unique userId per virtual user
    let res = http.get(`http://localhost:5000/rate-limiter?userId=${userId}`);

    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time is < 500ms': (r) => r.timings.duration < 500,
    });

    sleep(1); // Simulate a user wait time
}
