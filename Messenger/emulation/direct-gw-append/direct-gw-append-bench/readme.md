# Deploy

## Locally

docker login ghcr.io -u andreyka26-git

docker build -t ghcr.io/andreyka26-git/direct-gw-append-gateway:latest ./Gateway/

docker push ghcr.io/andreyka26-git/direct-gw-append-gateway:latest


## On server

version: '3.8'

services:
  gateway:
    image: ghcr.io/andreyka26-git/direct-gw-append-gateway:latest
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_ENVIRONMENT=Production
