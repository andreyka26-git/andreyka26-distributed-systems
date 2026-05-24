# Build with the .NET 8 SDK image, then run on the slim aspnet runtime.
# Build context is the MTls/ folder (see docker-compose.yml `context: .`),
# so every COPY path is relative to that folder.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the csprojs first to maximise layer caching for restore.
COPY Common/Common.csproj Common/
COPY RootCa/RootCa.csproj RootCa/
RUN dotnet restore RootCa/RootCa.csproj

# Now bring in the rest of the source and publish.
COPY Common/ Common/
COPY RootCa/ RootCa/
RUN dotnet publish RootCa/RootCa.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# /data is where the root CA PFX will land — mount a named volume here in compose
# so the cert persists across `docker compose down` / `up`.
RUN mkdir -p /data
ENV ROOT_CA_STORAGE_PATH=/data
ENV ROOT_CA_PORT=5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "RootCa.dll"]
