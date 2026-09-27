# Submarine

[![Build](https://github.com/SimplyMedia/Submarine/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/SimplyMedia/Submarine/actions/workflows/build.yml)
[![Image](https://img.shields.io/badge/image-ghcr.io%2Fsimplymedia%2Fsubmarine--api-blue?logo=docker&logoColor=white)](https://github.com/orgs/SimplyMedia/packages/container/package/submarine-api)

Find, download, import, and rename TV series, anime, and movies from Usenet and BitTorrent. One app instead of Sonarr, Radarr, and Prowlarr, with its own metadata and scene numbering services.

[Quick start](#quick-start) · [Example compose file](examples/docker-compose.yml) · [Configuration](#configuration) · [Development](#development)

## What it does

| Area | Main features |
|---|---|
| Library | Series (standard, daily, anime) and movies, several versions per item (for example 1080p and 4K), collections, calendar with iCal feed, wanted lists, import lists (Trakt, Plex, TMDB, AniList, MyAnimeList, Simkl, IMDb, RSS, other instances), auto tagging |
| Releases | Quality and language profiles, custom formats with TRaSH import, delay and release profiles, a title parser that tells BluRay discs, remuxes, and BDRips apart |
| Indexers | Torznab, Newznab, and Cardigann definitions synced from Prowlarr, proxies and FlareSolverr, stats, and a Newznab API for other apps |
| Downloads | qBittorrent, Transmission, Deluge, rTorrent, uTorrent, Aria2, Flood, Download Station, Vuze, Hadouken, Freebox, rqbit, SABnzbd, NZBGet, NZBVortex, Pneumatic, blackhole folders |
| Import | Hardlink or move, renaming, extras and subtitles, metadata files for Kodi, Plex, Emby, Roksbox, and WDTV, file dates, recycle bin, manual import, adopting an existing library |
| Notifications | Discord, Telegram, Slack, Pushover, Pushbullet, Pushcut, Pushsafer, Prowl, Join, Simplepush, Gotify, ntfy, Signal, Apprise, Notifiarr, email, Mailgun, SendGrid, webhooks, scripts, Trakt, Twitter, Kodi, Plex, Emby, Jellyfin, Synology |
| System | Health checks, scheduled tasks, backups with restore, logs, update history, forms or basic auth, outbound proxy, API key |
| Integrations | Sonarr, Radarr, and Prowlarr compatible APIs, so Overseerr, Jellyseerr, Bazarr, Recyclarr, and similar tools connect to Submarine |

Submarine runs as three containers. `submarine-api` is the app and web UI. `submarine-metadata` fetches and caches TMDB and TVDB, like Skyhook. `submarine-mappings` holds scene numbering and AniList mappings, like TheXEM. Each can run on its own host.

## Quick start

You need Docker with Compose, a [TMDB API key](https://www.themoviedb.org/settings/api), and a [TVDB API key](https://thetvdb.com/api-information).

```sh
mkdir submarine && cd submarine
curl -LO https://raw.githubusercontent.com/SimplyMedia/Submarine/develop/examples/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/SimplyMedia/Submarine/develop/examples/.env.example
```

Fill in `.env`, then start it. Until this version reaches `master`, set `SUBMARINE_TAG=develop`.

```sh
# Shared secret between the three containers
sed -i "s/^INTERNAL_API_KEY=.*/INTERNAL_API_KEY=$(openssl rand -hex 32)/" .env
docker compose up -d
```

Open http://localhost:8989 and create your account right away. Until you do, anyone who can reach the port can claim the instance.

**Hardlinks:** keep downloads and the library under `MEDIA_PATH`, so imports link files instead of copying them.

## Configuration

Most settings live in the web UI. These are set on the containers:

| Variable | Use it to |
|---|---|
| `SUBMARINE_TAG` | Pick the image tag, see [image tags](#image-tags) |
| `MEDIA_PATH` | Mount the folder that holds downloads and the library at `/media` |
| `PUID`, `PGID` | Run as the user that owns your media (see `id` on the host) |
| `INTERNAL_API_KEY` | Let the app talk to the metadata and mappings services, and keep others out |
| `TMDB_API_KEY`, `TVDB_API_KEY`, `TVDB_PIN` | Fetch series and movie metadata |
| `Database__Provider=Postgres`, `ConnectionStrings__Postgres` | Use Postgres instead of the default SQLite (see the `postgres` profile in the example) |

Set a URL base in Settings > General to serve Submarine under a sub path. Behind a reverse proxy, add the proxy's address under Settings > General > Trusted proxies so Submarine sees real client addresses. Data, logs, and backups live in `/config`.

### Image tags

| Tag | Contents |
|---|---|
| `latest`, `master` | The current state of `master` |
| `develop` | Development builds from `develop` |
| `<short sha>` | One specific commit from `master` or `develop` |
| `v1.5.5`, `v1.5`, `v1` | Versioned releases, once they are tagged |

The same tags exist for `submarine-metadata` and `submarine-mappings`. Images build for `linux/amd64` and `linux/arm64` after all tests pass.
## Documentation

| Doc | What it covers |
|---|---|
| [Compatibility](docs/compat.md) | Sonarr, Radarr, and Prowlarr facade URLs, supported consumers, and known limits |


## Development

Build and run all three services from source:

```sh
docker compose up -d --build
```

The backend needs the .NET 10 SDK and the frontend Node.js 22 with pnpm. See [AGENTS.md](AGENTS.md) for the checks to run before a change, and [frontend/README.md](frontend/README.md) for the web UI.

```sh
cd backend && dotnet build Submarine.slnx
cd frontend && pnpm install && pnpm dev
```

### Built with

- **.NET 10** with **ASP.NET Core** minimal APIs, **SignalR**, and **EF Core** on **SQLite** or **Postgres**.
- **Nuxt 4 / Vue**, **TypeScript**, **Tailwind CSS**, **Reka UI**, and **Pinia** for the web UI.
- **xUnit**, **Vitest**, and **Playwright** for tests.

## Contributing

Issues and pull requests are welcome. Keep changes focused, target `develop`, and make sure the tests pass. Use [Conventional Commits](https://www.conventionalcommits.org/).

## License

[AGPL-3.0](LICENSE).
