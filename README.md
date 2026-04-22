# trek-fr

[![CI](https://github.com/ThibaudRolain/trek-fr/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ThibaudRolain/trek-fr/actions/workflows/ci.yml)

App web de planification de treks en France : agrège trace GPX, GR, météo, refuges, POI et hébergements. Trois profils : pied, VTT, vélo route.

## Stack

- Backend : .NET 10 Web API (SOLID, architecture en couches)
- Frontend : Angular 21 + MapLibre GL JS + Tailwind
- DB : PostgreSQL 16 + PostGIS 3.4 (via Docker)

## Structure

```
trek-fr/
├── backend/
│   ├── TrekFr.sln
│   ├── TrekFr.Api/             # Web API + DI composition root
│   ├── TrekFr.Core/            # Domain, abstractions, use cases (no external deps)
│   ├── TrekFr.Infrastructure/  # Providers externes (Open-Meteo, ORS, IGN, ...)
│   └── TrekFr.Data/            # EF Core + PostGIS
├── frontend/                   # Angular workspace
└── docker-compose.yml          # postgis service
```

## Sources de données (toutes gratuites)

| Usage | Source |
|---|---|
| Fond de carte | IGN Géoplateforme |
| GR, POI, singles | OpenStreetMap via Overpass API |
| Météo | Open-Meteo |
| Refuges | refuges.info |
| Élévation | IGN BDAlti |
| Routing + round-trip | OpenRouteService (free tier) |
| Geocoding | Nominatim (OSM) |

## Prérequis

- .NET 10 SDK
- Node.js 20+ / Angular CLI 21+
- Docker Desktop (pour PostgreSQL + PostGIS)

## Démarrage rapide

```bash
# DB
docker compose up -d

# Backend
cd backend
dotnet run --project TrekFr.Api

# Frontend (autre terminal)
cd frontend
ng serve
```

## Deploy

### Vue d'ensemble

- **2 images Docker** multi-stage :
  - `backend/Dockerfile` → .NET 10 SDK (build) + ASP.NET 10 (runtime), écoute HTTP sur `:8080`.
  - `frontend/Dockerfile` → Node LTS (`ng build --configuration production`) + nginx:alpine (SPA fallback, listen `:8080`).
- **docker-compose.yml** orchestre les 3 services (postgis + backend + frontend) en mode prod-like.
- **Fly.io** : 2 apps distinctes, `fly.api.toml` et `fly.web.toml` à la racine.

### Variables d'environnement

| Var | Service | Rôle |
|---|---|---|
| `OpenRouteService__ApiKey` | backend | Clé API ORS (bind nested `.NET` via `__`). |
| `ALLOWED_ORIGINS` | backend | Origines CORS autorisées en prod, séparées par `,`. Ex. `https://trek-fr-web.fly.dev`. |
| `ASPNETCORE_ENVIRONMENT` | backend | `Production` en conteneur. |
| `ASPNETCORE_URLS` | backend | `http://+:8080` en conteneur (TLS terminé par la plateforme). |

L'URL publique du backend utilisée par le front est codée en dur dans `frontend/src/environments/environment.prod.ts` — remplacer le placeholder `REPLACE_WITH_BACKEND_URL` avant de builder l'image front.

### Build & run locaux (prod-like)

```bash
# Remplir la clé ORS avant de builder le backend
export ORS_API_KEY=xxxxxxxx

# Optionnel : remplir frontend/src/environments/environment.prod.ts avec
# http://localhost:8080 si on veut que le front prod-buildé tape l'API locale.

docker compose up --build
# Front servi sur http://localhost:8081
# API   servie sur http://localhost:8080
# DB    postgis sur localhost:5432
```

### Deploy Fly.io

Prérequis : un compte Fly, le CLI `flyctl` authentifié.

```bash
# Une seule fois : créer les apps (depuis la racine du repo)
fly apps create trek-fr-api
fly apps create trek-fr-web

# Secrets backend
fly secrets set OpenRouteService__ApiKey=<votre_cle_ORS> -c fly.api.toml
fly secrets set ALLOWED_ORIGINS=https://trek-fr-web.fly.dev -c fly.api.toml

# 1) Deploy backend
fly deploy ./backend -c fly.api.toml

# 2) Noter l'URL publique (ex. https://trek-fr-api.fly.dev),
#    la recopier dans frontend/src/environments/environment.prod.ts,
#    puis deploy frontend
fly deploy ./frontend -c fly.web.toml
```

Post-deploy, vérifier les healthchecks :

```bash
curl https://trek-fr-api.fly.dev/health
curl https://trek-fr-web.fly.dev/healthz
```

> PostGIS n'est pas encore nécessaire en prod (cf. `memory/infra_docker.md`). Quand une slice en aura besoin, ajouter un Fly Postgres (`fly postgres create`) et injecter la connection string en secret.

## Roadmap (slices)

1. ✅ Skeleton monorepo + stack en place
2. Upload GPX, affichage carte, stats (distance, D+/D-)
3. Générateur de trace (formulaire → ORS round_trip)
4. Ranking de candidats sur écart cible
5. Météo sur la trace
6. Refuges dans un buffer
7. GR + POI (Overpass)
8. Altimétrie IGN
9. Deep-links hébergements
10. Multi-jours + étapes

Chaque slice = PR mergeable et démontable seule.
