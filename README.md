# Submarine

Submarine is a PVR for Usenet and BitTorrent users. It manages TV series, anime and movies in one application: it watches indexers for new releases, sends them to your download client, and imports, renames and upgrades files automatically. It replaces Sonarr, Radarr and Prowlarr with a single service, and ships its own metadata and scene numbering services so nothing depends on third party proxies.

## What it does

Library
- Series (standard, daily, anime) and movies in one library, with TVDB and TMDB metadata.
- Several versions per item (for example 1080p and 4K), each with its own quality profile, language profile and folder.
- Aired, DVD and absolute episode numbering, season pass, mass editor, root folders, tags, collections.
- Calendar with an iCal feed, wanted lists (missing and cutoff unmet), import lists (TMDB, Trakt, AniList, Plex, Sonarr, Radarr, custom JSON).

Releases
- Indexers: Torznab, Newznab and Cardigann YAML definitions (bundled and synced from the Prowlarr definitions index), indexer proxies (HTTP, SOCKS, FlareSolverr), failure backoff, statistics and history.
- Submarine also exposes a Newznab and Torznab API, so other applications can use it as their indexer hub.
- Release title parsing that tells full BluRay discs, remuxes and BDRips apart, detects streaming services, languages, editions, release groups and flags.
- Quality and language profiles with cutoffs and upgrades, quality size limits, delay profiles, release profiles, release filters, custom formats (TRaSH JSON import and export).
- Interactive and automatic search, RSS sync, pending releases.

Downloads
- Download clients: qBittorrent, Transmission, Deluge, rTorrent, uTorrent, Aria2, Flood, Download Station, SABnzbd, NZBGet and blackhole folders.
- Queue with live progress, completed download handling, failed download handling with blocklist and redownload.
- Import pipeline: hardlink or move, media info via ffprobe, extras and subtitles, Kodi NFO files, permissions, recycle bin, manual import, library import, renaming with configurable formats.
- History, blocklist, remote path mappings.

Notifications and system
- Discord, Telegram, Slack, Pushover, Pushbullet, Gotify, ntfy, Apprise, email, webhooks, custom scripts, Kodi, and library updates for Plex, Emby and Jellyfin.
- Health checks, scheduled tasks, backups with restore, logs, update check, users with login and an API key for external tools.

## Getting started

```sh
docker compose up -d
```

Open http://localhost:8989 and create the first user. The metadata service listens on 8990 and the mappings service on 8991. Set `TMDB_API_KEY` and `TVDB_API_KEY` in a `.env` file next to `docker-compose.yml` so the metadata service can reach TMDB and TVDB.

SQLite is the default database. For Postgres start the bundled service with `docker compose --profile postgres up -d` and set `Database__Provider=Postgres` and `ConnectionStrings__Postgres` on the `submarine` service.

Keep your download folder and your media library on the same volume so imports can hardlink instead of copy.

Behind a reverse proxy, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on the `submarine` service so client addresses come from `X-Forwarded-For`. Sign in is rate limited per client address, and without this every user shares the proxy's address. Only enable it when the port is not reachable except through the proxy. To serve under a sub path, set the URL base in Settings > General.

## Services

- `submarine` (`ghcr.io/simplymedia/submarine`): the application and its web UI.
- `submarine-metadata` (`ghcr.io/simplymedia/submarine-metadata`): proxies and caches TVDB and TMDB and returns one normalized shape for series, episodes, movies and collections.
- `submarine-mappings` (`ghcr.io/simplymedia/submarine-mappings`): scene numbering and TVDB to AniList mappings, editable through its API. Writes require `Auth__AdminApiKey`.

Every service can be hosted on its own. Point the application at other instances with `Metadata__BaseUrl` and `Mappings__BaseUrl`.

## Building from source

Backend (.NET 10 SDK):

```sh
cd backend
dotnet build Submarine.slnx
for project in tests/*/*.csproj; do dotnet run --project "$project" -p:OpenApiGenerateDocumentsOnBuild=false; done
dotnet run --project src/Submarine.Api
```

Frontend (Node 22, pnpm 10):

```sh
cd frontend
pnpm install
pnpm dev          # proxies /api and /hubs to http://localhost:8989 (override with API_URL)
pnpm lint && pnpm typecheck && pnpm test
pnpm generate     # static build, copied into the API image
```

End to end tests need a published API and the mock metadata services:

```sh
dotnet publish backend/src/Submarine.Api -c Release -o /tmp/submarine-api
cd frontend && node e2e/stack.mjs start && E2E_BASE_URL=http://localhost:8989 pnpm e2e; node e2e/stack.mjs stop
```

The API document is at `/openapi/openapi.json` and browsable at `/scalar`. `backend/openapi.json` is regenerated on every build and `frontend/app/types/api.d.ts` is generated from it with `pnpm generate:api`.

## Built with

- C# / .NET 10, ASP.NET Core minimal APIs, SignalR, EF Core (SQLite and Postgres), Serilog, xUnit.
- Vue 3, Nuxt 4, TypeScript, Tailwind CSS 4, Reka UI, Pinia, Vitest, Playwright.

## License

See [LICENSE](LICENSE).
