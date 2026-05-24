FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/
COPY Client2/Client2.csproj Client2/
RUN dotnet restore Client2/Client2.csproj

COPY Common/ Common/
COPY Client2/ Client2/
RUN dotnet publish Client2/Client2.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

ENV CA_URL=http://localhost:5000
ENV SERVICE_CN=client2
ENV ALLOWED_CALLER_CN=client1
ENV LISTEN_PORT=5002
EXPOSE 5002

ENTRYPOINT ["dotnet", "Client2.dll"]
