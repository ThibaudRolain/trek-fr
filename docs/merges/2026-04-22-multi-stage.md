# Merge feature/multi-stage — 2026-04-22

**Merge commit** : [`637aa48`](../../../../commit/637aa48) • **Worktree `trek-fr-stages/` toujours en place** (branche non supprimée)

Découpage automatique d'une trace longue en étapes journalières, chaque étape s'arrêtant dans une commune proche de la trace. Ajoute le toggle UI, la logique domain pure, un provider "towns" basé sur le dataset `communes-fr`, les DTO API et l'enrichissement météo étape-par-étape. Complète `feature/weather` et `feature/quality-pass` déjà mergés le même jour.

## Contexte

Vision produit jour 1 : un trek de plusieurs jours doit raconter où tu dors. Komoot laisse l'utilisateur décider — trek-fr **propose**. Les slices 1–3 avaient livré la génération round-trip / A→B et la météo ; le découpage en étapes était la dernière brique majeure du MVP pour qu'un trek de 80 km / 3 jours arrive avec 2 propositions de nuit alignées sur la trace.

Contraintes cadrées dans la mémoire projet :

- **Tolérances conservatives sur l'effort humain** (`feedback_walk_tolerances`) : pas de fallback qui double la plage user sur km/jour ou D+/jour. Si aucune commune ne tombe dans la fenêtre, on échoue — l'utilisateur retune.
- **Échec feature secondaire = warning, pas 400** (`feedback_dont_block_on_side_feature_failure`) : la trace principale aboutit toujours ; si le découpage rate, on rend la trace + un warning.
- **Honnêteté** (`feedback_honest_about_what_we_know`) : l'app ne cherche pas sur Airbnb / Booking / Abritel. Les liens ouvrent une recherche côté user, jamais une réservation.
- **Docker indisponible** (`infra_docker`) : pas de PostGIS local. Les refuges restent parkés (cf. `feature_train.md` du même registre), les towns sortent du dataset `communes-fr` déjà embarqué.

## Ce qui a été livré

### Domain pur (`backend/TrekFr.Core/`)

- **`Domain/Stage.cs`** : `Stage(Index, Points, Stats, EndSleepSpot, OffTrackDistanceMeters)`, `SleepSpot(Name, Location, Kind)`, enum `SleepSpotKind { Refuge, Town, Arrival }`.
- **`Abstractions/ISleepSpotProvider.cs`** : `FindAlongTrackAsync(trackPoints, bufferMeters, ct)` → `IReadOnlyList<SleepSpotCandidate>`. `SleepSpotCandidate(Spot, NearestTrackIndex, OffTrackDistanceMeters, PatrimonyScore)`.
- **`UseCases/SplitIntoStages.cs`** (238 lignes) : use case pur, sans dépendance réseau.
  - Calcule les sommes cumulatives `cumDist` (Haversine) et `cumGain` (avec seuil anti-bruit ±3 m) en une passe.
  - Court-circuit : si la trace tient en un seul jour, une étape unique est renvoyée avec le nom d'arrivée (`ArrivalName`, défaut `"Arrivée"`).
  - Boucle étape par étape : `FindPivot` repère le premier index où km OU D+ dépassent le plafond journalier ; `PickCandidate` sélectionne la meilleure commune dans la fenêtre `pivot ± 20 %` (`WindowTolerance = 0.20`).
  - Scoring : refuge ⇒ score ≈ 1 000 000 (priorité absolue), town ⇒ `PatrimonyScore − 5 × kmFromPivot`. Trade-off patrimoine vs alignement sur le pivot.
  - Safety net `MaxStagesSafety = 100` (détecte les boucles sur paramètres pathologiques).
  - `NoStageSleepSpotException(StageIndex, ApproxKmFromStart, PivotLocation, Message)` : levée si aucune commune ne tombe dans la fenêtre. Message user-facing avec km et suggestion d'augmenter km/jour ou D+/jour.
- **`UseCases/StageOptions`** : `MaxDistancePerDayMeters`, `MaxElevationGainPerDay`, `WindowTolerance = 0.20`, `MaxOffTrackMeters = 2_000`, `ArrivalName = "Arrivée"`.

### Providers (`backend/TrekFr.Infrastructure/Stages/`)

- **`TrackProximity.cs`** (54 lignes) : helper pur, pour chaque point d'un dataset donne le segment de trace le plus proche (distance crow-fly + index). Utilisé par `CommunesTownProvider` pour projeter les communes sur la trace.
- **`CommunesTownProvider.cs`** (52 lignes) : implémente `ISleepSpotProvider` en scannant `CommuneDataset.Entries`. Pour chaque commune, calcule la distance à la trace via `TrackProximity`; retient celles dont la distance ≤ `bufferMeters` (2 km par défaut). `PatrimonyScore` = champ `score` du dataset (MH, Plus beaux villages, Villes d'art cf. `feature_destination.md`).
- **`NullRefugeProvider.cs`** (22 lignes) : implémente `ISleepSpotProvider` en renvoyant vide. Placeholder jusqu'à disposer d'un dataset refuges (CAF / clubalpin / IGN) — cf. section limitations.
- **`CompositeSleepSpotProvider.cs`** (37 lignes) : concatène les candidats de N providers, déduplique par `(Name, Location)` en gardant le plus proche. Wiring : `Composite(NullRefugeProvider, CommunesTownProvider)` côté Program.cs.

### CommuneDataset — `FindNearestWithDistance`

Nouvelle méthode publique sans cap : parcourt toutes les communes du dataset et renvoie la plus proche avec sa distance en mètres. Sémantiquement différente de `FindNearest(Coord, maxKm)` (qui cap à 50 km par défaut et bénéficie du bbox prefilter). Appelée une seule fois par requête échouée (enrichissement du warning) — pas perf-critical, full scan assumé.

**Conflit résolu au merge** : `main` avait introduit un bbox prefilter dans `FindNearest` (commit `cd1deb1` de `feature/quality-pass`, gain ~100× sur 50 km). La résolution garde les deux : `FindNearest` conserve le prefilter, `FindNearestWithDistance` reste en full-scan.

### API (`backend/TrekFr.Api/`)

- **`Tracks/TrackGenerateRequest.cs`** : 3 nouveaux champs optionnels — `SplitStages` (bool), `StageDistanceKm` (double?), `StageElevationGain` (int?).
- **`Tracks/TrackResponse.cs`** : ajoute `Stages` (liste de `StageDto`) et `Warnings` (liste de `WarningDto`). Chaque `StageDto` porte index, `endSleepSpot` (avec `kind`), stats partielles et `bbox` calculée. `WarningDto` : `Message` + optionnels `NearbyPlace`, `NearbyPlaceDistanceMeters`.
- **`Tracks/TracksEndpoints.cs`** : `GenerateAsync` wire le splitter si `SplitStages = true`, capture `NoStageSleepSpotException` et l'enrichit via `CommuneDataset.FindNearestWithDistance(ex.PivotLocation)` → warning ancré sur la commune réelle la plus proche (hors 2 km), pas juste sur les coords brutes du pivot. La trace reste renvoyée 200 OK.
- **`Program.cs`** : DI `SplitIntoStages`, `NullRefugeProvider`, `CommunesTownProvider`, `CompositeSleepSpotProvider` (+ `ISleepSpotProvider` → `CompositeSleepSpotProvider`).
- **`Properties/launchSettings.json`** : port backend fixé à **5379** (vs 5179 côté main) pour permettre la cohabitation de worktrees parallèles sur la même machine.

### UI (`frontend/src/app/features/tracks/`)

- **`track-generate.component.ts`** : toggle **"Découper en étapes"**, + 2 inputs `km/jour` et `D+/jour` (défauts 22 km, 1000 m). Validation client avant submit (bornes 1–100 / 1–10000). Le toggle ne s'affiche pas en A→B avec distance indicative — il est disponible dans les deux modes.
- **`track-stats-panel.component.ts`** : nouveau bloc "Étapes (N)" avec :
  - Micro-texte honnête : *"Les étapes s'arrêtent dans des communes proches de la trace. L'app ne cherche pas sur Airbnb / Booking / Abritel — les liens ouvrent une recherche sur chaque site pour que tu vérifies l'offre."*
  - Pour chaque étape : cercle numéroté coloré (alternance `#10b981` / `#0ea5e9`), nom de commune + badge `kind` (refuge / arrivée), km + D+ + durée. Clic ⇒ `stageFocus.emit(bbox)` pour zoomer.
  - Trio de liens **Airbnb / Booking / Abritel** par étape (sauf arrivée anonyme), avec `target="_blank" rel="noopener"`, URL pré-remplies avec nom de la commune + `"France"`.
  - Bloc **warning** dédié au-dessus des stats : si `warning.nearbyPlace` est présent, rend "Commune la plus proche : X (Y km)" + trio de liens externes. Même logique de recherche pré-remplie.
- **`app.ts`** : `pointsForWeather(track)` étendu. Si `track.stages` présent, interroge la météo sur `Départ + chaque endSleepSpot` (label `J{n} · {commune}`), tronqué à 10 (limite `/tracks/weather`). Sinon fallback legacy `Départ [+ Arrivée]`.
- **`map.component.ts`** (+162 lignes) : nouvelle couche MapLibre dédiée aux sleep spots (cercles numérotés), synchronisée avec `track.stages`. Le `onStageClick` du panel drive un `fitBounds` sur la bbox de l'étape.

### Dev ergonomics

- **`frontend/proxy.conf.json`** + **`angular.json`** : proxy dev-server Angular vers le backend en URL relative (`/tracks/*` → `http://localhost:5379`). Permet au front sur 4202 de hitter le back sans hardcoder l'URL dans `environment.ts`.

## Décisions prises

- **Un seul scan `foreach` dans `CommunesTownProvider`, pas de quadtree** : dataset ~31k communes, scan = O(n) par trace, largement sous la latence ORS. Pas de raison d'ajouter un index spatial tant que ça reste < 50 ms.
- **Fenêtre `±20 %` autour du pivot (`WindowTolerance = 0.20`)**, sans fallback ×2 (commit `8941e49`, "M1 tweak"). Explicitement retiré après essai : doublait la plage user en silence et donnait des étapes trop longues. Référence durable : `feedback_walk_tolerances`.
- **Refuges prioritaires via score ≈ 1M** dans `PickCandidate` : même si `NullRefugeProvider` ne renvoie rien aujourd'hui, la logique est prête — dès qu'un dataset refuges est branché, ils sortiront devant toutes les towns sans refactor.
- **`NoStageSleepSpotException` = warning enrichi, pas 400** : la trace primaire est livrée, la partie "étapes" dégrade gracefully. Le warning inclut la commune la plus proche **hors des 2 km** + les liens externes pour continuer manuellement. Cf. `feedback_dont_block_on_side_feature_failure`.
- **Liens Airbnb / Booking / Abritel rendus côté front, pas intégrés côté back** : chaque plateforme a son paramètre de recherche trivial (`ss`, `q`, `homes`). Aucune valeur à passer par l'API. Également volontaire pour la clarté du contrat : l'API ne sous-entend pas qu'elle connaît ces plateformes.
- **Ports dédiés 5379 / 4202 committed** (commit `076edf5`), pas juste en env local : permet de cloner la config dans d'autres worktrees futurs sans conflit avec main (5179 / 4200) ou weather (5279 / 4201). Indexé dans `dev_ports_allocation.md`.
- **Pas de refactor du Haversine pour réutiliser `Geo.HaversineMeters`** dans ce merge : le `simplify` sur main a centralisé Haversine (commit `cd1deb1`) **après** le fork stages. `SplitIntoStages.cs` garde une copie privée pour l'instant — cf. section limitations.

## Suivis / limitations connues

- **Haversine dupliqué** dans `SplitIntoStages.cs` (privé) vs `Core/Domain/Geo.HaversineMeters` centralisé (issu de `feature/quality-pass`). À refactorer post-merge pour retirer la copie et constants `EarthRadiusMeters`. Non-bloquant, comportement identique.
- **Tests `SplitIntoStages`** : aucun test dédié ajouté sur ce worktree. `feature/quality-pass` n'avait pas encore ce code à tester lorsqu'elle a étendu la suite à 124. À compléter (domain pur, facile à couvrir).
- **`NullRefugeProvider`** : placeholder, ne renvoie rien. Côté produit, un trek alpin tombera en "communes uniquement". Suivi : brancher un dataset refuges (CAF, IGN `pts_remarquables` layer, OSM `tourism=alpine_hut`). Nécessite probablement PostGIS (bloqué par `infra_docker`).
- **Font `Noto Sans Bold` pour les numéros sur cercles MapLibre** : stack par défaut, non vérifiée dans ce navigateur. Si les chiffres sont invisibles, fallback Markers DOM à faire (discuté, non codé).
- **Warning + liens externes affichent "France" en dur** : à élargir si trek-fr ouvre à d'autres pays. Pas de priorité, l'app est France-only par design actuel.
- **Extension météo tronquée à 10 points** : conforme à la limite `/tracks/weather` mais un trek de 11+ jours perd les dernières étapes dans le panneau météo. Pas bloquant (le backend pourrait élargir en round-robin sur plusieurs batches si le besoin remonte).

## Cleanup associé

- **Worktree `trek-fr-stages/` toujours actif** : conservé tant que le user valide la feature dans le navigateur depuis `main` (ports 5179 / 4200). À retirer avec la branche `feature/multi-stage` après validation.
- **Stash `feature/quick-wins`** : `git stash` créé dans le worktree main pour libérer `.claude/settings.local.json` avant switch `feature/quick-wins` → `main`. À `stash pop` en retournant sur `feature/quick-wins`.
