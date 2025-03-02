import http from "k6/http";
import { check } from "k6";
import { sleep } from "k6";

export let options = {
  vus: 200,
  duration: "10s",
};

export default function () {
  let url = `http://localhost:5000/rate-limiter?userId=user1`;

  let response = http.get(url);

  check(response, {
    "status is 200": (r) => r.status === 200,
  });

  sleep(1);
}
