---
name: restart
description: Kill le backend .NET et le frontend Angular de trek-fr, rebuild, relance les deux en background. Use this when the user types `/restart` in trek-fr.
---

# Restart trek-fr

Rebuild and restart both the .NET API (port 5179) and the Angular dev server (port 4200) after code changes.

## Steps

1. **Kill the processes bound to our two dev ports.** Never `taskkill //IM dotnet.exe` or `//IM node.exe` — that would nuke unrelated user processes. Always target by port PID.

   ```
   for /f "tokens=5" %a in ('netstat -aon ^| findstr :5179 ^| findstr LISTENING') do taskkill //F //PID %a
   for /f "tokens=5" %a in ('netstat -aon ^| findstr :4200 ^| findstr LISTENING') do taskkill //F //PID %a
   ```

   Ignore "process not found" errors if nothing's running.

2. **Rebuild the backend solution** to catch compile errors *before* spawning the dev server (otherwise the background task logs a confusing lock error). Run:

   ```
   dotnet build C:/Users/bertr/dev/trek-fr/backend/TrekFr.Api/TrekFr.Api.csproj
   ```

   If the build fails with CS errors, **stop and print them**. Do not start the servers.

3. **Start the backend in the background.** Use Bash with `run_in_background: true`:

   ```
   dotnet run --project C:/Users/bertr/dev/trek-fr/backend/TrekFr.Api/TrekFr.Api.csproj --no-build
   ```

   Capture the task ID.

4. **Start the frontend in the background.** Use Bash with `run_in_background: true`:

   ```
   cd C:/Users/bertr/dev/trek-fr/frontend && npx ng serve --port 4200
   ```

   Capture the task ID.

5. **Verify both servers are up.** Poll with Monitor until both respond, max 30s. Expected:
   - Backend health: `curl -s -o /dev/null -w "%{http_code}" http://localhost:5179/health` → `200`
   - Frontend: `curl -s -o /dev/null -w "%{http_code}" http://localhost:4200/` → `200`

6. **Report** the two URLs and the background task IDs to the user:
   - API  : http://localhost:5179 (swagger at `/openapi/v1.json`)
   - Front: http://localhost:4200

## Rules

- Port 5179 or 4200 stuck after taskkill → print the PID and the process name (`tasklist //FI "PID eq <N>"`) and ask the user to kill manually. Don't guess-kill by image name.
- If the backend build fails, **don't start either server** — show errors. The front depends on the back for live data.
- If the user has uncommitted changes, don't commit or stash — just restart. /ship exists for commits.
- ORS API key lives in `backend/TrekFr.Api/appsettings.Development.json` (gitignored) — if it's missing the backend will fail to start with a config-validation error; surface that to the user.
