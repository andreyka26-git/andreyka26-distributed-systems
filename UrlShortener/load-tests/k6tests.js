import http from "k6/http";
import { check, sleep } from "k6";

// Starting in 3 mins

export let options = {
  stages: [
    { duration: "10s", target: 100 }, // Ramp up to 100 VUs in 5 seconds
    { duration: "5s", target: 500 }, // Increase to 500 VUs in the next 5 seconds
    { duration: "10s", target: 500 }, // Sustain 500 VUs for 10 seconds
    { duration: "5s", target: 100 }, // Decrease further to 100 VUs
  ],
};

// WRITE FLOW
// export default function () {
//   const uniqueId = `${__VU}-${Date.now()}`; // VU id + timestamp for uniqueness
//   const payload = JSON.stringify({
//     TargetUrl: `https://example.com/page-${uniqueId}`,
//   });

//   const params = {
//     headers: {
//       "Content-Type": "application/json",
//     },
//   };

//   let res = http.post("http://localhost:5000/shortener/url", payload, params);

//   check(res, {
//     "status is 200 or 201": (r) => r.status === 200 || r.status === 201,
//     "response has id": (r) => r.body.length > 0,
//   });

//   sleep(1);
// }

// READ FLOW
// export default function () {
//   let url = `http://localhost:5000/shortener/url/pkJH9dSr`;
//   let response = http.get(url, { redirects: 0 });

//   check(response, {
//     "status is 302": (r) => r.status === 302,
//   });

//   sleep(1);
// }
