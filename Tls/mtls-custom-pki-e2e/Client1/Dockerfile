FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/
COPY Client1/Client1.csproj Client1/
RUN dotnet restore Client1/Client1.csproj

COPY Common/ Common/
COPY Client1/ Client1/
RUN dotnet publish Client1/Client1.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# Defaults — docker-compose overrides CA_URL / PEER_URL with service-name hosts.
ENV CA_URL=http://localhost:5000
ENV PEER_URL=https://localhost:5002
ENV SERVICE_CN=client1
ENV LISTEN_PORT=5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "Client1.dll"]
