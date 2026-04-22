# Changelog

Historique des merges sur `main` — infra, outillage, opérations. Les features produit sont documentées dans leurs PR/commits respectifs ; ce fichier sert à garder une trace lisible des évolutions d'infrastructure et des décisions ops.

Format inspiré de [Keep a Changelog](https://keepachangelog.com). Dates au format ISO 8601.

## 2026-04-22

### Added — CI pipeline (feature/ci)

Pipeline GitHub Actions (build + test backend .NET + frontend Angular) + Dependabot hebdo (nuget/npm/actions) + pin SDK `.NET 10.0.202` + budgets Angular 1.5MB/2MB + badge README.

**Doc détaillée** : [`docs/merges/2026-04-22-feature-ci.md`](docs/merges/2026-04-22-feature-ci.md) • **Merge** : `bdba9f8`

## Entrées antérieures

Pour l'historique avant la mise en place de ce CHANGELOG, se référer à `git log main` — les commits suivent une nomenclature `<scope> — <description>` qui reste lisible chronologiquement.
