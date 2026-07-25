# --- build stage ---------------------------------------------------------- #
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SeatLocking.csproj ./
RUN dotnet restore SeatLocking.csproj
COPY src/ ./
RUN dotnet publish SeatLocking.csproj -c Release -o /app --no-restore

# --- runtime stage -------------------------------------------------------- #
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "SeatLocking.dll"]
