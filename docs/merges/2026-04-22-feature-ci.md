# Merge feature/ci — 2026-04-22

**Merge commit** : [`bdba9f8`](../../../../commit/bdba9f8) • **Branche supprimée** • **Worktree `trek-fr-ci/` retiré**

Pipeline CI/CD minimale mise en place sur GitHub Actions : build + test backend .NET et frontend Angular, avec Dependabot pour le suivi des updates.

## Contexte

Jusqu'ici le projet tournait sans pipeline : chaque contributeur (moi + Claude) faisait `dotnet build` + `ng build` en local avant de push. Ça a marché tant qu'on était seul sur l'ancien setup single-branch, mais avec la multiplication des worktrees parallèles (`feature/weather`, `feature/multi-stage`, `feature/tests`, `feature/quick-wins`, etc.) il devenait risqué de merger sans filet. L'objectif de ce worktree était de poser une base CI **minimale mais correcte** — sans sur-ingéniérer tant qu'on n'a pas besoin de staging / deploy (qui vit dans `feature/deploy`).

## Ce qui a été livré

### `.github/workflows/ci.yml`

Deux jobs indépendants en parallèle :

- **`backend`** (Ubuntu, .NET 10) : restore → build Release → detect test projects → test (skip message explicite si aucun projet `*.Tests.csproj`). Cache NuGet via `actions/cache@v4`, clef basée sur les `*.csproj` + `*.slnx`.
- **`frontend`** (Ubuntu, Node LTS) : `npm ci` → `ng build --configuration production` → `ng test --watch=false`. Cache npm via `actions/setup-node@v4` + cache `.angular/cache` pour accélérer les builds successifs.

Triggers : `push` sur `main` + `pull_request`. `concurrency` configuré pour annuler les runs obsolètes sur la même ref (économie quota Actions).

### `.github/dependabot.yml`

Trois écosystèmes surveillés **hebdo (lundi)** :

- **NuGet** (`/backend`) : limite 5 PR ouvertes, groupe `minor` + `patch` dans une seule PR pour éviter le spam.
- **npm** (`/frontend`) : limite 5 PR, groupe séparé `@angular/*` (toutes les versions Angular dans une PR) + groupe `minor-patch`.
- **GitHub Actions** (`/`) : limite 3 PR, pour garder les `actions/*@v4` à jour.

### `global.json`

```json
{
  "sdk": { "version": "10.0.202", "rollForward": "latestFeature", "allowPrerelease": false }
}
```

Pin de la version SDK .NET pour garantir que CI et local utilisent la même famille de SDK — `latestFeature` laisse remonter vers les patchs mais pas au-delà de la feature release.

### `frontend/angular.json`

Budgets passés de `500KB / 1MB` à `1.5MB / 2MB` sur le bundle initial. MapLibre + Tailwind + Angular poussent naturellement au-delà des defaults Angular ; garder les anciennes limites générait des warnings CI alors que le bundle est sain pour une SPA carto.

### `README.md`

Badge CI ajouté en tête :

```
[![CI](https://github.com/ThibaudRolain/trek-fr/actions/workflows/ci.yml/badge.svg?branch=main)](.../ci.yml)
```

## Décisions prises

- **Pas de coverage reporting** dans ce premier jet — inutile tant qu'on n'a pas de tests réels (seul `feature/tests` en a, pas encore mergée). À ajouter quand tests verts sur `main`.
- **Pas de matrice multi-OS** (Windows/Linux/macOS) : runs uniquement sur `ubuntu-latest`. Rajouter `windows-latest` si un contributeur signale un bug OS-specific.
- **Pas de publish / deploy** : volontairement séparé dans `feature/deploy` (fly.io) pour garder une pipeline de validation indépendante d'une pipeline de release.
- **Dependabot hebdo, pas quotidien** : pour un projet solo, quotidien = spam. Hebdo est suffisant et permet de batcher la review des updates le lundi.
- **Budget Angular 1.5MB/2MB** : mesuré sur le bundle actuel. À revoir si on descend réellement sous 1MB après tree-shaking / code-splitting futur.

## Suivis / limitations connues

- Le workflow détecte les tests .NET via `find *.Tests.csproj` — quand `feature/tests` sera mergée (21 tests verts selon le handoff), les tests s'exécuteront automatiquement sans modif du workflow.
- Pas de lint / format check (`dotnet format`, `eslint`, `prettier`) dans la pipeline. À ajouter si on veut enforcer un style avant merge.
- Pas de security scanning (`CodeQL`, Trivy, etc.). À faire tourner par `feature/security-scan` ou équivalent, hors scope ici.

## Cleanup associé

- Branche `feature/ci` supprimée local + remote.
- Worktree `trek-fr-ci/` retiré de `git worktree list` (dossier vide résiduel sur disque à cause d'un handle Windows, pareil que `trek-fr-weather/` — non bloquant).
