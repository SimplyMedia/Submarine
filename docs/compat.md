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


## Sonarr facade

The Sonarr facade currently implements series list/get/lookup/add/update/delete and bulk editing,
episode list/get/monitor updates, episode-file list/get, and season-pass monitoring. Series
responses use the selected compatibility version for their path and file statistics. TVDB and IMDb
lookup terms are passed to the metadata service in their original prefixed form.

The Sonarr facade is not yet complete for Bazarr or LunaSea workflows: episode-file mutation,
release search/grab, manual import, rename, and parse are not implemented. Selected-version title
deletion is not implemented. Bazarr-compatible resource projection is registered, but native event
forwarding and consumer-level SignalR synchronization are not verified. No Sonarr consumer Docker
workflow has been run for this change.

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
