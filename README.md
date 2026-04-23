# trek-fr

[![CI](https://github.com/ThibaudRolain/trek-fr/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ThibaudRolain/trek-fr/actions/workflows/ci.yml)

App web de planification de treks en France : génère des traces GPX (aller-retour ou A→B), enrichit chaque sortie avec la météo, les points d'intérêt patrimoniaux et les infos de la ville d'arrivée. Trois profils : pied, VTT, vélo route.

**Prod live** : <https://178-104-47-96.sslip.io>

## Stack

- Backend : .NET 10 Web API (architecture en couches, SOLID)
- Frontend : Angular 21 + MapLibre GL JS + Tailwind CSS
- DB : PostgreSQL 16 + PostGIS 3.4 (Docker — dev local uniquement pour l'instant)

## Structure

```
trek-fr/
├── backend/
│   ├── TrekFr.sln
│   ├── TrekFr.Api/             # Web API + composition root DI
│   ├── TrekFr.Core/            # Domaine, abstractions, use cases (zéro dépendance externe)
│   ├── TrekFr.Infrastructure/  # Providers externes (Open-Meteo, ORS, IGN, Wikidata…)
│   └── TrekFr.Data/            # EF Core + PostGIS
├── frontend/                   # Angular workspace
├── docker-compose.yml          # Dev local (postgis uniquement)
├── docker-compose.prod.yml     # Prod VPS (postgis + backend + frontend + Caddy)
└── Caddyfile                   # Reverse proxy + TLS Let's Encrypt automatique
```

## Sources de données (toutes gratuites)

| Usage | Source |
|---|---|
| Fond de carte | IGN Géoplateforme |
| Routing + round-trip | OpenRouteService (free tier) |
| Météo | Open-Meteo |
| Élévation | IGN BDAlti |
| Geocoding | Nominatim (OSM) |
| POI patrimoniaux | Wikidata SPARQL |
| GR, POI, singles | OpenStreetMap via Overpass API |
| Refuges | refuges.info |

## Prérequis (dev local)

- .NET 10 SDK
- Node.js 20+ / Angular CLI 21+
- Docker Desktop (pour PostgreSQL + PostGIS local)

## Démarrage rapide

```bash
# DB locale
docker compose up -d

# Backend (copier appsettings.Development.json.example → appsettings.Development.json et renseigner ORS_API_KEY)
cd backend
dotnet run --project TrekFr.Api

# Frontend (autre terminal)
cd frontend
ng serve
```

Le proxy Angular (`proxy.conf.json`) route `/tracks/…` vers le backend local — pas besoin d'URL absolue dans `environment.ts`.

## Deploy (VPS Hetzner)

### Architecture prod

4 conteneurs orchestrés par `docker-compose.prod.yml` :

| Conteneur | Rôle |
|---|---|
| `postgis` | PostgreSQL 16 + PostGIS 3.4 |
| `backend` | .NET 10 API, écoute HTTP `:8080` |
| `frontend` | nginx sert le build Angular, écoute `:8080` |
| `caddy` | Reverse proxy + TLS Let's Encrypt auto, ports 80/443 |

Le VPS est un Hetzner (Ubuntu 22.04) à `root@178.104.47.96`. Le repo est cloné dans `/root/trek-fr`.

### Variables d'environnement

| Var | Service | Rôle |
|---|---|---|
| `ORS_API_KEY` | backend | Clé API OpenRouteService. Dans `/root/trek-fr/.env` sur le VPS (chmod 600, jamais commitée). |
| `ALLOWED_ORIGINS` | backend | Origines CORS autorisées. Défini dans `docker-compose.prod.yml`. |
| `ASPNETCORE_ENVIRONMENT` | backend | `Production` en conteneur. |

### Redéployer manuellement

```bash
ssh root@178.104.47.96
cd /root/trek-fr
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

### Redéployer depuis Claude Code

```
/shiptoio
```

Le skill `/shiptoio` pousse la branche courante, se connecte au VPS, fait `git pull` et relance les conteneurs. Voir `.claude/skills/shiptoio/SKILL.md`.

### Vérification post-deploy

```bash
curl https://api.178-104-47-96.sslip.io/health   # → {"status":"ok"}
curl https://178-104-47-96.sslip.io/              # → 200 (frontend nginx)
```

## Roadmap

| # | Feature | État |
|---|---|---|
| 1 | Skeleton monorepo + stack | ✅ |
| 2 | Générateur de trace (round-trip + A→B) | ✅ |
| 3 | Sélection de candidats / ranking | ✅ |
| 4 | Météo sur la trace | ✅ |
| 5 | Destination A→B + infos ville d'arrivée (badges, Wikipedia) | ✅ |
| 6 | POI le long du tracé (Overpass) | 🔜 |
| 7 | Préférence type de voie (route / chemin / piste) | 🔜 |
| 8 | Retour train/bus depuis la destination | 🔜 (bloqué GTFS local) |
| 9 | Refuges + hébergements | 🔜 |
| 10 | Multi-jours + étapes | 🔜 |

Chaque feature livrée = PR mergeable et démontable seule.
