# Merge feature/deploy — 2026-04-22

**Merge commit** : [`b4a112e`](../../../../commit/b4a112e) • **Worktree `trek-fr-deploy/` à nettoyer** • **Branche `feature/deploy` à supprimer après validation prod**

Première mise en prod du MVP trek-fr. Dockerisation complète backend + frontend, docker-compose prod-like à la racine, reverse proxy Caddy avec TLS Let's Encrypt automatique via sslip.io, déploiement sur un VPS Hetzner (CAX11 ~€4/mois). Les URLs publiques sont :

- **Front** : https://178-104-47-96.sslip.io
- **API**  : https://api.178-104-47-96.sslip.io/health

La config Fly.io (`fly.api.toml` + `fly.web.toml`) est commitée en alternative documentée mais pas utilisée — Fly a perdu son tier gratuit entre le scoping et le deploy, bascule sur Hetzner décidée en cours de route.

## Contexte

Jusque-là l'app n'était utilisable que par l'auteur en `dotnet run` + `ng serve` localement. La roadmap mémoire marquait le point 16 "Deploy" comme dette technique — aucune slice bloquée mais impossible de :

- **Dogfooder depuis mobile** ou partager une URL à un pote randonneur pour valider la vision produit (POI, météo, étapes — cf. `product_differentiation.md`).
- **Soumettre l'app à la pression prod** : CORS réel, HTTPS, secrets, healthchecks, budgets Angular. Beaucoup de bugs d'intégration se réveillent là.

Le choix Hetzner vs Fly a été fait en cours de session : Fly a passé son free tier à payant (~€2-5/mois variable), Hetzner CAX11 à €4/mois fixe + VM persistante = meilleur rapport qualité/prix pour un MVP perso qui finira par vouloir PostGIS (feature train parkée dans `feature_train.md`). Le chemin est donc **VPS bare + docker-compose** plutôt que PaaS, ce qui conserve une flexibilité pour les futures features DB-backed.

L'utilisateur étant novice en ops, le setup a été fait en interactif SSH pas-à-pas : génération clé SSH locale, création du serveur Hetzner, SSH passwordless, install Docker, clone repo, puis Claude prend la main via SSH pour déployer.

## Ce qui a été livré

### Images Docker

- **[`backend/Dockerfile`](../../backend/Dockerfile)** — multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` (build `dotnet publish -c Release`) → `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime). Écoute HTTP sur `:8080` (`ASPNETCORE_URLS=http://+:8080`), pas de HTTPS interne. `DOTNET_RUNNING_IN_CONTAINER=true`. COPY des `.csproj` en premier pour warm-up le cache `dotnet restore`.
- **[`frontend/Dockerfile`](../../frontend/Dockerfile)** — multi-stage `node:lts` (`npm ci` + `npx ng build --configuration production` → `/src/dist/frontend/browser/`) → `nginx:alpine` avec conf perso copiée dans `/etc/nginx/conf.d/default.conf`.
- **[`frontend/nginx.conf`](../../frontend/nginx.conf)** — listen `:8080`, SPA fallback `try_files $uri $uri/ /index.html`, cache long `1y, immutable` sur les assets hashés (JS/CSS/fonts/images), `/healthz` renvoie `200 ok` pour healthcheck conteneur.

### Orchestration

- **[`docker-compose.yml`](../../docker-compose.yml)** — étendu du service `postgis` unique à `postgis + backend + frontend`, ports 8080/8081/5432 mappés sur l'hôte, utile en mode prod-like local (avec `ORS_API_KEY` passé en var shell).
- **[`docker-compose.prod.yml`](../../docker-compose.prod.yml)** — compose **prod only** : 4 services (postgis + backend + frontend + caddy), aucun port mappé sauf Caddy sur `:80`/`:443`, clé ORS lue depuis `.env` via `${ORS_API_KEY:?Set ORS_API_KEY in .env}` (refuse le démarrage si absente), `ALLOWED_ORIGINS=https://178-104-47-96.sslip.io` en dur. C'est ce fichier qui est utilisé sur le VPS.
- **[`Caddyfile`](../../Caddyfile)** — 2 virtual hosts sslip.io + `reverse_proxy` vers les noms de service docker (`frontend:8080` / `backend:8080`). Caddy gère Let's Encrypt via challenge HTTP-01 automatiquement, volume `caddy-data` pour persister les certs.

### Backend — config env vars

- **[`backend/TrekFr.Api/Program.cs`](../../backend/TrekFr.Api/Program.cs)** — ajout d'une policy CORS `"Production"` qui lit la liste d'origines depuis `Cors:AllowedOrigins` (nested config) OU `ALLOWED_ORIGINS` (env var plate, pratique en compose). `UseHttpsRedirection()` conditionné à `app.Environment.IsDevelopment()` : en conteneur derrière un proxy TLS, le forcer créerait une boucle. Clé ORS via `OpenRouteService__ApiKey` — aucun code ajouté, le nested binding .NET natif (`__` → `:`) marche out-of-the-box.

### Frontend — environment prod

- **[`frontend/src/environments/environment.prod.ts`](../../frontend/src/environments/environment.prod.ts)** — nouvelle variante prod, `apiBase: 'https://api.178-104-47-96.sslip.io'` en dur. Activée via `fileReplacements` dans `angular.json` sur la config production.
- **[`frontend/angular.json`](../../frontend/angular.json)** — ajout du `fileReplacements` prod vers `environment.prod.ts`. (Le bump des budgets Angular à 1.5MB/2MB était déjà fait côté `feature/ci` — la valeur a été convergée au merge.)

### Fly.io (alternative)

- **[`fly.api.toml`](../../fly.api.toml)** + **[`fly.web.toml`](../../fly.web.toml)** — 2 apps Fly distinctes, région `cdg`, healthchecks sur `/health` et `/healthz`, `force_https = true`, `auto_stop_machines = "stop"`. Non utilisés en prod (bascule Hetzner), conservés en doc pour une future migration éventuelle.

### Documentation

- **[`README.md`](../../README.md)** — nouvelle section **Deploy** en dessous du Démarrage rapide : vue d'ensemble images, tableau variables d'env, procédure build local, procédure Fly.io. La procédure Hetzner n'est pas documentée dans le README — elle vit dans la mémoire `project_deploy_live.md` (plus opérationnel que README général).

## Décisions prises

- **Hetzner > Fly.io** — bascule en cours de route quand Fly a annoncé la fin de son free tier. Hetzner CAX11 €4/mois ARM persistant > Fly €2-5/mois variable éphémère, surtout pour un MVP qui voudra PostGIS en prod (feature train parkée). La config Fly est **gardée en dépôt** pour ne pas perdre le travail de scoping.
- **sslip.io plutôt qu'acheter un domaine** — pas de `.fr` pour un MVP, on utilise le wildcard DNS gratuit de sslip.io qui résout `X-Y-Z-W.sslip.io` → `X.Y.Z.W`. Switch vers un vrai domaine = 1 ligne dans `Caddyfile` + `environment.prod.ts` quand le moment viendra.
- **2 sous-domaines front + API, pas un seul avec `/api/*`** — option same-origin écartée pour garder l'archi "2 apps séparées" (compatible Fly, docker-compose lisible). Coût : CORS à configurer explicitement — acceptable vu le nombre de origines (1).
- **Env var `ORS_API_KEY` via `.env` local sur VPS, pas dans un secret manager** — MVP, un seul serveur, clé révocable en 1 clic sur openrouteservice.org. Trop tôt pour Vault/SOPS/Fly secrets.
- **`UseHttpsRedirection()` désactivé en prod** — décision explicite (cf. commentaire dans `Program.cs:108`) : le TLS est terminé par Caddy, l'app écoute HTTP sur `:8080`, forcer la redirection créerait une boucle 307.
- **Budget Angular remonté à 2MB** — maplibre-gl gonfle l'initial chunk à 1.42MB. Le fix propre (code-split lazy-load de `MapComponent`) est parké dans `feature_lazy_map.md`. En attendant, le seuil a été remonté pour débloquer `ng build --configuration production`. La valeur s'aligne sur celle déjà poussée par `feature/ci` pour la même raison.
- **Docker Compose v2 installé manuellement depuis GitHub releases sur le VPS** — Ubuntu 22.04 livre `docker.io` + `docker-compose` v1.29.2 (EOL) via son repo officiel. Pas possible d'installer `docker-compose-plugin` via apt sans ajouter le repo Docker. Le binaire direct dans `/usr/local/libexec/docker/cli-plugins/docker-compose` fait le travail sans conflits.
- **Images non pushées vers un registry** — le VPS build les images lui-même via `docker compose up -d --build` après `git pull`. Simple, pas besoin de GHCR / Docker Hub. Coût : ~3-5 min de CPU VPS par deploy. Acceptable à cette échelle.

## Suivis / limitations connues

- **Clé ORS partagée en clair dans la conversation de setup** — à rotate côté openrouteservice.org + mettre à jour `/root/trek-fr/.env` sur le VPS + `docker compose restart backend`.
- **Pas de backup postgis** — le conteneur tourne mais rien n'écrit dedans encore. Quand une feature persistera (save-trace server-side, feature train), mettre en place `pg_dump` cron ou snapshots Hetzner.
- **Pas de CI de deploy** — le redéploiement reste manuel (SSH + `git pull` + `docker compose up -d --build`). Une GitHub Action webhook pourrait automatiser, mais nécessite d'exposer la clé SSH ou un endpoint de deploy sur le VPS.
- **Bundle initial 1.42MB** — parké dans `feature_lazy_map.md`. À faire pour un vrai usage mobile (3G).
- **Pas d'observability agrégée** — juste `docker compose logs`. Pas de Loki/Grafana/Sentry. Pour un MVP dogfooding c'est OK.
- **Le VPS tourne sur branche `feature/deploy` pas `main`** — au moment du merge la branche n'était pas encore supprimée. Après merge à supprimer, switch du VPS vers `main` dans la prochaine opération de redeploy.
- **`docker-compose.yml` racine (non-prod) utilise les mêmes `container_name`** que `docker-compose.prod.yml` — si on mélange les deux en local, collision. Convention : sur le VPS on utilise uniquement `-f docker-compose.prod.yml`, en local pour du prod-like on utilise `docker-compose.yml` tel quel.

## Cleanup associé

- Branche `feature/deploy` **à supprimer** (local `C:/Users/bertr/dev/trek-fr/` + remote).
- Worktree `C:/Users/bertr/dev/trek-fr-deploy/` **à retirer** (`git worktree remove trek-fr-deploy`).
- Le VPS à rebasculer sur `main` lors du prochain redeploy (`git checkout main && git pull && docker compose -f docker-compose.prod.yml up -d --build`).
- **Mémoire** : `project_deploy_live.md` créée + entrée dans `MEMORY.md` pointant dessus ; cf. aussi `feature_lazy_map.md` pour la dette bundle JS.
