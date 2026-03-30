FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Ticketmaster/Ticketmaster.csproj", "Ticketmaster/"]
RUN dotnet restore "Ticketmaster/Ticketmaster.csproj"
COPY . .
WORKDIR "/src/Ticketmaster"
RUN dotnet build "Ticketmaster.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ticketmaster.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "Ticketmaster.dll"]
