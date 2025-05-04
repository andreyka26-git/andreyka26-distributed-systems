FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/UrlShortener/UrlShortener.csproj", "UrlShortener/"]
RUN dotnet restore "UrlShortener/UrlShortener.csproj"

COPY src/UrlShortener/ UrlShortener/

WORKDIR "/src/UrlShortener"
RUN dotnet build "UrlShortener.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UrlShortener.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "UrlShortener.dll"]
