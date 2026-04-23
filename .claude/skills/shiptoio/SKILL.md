---
name: shiptoio
description: Pousse la branche courante sur GitHub puis redéploie sur le VPS Hetzner (178.104.47.96) via SSH — git pull + docker compose prod up --build. Use this when the user types `/shiptoio`.
---

# /shiptoio — Déploiement vers le VPS Hetzner

Déploie le code courant sur <https://178-104-47-96.sslip.io> en 4 étapes : vérification locale, push GitHub, redéploiement VPS, vérification post-deploy.

## Infra rappel
- VPS : `root@178.104.47.96`, repo à `/root/trek-fr`
- Compose : `docker-compose.prod.yml` (postgis + backend + frontend + caddy)
- Secrets : `/root/trek-fr/.env` (ORS_API_KEY — déjà en place, ne pas toucher)

---

## Étape 1 — Vérification locale

```bash
git status --short
```

Si des fichiers **non commités** sont présents, stoppe et demande à l'utilisateur de commiter ou stasher avant de continuer.

```bash
git log --oneline -3
```

Affiche les 3 derniers commits pour que l'utilisateur sache exactement ce qui partira en prod.

---

## Étape 2 — Push GitHub

```bash
git push
```

Si le push échoue parce que la branche n'a pas encore de remote upstream :

```bash
git push --set-upstream origin $(git branch --show-current)
```

Si le push échoue pour une autre raison (conflit, protection de branche…) : stoppe et explique l'erreur. Ne jamais utiliser `--force`.

---

## Étape 3 — Redéploiement VPS

```bash
ssh -o StrictHostKeyChecking=no -o BatchMode=yes root@178.104.47.96 "
  set -e
  cd /root/trek-fr
  echo '=== git pull ==='
  git pull
  echo '=== docker compose up --build ==='
  docker compose -f docker-compose.prod.yml up -d --build
  echo '=== done ==='
"
```

La commande SSH est atomique (`set -e`) : si `git pull` ou `docker compose` échoue, toute la session SSH s'arrête et l'erreur remonte.

**Si SSH échoue avec "Permission denied (publickey)"** : l'utilisateur doit ajouter sa clé publique sur le VPS (`ssh-copy-id root@178.104.47.96`) ou vérifier que l'agent SSH est actif (`ssh-add`). Affiche la commande à lancer.

---

## Étape 4 — Vérification post-deploy

Attendre ~10 secondes le temps que les conteneurs démarrent, puis :

```bash
api=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 https://api.178-104-47-96.sslip.io/health 2>/dev/null || echo "ERR")
front=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 https://178-104-47-96.sslip.io/ 2>/dev/null || echo "ERR")
echo "API=$api  FRONT=$front"
```

- `API=200` et `FRONT=200` → deploy réussi.
- Toute autre valeur → afficher les logs pour diagnostic :

```bash
ssh -o StrictHostKeyChecking=no -o BatchMode=yes root@178.104.47.96 "
  docker compose -f /root/trek-fr/docker-compose.prod.yml logs --tail=50 backend caddy
"
```

---

## Rapport final

Affiche un résumé :

```
Deploy terminé ✓
  Commit : <hash court> — <message>
  Frontend : https://178-104-47-96.sslip.io
  API      : https://api.178-104-47-96.sslip.io/health
```

## Règles

- Ne jamais `git push --force` ni `git reset` avant le push.
- Ne jamais modifier `/root/trek-fr/.env` sur le VPS — les secrets sont déjà en place.
- Si la branche courante n'est pas `main`, prévenir l'utilisateur que ce n'est pas `main` qui sera déployé (le VPS track la branche pushée, mais le `git pull` récupère ce que `origin/HEAD` pointe — préciser si ambigu).
