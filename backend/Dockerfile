# syntax=docker/dockerfile:1.7
# -----------------------------
# Stage 1 — build & publish
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copie des csproj + slnx pour tirer parti du cache de restore
COPY TrekFr.slnx ./
COPY TrekFr.Api/TrekFr.Api.csproj            TrekFr.Api/
COPY TrekFr.Core/TrekFr.Core.csproj          TrekFr.Core/
COPY TrekFr.Infrastructure/TrekFr.Infrastructure.csproj TrekFr.Infrastructure/
COPY TrekFr.Data/TrekFr.Data.csproj          TrekFr.Data/
COPY tools/BuildCommunes/BuildCommunes.csproj tools/BuildCommunes/

RUN dotnet restore TrekFr.Api/TrekFr.Api.csproj

# Copie du reste des sources et publish
COPY . .
RUN dotnet publish TrekFr.Api/TrekFr.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -----------------------------
# Stage 2 — runtime
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Écoute HTTP interne sur 8080 ; la plateforme (Fly, reverse proxy, docker-compose)
# mappe l'externe et termine TLS.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "TrekFr.Api.dll"]
