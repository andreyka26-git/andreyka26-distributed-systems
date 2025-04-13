import http from "k6/http";
import { check, sleep } from "k6";

export let options = {
  stages: [
    { duration: "10s", target: 100 }, // Ramp up to 100 VUs in 5 seconds
    { duration: "5s", target: 500 }, // Increase to 500 VUs in the next 5 seconds
    { duration: "10s", target: 1000 }, // Sustain 500 VUs for 10 seconds
    // { duration: "10s", target: 3000 }, // Sustain 500 VUs for 10 seconds
    // { duration: "10s", target: 15000 }, // Sustain 500 VUs for 10 seconds
    { duration: "5s", target: 100 }, // Decrease further to 100 VUs
    // { duration: "5s", target: 0 }, // Ramp down to 0 VUs
  ],
};

export default function () {
  let url = `http://localhost:5000/identifier`;
  let response = http.get(url);

  check(response, {
    "status is 200 or 429": (r) => r.status === 200 || r.status === 429,
  });

  sleep(1);
}
