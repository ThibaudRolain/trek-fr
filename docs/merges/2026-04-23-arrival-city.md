# Merge feature/arrival-city — 2026-04-23

**Merge commit** : [`4d9de75`](../../../../commit/4d9de75) • **Worktree `trek-fr-arrival-city/` à nettoyer** • **Branche `feature/arrival-city` à supprimer**

Quand l'app propose une ville d'arrivée en mode A→B, elle affiche désormais ses labels patrimoniaux (Plus Beaux Villages de France, Villes d'art et d'histoire, nombre de monuments historiques) dans un encart dédié, avec un lien Wikipedia. Les données viennent de `communes-fr.json` — aucune dépendance externe ajoutée.

## Contexte

La feature `ProposeDestination` livrait jusqu'ici uniquement le nom de la ville. Les données MH/PBV/VAH étaient déjà présentes dans `communes-fr.json` (produites par `BuildCommunes` via Wikidata + Mérimée + Média Wiki) mais jamais exposées dans l'API ni affichées dans l'UI. L'objectif était de valoriser ce signal patrimonial déjà calculé pour donner à l'utilisateur une première raison de vouloir aller dans la ville proposée.

## Ce qui a été livré

### Backend — propagation des champs patrimoniaux

**`CommuneEntry`** (`TrekFr.Infrastructure/Communes/CommuneDataset.cs`) — trois nouveaux champs optionnels :

```csharp
[property: JsonPropertyName("mh")] int? MonumentsHistoriques = null,
[property: JsonPropertyName("pbv")] bool IsPlusBeauVillage = false,
[property: JsonPropertyName("vah")] bool IsVilleArtHistoire = false
```

**`ProposedDestination`** (`TrekFr.Core/Abstractions/IDestinationProposer.cs`) — les trois champs propagés dans le record domain. `CommunesDestinationProposer` les renseigne dans `ProposeAsync` et `GetTopCandidatesAsync`.

**`DestinationInfoDto`** (`TrekFr.Api/Tracks/TrackResponse.cs`) — nouveau DTO exposé dans la réponse JSON :

```json
"destinationInfo": {
  "name": "Eguisheim",
  "monumentsHistoriques": 6,
  "isPlusBeauVillage": true,
  "isVilleArtHistoire": false
}
```

`TrackResponse.From()` accepte désormais `ProposedDestination?` (au lieu de `string?`) pour construire le DTO.

**CORS** (`Program.cs`) — `http://localhost:4204` ajouté à la policy dev (port de ce worktree).

### Frontend — `ArrivalCityPanelComponent`

Nouveau composant standalone (`arrival-city-panel.component.ts`, 52 lignes) affiché dans la sidebar entre le stats panel et le weather panel. Visible uniquement quand `destinationInfo` est non-null (= mode A→B proposé par l'app).

Contenu :
- Nom de la ville + lien Wikipedia (`https://fr.wikipedia.org/wiki/<nom>`)
- Badge amber "Plus Beau Village ✦" si `isPlusBeauVillage`
- Badge sky "Ville d'art et d'histoire" si `isVilleArtHistoire`
- Badge slate "N MH" si `monumentsHistoriques > 0`
- Fallback "Pas de label patrimonial recensé" si aucun badge

### Tests

Les deux fakes `IDestinationProposer` dans `TrekFr.Tests` n'implémentaient pas `GetTopCandidatesAsync` (interface étendue lors du worktree multi-stage, tests non mis à jour). Stub ajouté dans `TestWebApplicationFactory.cs` et `UseCaseTests.cs`.

## Décisions prises

**Pas de fetch Wikipedia au chargement.** L'API REST Wikipedia (`/page/summary/<nom>`) renvoie un résumé + image hero ; le fetch serait trivial côté front mais a été laissé pour un prochain sprint (amélioration UI demandée par l'utilisateur après la démo).

**Pas de lien Wikidata QID.** Les QID sont calculés par `BuildCommunes` mais non stockés dans `communes-fr.json` (le champ `wikidata` n'a jamais été inclus dans l'output). Le lien Wikipedia construit depuis le nom suffit pour l'instant.

**`proposedDestinationName` conservé.** Ce champ string existe depuis la feature `ProposeDestination` et est lu par `track-generate.component` (bouton "Autre proposition"), `map.component` (marker rouge), `saved-tracks.service` (nom par défaut). Plutôt que de tout migrer vers `destinationInfo.name`, on a gardé les deux — `destinationInfo` est additionnel.

## Suivis / limitations connues

- **UI à améliorer** (retour utilisateur immédiat) : l'encart est fonctionnel mais basique. Pistes : intégrer les badges inline dans le stats panel, ajouter hero image + résumé Wikipedia, meilleure typographie des badges.
- Le lien Wikipedia est construit depuis le nom de la commune ; fonctionne bien pour les noms simples mais peut échouer sur les noms avec caractères spéciaux ou homonymes.
- Pas de wikidata QID dans `communes-fr.json` — ajouter dans `BuildCommunes` si on veut des liens directs vers les fiches Wikidata.

## Cleanup associé

- Worktree `trek-fr-arrival-city/` à supprimer (`git worktree remove`)
- Branche `feature/arrival-city` à supprimer après validation
- Fichiers `backend.log` / `backend.log.err` dans `.claude/` à ignorer (non commités, gitignorés implicitement)
