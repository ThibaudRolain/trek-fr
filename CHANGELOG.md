# Changelog

Historique des merges sur `main` — infra, outillage, opérations. Les features produit sont documentées dans leurs PR/commits respectifs ; ce fichier sert à garder une trace lisible des évolutions d'infrastructure et des décisions ops.

Format inspiré de [Keep a Changelog](https://keepachangelog.com). Dates au format ISO 8601.

## 2026-04-22

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
