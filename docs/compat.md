# Compat adapters

Submarine exposes separate Sonarr v3, Radarr v3, and Prowlarr v1 compatibility APIs so third-party
tools can use Submarine without knowing it replaced three separate applications.

- `<UrlBase>/compat/sonarr` looks like a Sonarr v3 API to Sonarr clients.
- `<UrlBase>/compat/radarr` looks like a Radarr v3 API to Radarr clients.
- `<UrlBase>/compat/prowlarr` looks like a Prowlarr v1 API to Prowlarr clients.

All three share Submarine's own hostname, port, SSL certificate, and global API key. There is
nothing extra to run and no separate credentials to manage.

## Supported consumers

| Consumer | Version |
| --- | --- |
| Overseerr | 1.35.0 |
| Jellyseerr | 2.7.3 |
| Seerr | 3.4.1 |
| Bazarr | 1.6.1 |
| Recyclarr | 8.7.0 |
| Unpackerr | 0.16.1 |
| Homepage | 2.4.0 |
| Notifiarr | 0.9.7 |
| Maintainerr | 2.5.0 |
| Kometa | 2.5.0 |
| ArrAPI | 1.4.14 |
| Decluttarr | 2.0.0 |
| LunaSea | 11.0.0 |

## Configuring a client

Every client above needs the same three things: Submarine's real hostname, its existing port and
SSL setting, and Submarine's global API key from **Settings → General**. Nothing about the
connection changes between compat apps other than the URL base.

1. **Host and port.** Use the same hostname, port, and `http`/`https` scheme you already use to
   reach Submarine. Do not stand up a separate instance or port for compat traffic.
2. **URL Base.** Set the client's URL base field to the appropriate path:
   - Sonarr-shaped clients: `<UrlBase>/compat/sonarr`
   - Radarr-shaped clients: `<UrlBase>/compat/radarr`
   - Prowlarr-shaped clients: `<UrlBase>/compat/prowlarr`

   `<UrlBase>` is Submarine's own configured URL base (empty by default). If Submarine has no URL
   base configured, use `/compat/sonarr`, `/compat/radarr`, or `/compat/prowlarr` directly.

   **Do not include `/api/v3` or `/api/v1` in the URL base.** The client adds that suffix itself;
   appending it yourself produces a doubled, invalid path.
3. **API key.** Use Submarine's single global API key (Settings → General → Security) for every
   compat endpoint. There is no separate key per facade.

### Field-based clients (Bazarr, Recyclarr, Decluttarr, ArrAPI, Kometa, ...)

These clients take hostname, port, SSL, URL base, and API key as separate fields. Fill in
Submarine's real hostname and port, enable SSL if Submarine uses it, and set the URL base and API
key as described above.

### URL-based clients (Overseerr, Jellyseerr, Seerr, Homepage, Notifiarr, Maintainerr, Unpackerr, LunaSea)

These clients take one full base URL instead of separate fields. Build it as:

```
https://host:port<UrlBase>/compat/<app>
```

For example, with Submarine at `https://media.example.com:8443` and no configured `UrlBase`,
point an Overseerr Sonarr connection at `https://media.example.com:8443/compat/sonarr` and a
Radarr connection at `https://media.example.com:8443/compat/radarr`. Enter the global API key in
the same form. Do not add `/api/v3` or `/api/v1` to this URL; the client appends it.

## Shared library state

The two facades are views of the same native catalog, not separate Sonarr and Radarr databases.
Quality and language profiles, tags, and root folders keep their native IDs and are shared with
native Submarine operations; changes through one transport are visible through the others.

Each Sonarr series or Radarr movie has at most one persisted compatibility binding to a native
media version. If no binding exists, the first facade access selects the lowest-ID version and
persists it. Adding or renaming another version does not change the facade's selected version.
Removing a selected version rebinds to the lowest remaining version. Deleting a facade entry
excludes that entry rather than exposing a sibling version; re-adding it must explicitly reactivate
the binding.

## Prowlarr facade

`<UrlBase>/compat/prowlarr/api/v1` implements the routes Homepage and Notifiarr actually use:

- `GET /system/status`, `GET /health` — status and health issues, same dialect rules as Sonarr/Radarr.
- `GET /indexer`, `/indexer/{id}`, `/indexer/schema` — the configured native indexers, projected as
  Prowlarr provider resources (`fields[]` mirror the indexer's native settings JSON).
- `GET /indexerstats` — per-indexer query/grab/failure totals and per-user-agent totals, recomputed
  from `IndexerHistory` for the requested date range and indexer id filter.
- `GET /search` — a real interactive search across the enabled indexers (`query`, `type`,
  `categories`, `indexerIds`, `limit`, `offset`), returning raw release resources (GUID, indexer,
  protocol, size, seeders/leechers, download/magnet URLs). This is a release lookup only; it does
  not add anything to the library.
- `GET /notification`, `/{id}`, `/schema`; `POST`, `PUT` (collection and `/{id}`); `DELETE /{id}`;
  `POST /notification/test` — Notifiarr and Webhook notifications only, since those are the only
  two upstream implementations either researched consumer touches. A collection `PUT` or `POST`
  with a matching `id` updates the existing row instead of creating a duplicate, so Notifiarr's
  repeated self-registration converges on one notification.
- `POST /search`, `/search/bulk`, and the `/applications` family are **not implemented**. No
  researched Prowlarr consumer (Homepage, Notifiarr) grabs an arbitrary release or manages external
  PVR applications through Prowlarr; both routes return a non-HTML 404 rather than a fake response.

## Outbound Sonarr/Radarr-compatible webhooks

The native **Webhook** notification has a `payloadFormat` field: `Native` (default, Submarine's own
`NotificationMessage` shape) or `SonarrRadarrCompatible` (upstream Sonarr/Radarr webhook envelopes:
`eventType`, `instanceName`, `applicationUrl`, `series`/`episodes` or `movie`, `release`, health
fields, and so on). Set `facade` (`Sonarr` or `Radarr`) to choose which shape non-media events (health,
test) use, and `applicationUrl` to the facade root the payload should advertise. Media events
(series/episode/movie) pick their shape automatically from the event's own media kind.

The **Notifiarr** notification always sends the Sonarr/Radarr-compatible envelope to
`https://notifiarr.com/api/v1/notification/{sonarr|radarr}`, matching what Notifiarr's own
receiver parses; this is unrelated to and independent of the Prowlarr notification registration
above, which lets Notifiarr register *itself* as a listener inside Submarine.

## Sonarr facade

The Sonarr facade currently implements series list/get/lookup/add/update/delete and bulk editing,
episode list/get/monitor updates, episode-file list/get, and season-pass monitoring. Series
responses use the selected compatibility version for their path and file statistics. TVDB and IMDb
lookup terms are passed to the metadata service in their original prefixed form.

The Sonarr facade is not yet complete for Bazarr or LunaSea workflows: episode-file mutation,
release search/grab, manual import, rename, and parse are not implemented. Series DELETE works when
the title has no sibling versions; deleting only the selected version while preserving siblings
returns 409. Bazarr-compatible resource projection is registered, but native event forwarding and
consumer-level SignalR synchronization are not verified. No Sonarr consumer Docker workflow has
been run for this change.

## Radarr facade

The Radarr v3 facade is available at `<UrlBase>/compat/radarr/api/v3`. Movie listing and lookup
use local Submarine IDs while retaining TMDB and IMDb identifiers. Adding, updating, deleting,
movie-file listing/deletion, editor updates, collection previews, import-list previews and movie
exclusions operate on the shared native library. Movie updates and file reads use the persisted
Radarr version binding; deleting one of several versions leaves sibling versions in the native
catalog and tombstones the Radarr entry.

The `/credits` response comes from TMDB movie cast and crew data. `/extrafile` enumerates matching
sidecars next to selected-version movie files; their ids are derived deterministically from the
sidecar's full path rather than a persisted catalog row, because upstream Radarr's `/extrafile`
route is itself read-only and no researched consumer (Overseerr, Seerr, Bazarr, Recyclarr,
Unpackerr, Homepage, Notifiarr, Maintainerr, Kometa/ArrAPI, Decluttarr, LunaSea) ever mutates an
extra file. `/parse`, rename preview/command, and selected-version release search and cached
release grabs are exposed for Radarr clients. `POST /release` accepts a bare `{guid,indexerId}`
from any recent search response, exactly like upstream: when the request omits `movieId`, the
facade resolves it from the cached release candidate the original search already matched to a
movie.

`PUT /movie/{id}` and `PUT /movie/editor` support root-folder changes (`?moveFiles=` on the single
update, `moveFiles` in the editor body) through a version-scoped native move operation: only the
facade-bound `MediaVersion` moves, siblings of a multi-version title are untouched. `GET
/importlist/movie?includeRecommendations=true` still returns 501: no researched consumer reads
TMDB recommendations through Radarr's import-list preview, so no native recommendation source was
built for it.

The Radarr compat SignalR hub (`<UrlBase>/compat/radarr/signalr/messages`) receives `receiveMessage`
events for `movie` add/update/delete, selected-version movie-file import/delete, and selected-version
renames, matching the shape Bazarr's realtime client expects. File- and rename-scoped events are
filtered to the facade's currently bound version so an unselected version's changes are never
published as the facade movie's changes.

Real Docker workflows with Overseerr, Bazarr, and Recyclarr have not been run in this worktree; the
consumer-version table is the target contract, not runtime smoke.

## Limits

- The compat facades implement the consumer route union researched for the versions listed above,
  not every Sonarr, Radarr, or Prowlarr endpoint. Compatibility depends on the consumer using
  routes supported by its integration.
- **Prowlarr's `/applications` endpoint is not supported.** Submarine has no concept of a
  downstream Sonarr/Radarr application to push indexers to, so this route is intentionally
  absent from `<UrlBase>/compat/prowlarr`.
- The compat layer never fabricates data or fakes an operation it cannot perform. If a route is
  unsupported, it returns an error rather than a stub or placeholder response. Do not rely on any
  compat behavior beyond what has been verified against the consumer versions listed above.
