# Merge feature/poi-on-route — 2026-04-23

**Merge commit** : `103e900` • **Branche supprimée** • **Worktree `trek-fr-poi/` à nettoyer**

Slice 1 de la feature POI le long du trace : pour chaque commune traversée ayant des monuments historiques (données Mérimée locales), un marqueur amber apparaît sur la carte et un panneau "Patrimoine" liste les communes par distance depuis le départ. Zéro requête réseau ajoutée.

## Contexte

La vision produit trek-fr depuis le jour 1 inclut "montrer ce qui se trouve sur le chemin" comme différenciateur vs Komoot. La feature avait été parkée après la livraison de ProposeDestination. `communes-fr.json` contient déjà le champ `mh` (count de Monuments Historiques par commune, agrégé depuis la Base Mérimée lors du build du dataset). Slice 1 exploite ce champ sans aucune dépendance réseau ni PostGIS — faisable immédiatement avec le dataset local.

## Ce qui a été livré

### Backend — domaine et infrastructure

**`TrekFr.Core/Domain/MhPoi.cs`** — record `MhPoi(CommuneName, MonumentCount, Location, DistanceFromStartMeters, DistanceFromTrackMeters)`. Représente un cluster patrimonial par commune (pas un monument individuel).

**`TrekFr.Core/Abstractions/IMhPoiProvider.cs`** — interface `FindAlongTrackAsync(trackPoints, ct)` retournant `IReadOnlyList<MhPoi>`.

**`TrekFr.Infrastructure/Communes/MhPoiProvider.cs`** — implémentation singleton :
- Pré-filtre les communes via une bbox + buffer (même pattern que `CommunesTownProvider`)
- Seuil : 2 km de la trace (`TrackProximity.FindNearest`)
- Filtre `MonumentsHistoriques > 0`
- Max 20 résultats triés par `DistanceFromStartMeters`
- Utilise `TrackProximity` (internal dans le même assembly, accessible cross-namespace)

### Backend — API

**`TrackResponse.cs`** — ajout du champ `IReadOnlyList<PoiOnRouteDto>? PoisOnRoute` et du record `PoiOnRouteDto(CommuneName, MonumentCount, Latitude, Longitude, DistanceFromStartMeters, DistanceFromTrackMeters)`. Les overloads `From(GeneratedTrack)` et `From(ProposedGeneratedTrack)` acceptent désormais un paramètre `pois` optionnel.

**`TracksEndpoints.cs`** — injection de `IMhPoiProvider` dans `GenerateAsync`. Appel post-routing avec `try/catch` : un échec du provider ajoute un `WarningDto` sans jamais renvoyer 400 (pattern établi pour les enrichissements secondaires).

**`Program.cs`** — enregistrement `AddSingleton<IMhPoiProvider, MhPoiProvider>()`.

### Frontend

**`track.models.ts`** — interface `PoiOnRoute` + champ `poisOnRoute: PoiOnRoute[] | null` dans `TrackResponse`.

**`map.component.ts`** — source GeoJSON `pois`, layer `pois-circle` (cercles amber `#d97706`, rayon 9, stroke dark) + layer `pois-label` (count MH en blanc). Nettoyage via `removePoisLayers()` quand la trace change.

**`track-stats-panel.component.ts`** — section "Patrimoine" collapsible (signal `poisOpen`, défaut ouvert). Liste les communes avec badge amber + count + distance km depuis le départ.

## Décisions prises

**Cluster par commune, pas monument individuel.** Le dataset local ne contient que le count par commune, pas les records individuels (nom, type de protection, coordonnées précises). Slice 1 affiche "Fontainebleau — 44 MH" plutôt que 44 épingles individuelles. Plus lisible et faisable sans étendre le pipeline de données.

**Buffer 2 km.** Sur une trace pied/vélo, 2 km représentent un détour court acceptable pour rejoindre un monument. Seuil paramétrable via constante dans `MhPoiProvider`.

**Max 20 POI.** Évite le flood visuel sur des traces en zone urbaine dense (ex. Paris intra-muros).

**Coordonnées au centre-commune.** Sans coordonnées précises par monument, le centre de la commune est utilisé comme proxy. Explicitement documenté comme limitation de Slice 1.

**Conflit de merge résolu manuellement.** `feature/arrival-city` avait étendu `CommuneEntry` avec `MonumentsHistoriques` (int?), `IsPlusBeauVillage`, `IsVilleArtHistoire` — champs distincts du `Mh` (int) ajouté par poi-on-route. Resolution : garder les 3 champs de arrival-city, `MhPoiProvider` adapté pour `MonumentsHistoriques ?? 0`.

## Suivis / limitations connues

- **Noms individuels de monuments** — nécessite d'étendre `BuildCommunes` pour émettre un `monuments-fr.json` (ref, nom, protection, insee, lat_commune) depuis le CSV Mérimée. Slice 2 naturelle.
- **Coordonnées approximatives** — le marqueur est au centre de la commune, pas à l'emplacement du monument. Pour les communes étendues (ex. communes rurales avec château isolé), l'imprécision peut dépasser 5 km.
- **Overpass / OSM** — les POI non-patrimoniaux (sommets, points de vue, refuges, sources) nécessitent Overpass. Slice 3+ après validation de la Slice 1.
- **UI à polir** — le user a indiqué travailler sur l'UI à côté (markers cliquables, popup détail, lien Mérimée).

## Cleanup associé

Worktree `C:\Users\bertr\dev\trek-fr-poi` à supprimer une fois les serveurs éteints.
