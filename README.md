# trek-fr

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
