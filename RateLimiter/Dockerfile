FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/RateLimiter.Api/RateLimiter.Api.csproj", "RateLimiter.Api/"]
RUN dotnet restore "RateLimiter.Api/RateLimiter.Api.csproj"

COPY src/RateLimiter.Api/ RateLimiter.Api/

WORKDIR "/src/RateLimiter.Api"
RUN dotnet build "RateLimiter.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RateLimiter.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "RateLimiter.Api.dll"]
