# Run

As usual dotnet project. F5 in IDE, or using dotnet cli, or docker

`docker compose up`

To check the redis keys (however they will be expired by defualt in 1 sec)

`docker exec -it <redis-container-name> sh`
`redis-cli`
`KEYS *`

## Load tests

install k6 following the official guide

run `k6 run k6tests.js` from the `load-tests` folder

