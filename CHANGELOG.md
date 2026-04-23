# Changelog

Historique des merges sur `main` — infra, outillage, opérations. Les features produit sont documentées dans leurs PR/commits respectifs ; ce fichier sert à garder une trace lisible des évolutions d'infrastructure et des décisions ops.

Format inspiré de [Keep a Changelog](https://keepachangelog.com). Dates au format ISO 8601.

## 2026-04-23

### Added — Waytypes/Surface breakdown (feature/waytypes)

Décomposition de la trace par type de voie et surface via ORS `extra_info=["waytype","surface"]`. Backend : `TrackExtras` + `TrackStats.Surface/WayTypes`, `IRoutingProvider` retourne des tuples, `CompositionDto` dans `TrackResponse`. Front : barre empilée Tailwind + labels FR (waytypes 0–8, surface 0–13). Si ORS ne retourne pas les extras → `warnings[]`, jamais 400.

**Doc détaillée** : [`docs/merges/2026-04-23-waytypes.md`](docs/merges/2026-04-23-waytypes.md) • **Merge** : `4d073ff`

### Added — POI Mérimée le long du trace (feature/poi-on-route)

Slice 1 POI on route : communes traversées avec monuments historiques (données Mérimée locales, zéro requête réseau). Marqueurs amber sur la carte + panneau "Patrimoine" collapsible dans la sidebar. Backend : `IMhPoiProvider` / `MhPoiProvider` (buffer 2 km, max 20), `PoiOnRouteDto` dans `TrackResponse`. Échec provider → `warnings[]`, jamais 400.

**Doc détaillée** : [`docs/merges/2026-04-23-poi-on-route.md`](docs/merges/2026-04-23-poi-on-route.md) • **Merge** : `103e900`

### Added — Détails ville d'arrivée (feature/arrival-city)

Exposition des champs patrimoniaux de `communes-fr.json` (MH, PBV, VAH) dans la réponse `/tracks/generate` via un nouveau `destinationInfo` DTO. Nouveau `ArrivalCityPanelComponent` Angular : badges "Plus Beau Village", "Ville d'art et d'histoire", compteur MH + lien Wikipedia — visible en mode A→B proposé par l'app.

**Doc détaillée** : [`docs/merges/2026-04-23-arrival-city.md`](docs/merges/2026-04-23-arrival-city.md) • **Merge** : `4d9de75`

## 2026-04-22

### Added — Première mise en prod (feature/deploy)

Dockerisation backend (.NET 10 SDK→ASP.NET 10) + frontend (node→nginx:alpine SPA fallback), `docker-compose.prod.yml` 4 services (postgis + backend + frontend + caddy), reverse proxy Caddy avec TLS Let's Encrypt automatique via sslip.io. Déploiement live sur VPS Hetzner CAX11 (~€4/mois) — front https://178-104-47-96.sslip.io, API https://api.178-104-47-96.sslip.io. CORS prod via env var `ALLOWED_ORIGINS`, `UseHttpsRedirection` conditionné au dev. Config Fly.io conservée en alternative (bascule Hetzner décidée en cours de route — fin du free tier Fly).

**Doc détaillée** : [`docs/merges/2026-04-22-feature-deploy.md`](docs/merges/2026-04-22-feature-deploy.md) • **Merge** : `b4a112e`

### Added — Quick-wins round-trip (feature/quick-wins)

Round-trip retries 5 → 10, 400 actionnables (`NonRoutablePointException` + `DistanceMismatchException` avec meilleure distance trouvée et suggestion), validation distance ±20 %. Filtre dénivelé D+ min/max (tolérance élastique ±15 %) sur générateur et proposer (top-5 itération). Seed renvoyé dans la réponse (UX "Autre variante"). localStorage des traces sauvegardées (MVP). Skill `/restart` projet-local worktree-aware, `environment.ts` auto-regéré.

**Doc détaillée** : [`docs/merges/2026-04-22-feature-quick-wins.md`](docs/merges/2026-04-22-feature-quick-wins.md) • **Merge** : `269d552`

### Added — Découpage en étapes (feature/multi-stage)

Toggle "Découper en étapes" + inputs km/jour et D+/jour côté front ; domain pur `SplitIntoStages` (pivot + scoring patrimoine, refuges prioritaires) ; providers `CommunesTownProvider` / `NullRefugeProvider` / `CompositeSleepSpotProvider` ; endpoint `/tracks/generate` étendu avec `SplitStages` + `StageDto[]` ; `NoStageSleepSpot` rendu en warning ancré sur la commune la plus proche (hors 2 km) + liens Airbnb / Booking / Abritel pré-remplis ; extension météo automatique sur chaque sleep spot.

**Doc détaillée** : [`docs/merges/2026-04-22-multi-stage.md`](docs/merges/2026-04-22-multi-stage.md) • **Merge** : `637aa48`

### Added — Passe qualité (feature/quality-pass)

Couverture tests backend 21 → 124, frontend 3 → 43. Simplifications : Haversine centralisé (`Core/Domain/Geo.cs`), ORS `SendAndParseAsync` dédupliqué, `UpstreamBadGateway` + `ILogger<T>`, bbox prefilter `CommuneDataset.FindNearest` (~100× sur 50 km), warm-up eager du dataset. Nettoyage : suppression `track-upload` (mort code).

**Doc détaillée** : [`docs/merges/2026-04-22-feature-quality-pass.md`](docs/merges/2026-04-22-feature-quality-pass.md) • **Merge** : `786162c`

### Added — CI pipeline (feature/ci)

Pipeline GitHub Actions (build + test backend .NET + frontend Angular) + Dependabot hebdo (nuget/npm/actions) + pin SDK `.NET 10.0.202` + budgets Angular 1.5MB/2MB + badge README.

**Doc détaillée** : [`docs/merges/2026-04-22-feature-ci.md`](docs/merges/2026-04-22-feature-ci.md) • **Merge** : `bdba9f8`

## Entrées antérieures

Pour l'historique avant la mise en place de ce CHANGELOG, se référer à `git log main` — les commits suivent une nomenclature `<scope> — <description>` qui reste lisible chronologiquement.
