---
name: restart
description: Kill le backend .NET et le frontend Angular de CE worktree trek-fr (ports lus depuis launchSettings.json + .claude/dev-ports.json), rebuild, relance les deux en background. Use this when the user types `/restart` in trek-fr.
---

# Restart trek-fr (worktree-aware)

Rebuild and restart both the .NET API and the Angular dev server for **this worktree**. Supports parallel worktrees (main / feature/weather / feature/multi-stage / etc.) thanks to dynamic port detection — never kills processes of a sibling worktree.

## Port detection

Run this at the top of the execution (bash — **not** cmd.exe):

```bash
# Backend port : source de vérité = backend/TrekFr.Api/Properties/launchSettings.json
API_PORT=$(grep -oE 'http://localhost:[0-9]+' backend/TrekFr.Api/Properties/launchSettings.json | head -1 | grep -oE '[0-9]+$' || echo "5179")

# Frontend port : source de vérité = .claude/dev-ports.json (fallback 4200)
FRONT_PORT=$(grep -oE '"front"[[:space:]]*:[[:space:]]*[0-9]+' .claude/dev-ports.json 2>/dev/null | grep -oE '[0-9]+' | tail -1)
FRONT_PORT=${FRONT_PORT:-4200}

echo "Ports: API=$API_PORT Front=$FRONT_PORT"
```

Print "Ports: API=... Front=..." back to the user so they know which worktree will be touched.

## Steps

1. **Kill the processes bound to this worktree's two dev ports.** Native bash (not cmd.exe — it runs through git-bash on Windows). Target by port PID only, never by image name (would nuke sibling worktrees or user's other dotnet/node).

   ```bash
   netstat -aon | grep ":$API_PORT" | grep LISTENING | awk '{print $5}' | sort -u | while read pid; do taskkill //F //PID $pid 2>&1 || true; done
   netstat -aon | grep ":$FRONT_PORT" | grep LISTENING | awk '{print $5}' | sort -u | while read pid; do taskkill //F //PID $pid 2>&1 || true; done
   ```

   Ignore "process not found" errors if nothing's running.

2. **Rebuild the backend solution** to catch compile errors *before* spawning the dev server (otherwise the background task logs a confusing lock error):

   ```bash
   dotnet build backend/TrekFr.Api/TrekFr.Api.csproj
   ```

   If the build fails with CS errors, **stop and print them**. Do not start the servers.

3. **Start the backend in the background.** Use Bash with `run_in_background: true`:

   ```bash
   dotnet run --project backend/TrekFr.Api/TrekFr.Api.csproj --no-build
   ```

   Capture the task ID. Note : the port used is whatever launchSettings.json says — `API_PORT` was only extracted to tell us where to curl later.

4. **Start the frontend in the background.** Use Bash with `run_in_background: true`:

   ```bash
   cd frontend && npx ng serve --port $FRONT_PORT
   ```

   Capture the task ID.

5. **Verify both servers are up.** Use Monitor with an until-loop, max 30 s:

   ```bash
   for i in $(seq 1 15); do
     api=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 http://localhost:$API_PORT/health 2>/dev/null || echo "000")
     front=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 http://localhost:$FRONT_PORT/ 2>/dev/null || echo "000")
     if [ "$api" = "200" ] && [ "$front" = "200" ]; then echo "UP"; exit 0; fi
     sleep 2
   done
   echo "TIMEOUT api=$api front=$front"; exit 1
   ```

6. **Report** to the user:
   - API  : http://localhost:$API_PORT (swagger at `/openapi/v1.json`)
   - Front: http://localhost:$FRONT_PORT

## Rules

- **Never `taskkill //F //IM dotnet.exe` or `//IM node.exe`.** Would nuke sibling worktrees. Always target by port PID.
- If $API_PORT or $FRONT_PORT is stuck after taskkill → print the PID and its process name (`tasklist //FI "PID eq <N>"`) and ask the user to kill manually.
- If the backend build fails, **don't start either server** — show errors. The front depends on the back for live data.
- ORS API key lives in `backend/TrekFr.Api/appsettings.Development.json` (gitignored) — if missing, the backend will fail config validation ; surface that to the user.
- If a sibling worktree's front points to this worktree's backend (because `track.service.ts` hardcodes `http://localhost:5179`), that's a known cross-worktree config smell — not /restart's job to fix.

## Pour les autres worktrees

Chaque worktree doit :
1. Modifier son `backend/TrekFr.Api/Properties/launchSettings.json` pour que `applicationUrl` utilise SON port backend (ex : `http://localhost:5279`)
2. Committer son propre `.claude/dev-ports.json` avec son port frontend (ex : `{"front": 4201}`)

Le skill ci-dessus fait le reste automatiquement.
