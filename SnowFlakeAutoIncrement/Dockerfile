FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SnowFlakeAutoIncrement/SnowFlakeAutoIncrement.csproj", "SnowFlakeAutoIncrement/"]
RUN dotnet restore "SnowFlakeAutoIncrement/SnowFlakeAutoIncrement.csproj"

COPY src/SnowFlakeAutoIncrement/ SnowFlakeAutoIncrement/

WORKDIR "/src/SnowFlakeAutoIncrement"
RUN dotnet build "SnowFlakeAutoIncrement.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SnowFlakeAutoIncrement.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "SnowFlakeAutoIncrement.dll"]
