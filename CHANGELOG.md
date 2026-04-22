# Changelog

Historique des merges sur `main` — infra, outillage, opérations. Les features produit sont documentées dans leurs PR/commits respectifs ; ce fichier sert à garder une trace lisible des évolutions d'infrastructure et des décisions ops.

Format inspiré de [Keep a Changelog](https://keepachangelog.com). Dates au format ISO 8601.

## 2026-04-22

### Added — CI pipeline (PR feature/ci)

Pipeline GitHub Actions + Dependabot intégrée à `main` (merge `bdba9f8`).

- **Workflow CI** (`.github/workflows/ci.yml`) — build + test backend .NET + frontend Angular sur chaque push / PR vers `main`. Caches npm et NuGet configurés pour accélérer les runs (>1 min gagné par run après warm-up).
- **Dependabot** (`.github/dependabot.yml`) — surveillance hebdo des updates pour NuGet, npm et GitHub Actions. Les PR générées sont ciblées sur `main`.
- **Pin SDK .NET** (`global.json`) — version `10.0.202` avec `rollForward: latestFeature` pour garantir la reproductibilité locale / CI.
- **Budgets Angular** — initial bundle passé à 1.5 MB / max 2 MB dans `frontend/angular.json` pour refléter la réalité actuelle du bundle (MapLibre + Tailwind) sans générer de warning CI.
- **Badge CI** ajouté en tête du `README.md`.

**Worktree `trek-fr-ci/` supprimé**, branche `feature/ci` supprimée locale + remote.

## Entrées antérieures

Pour l'historique avant la mise en place de ce CHANGELOG, se référer à `git log main` — les commits suivent une nomenclature `<scope> — <description>` qui reste lisible chronologiquement.
