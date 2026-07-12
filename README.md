# Submarine

Submarine is a PVR for Usenet and BitTorrent users that manages TV and movies (including anime) in one tool. It monitors indexers for new releases, grabs them through your download client, and imports, renames and upgrades files automatically.

Submarine is inspired by [Sonarr](https://github.com/Sonarr/Sonarr/) and [Radarr](https://github.com/Radarr/Radarr/) but written from scratch on a current stack.

## Features

### Library
- [x] TV and movies in one application, including anime and anime movies
- [x] Metadata from TVDB and TMDB through a self-hostable proxy service
- [x] Aired, DVD and absolute episode orderings
- [x] Automatic metadata refresh with episode title diffing
- [x] Auto-rename when an episode title becomes available later (files imported as "Episode 1" get renamed once TVDB/TMDB delivers the real title)
- [x] Root folders, tags, calendar

### Releases
- [x] Torznab and Newznab indexer support
- [x] Release title parsing (quality, languages, streaming source, release group, editions, torrent flags)
- [x] Quality and language profiles with cutoffs and upgrades
- [x] Built-in filters for release group, indexer, quality, language and source with allow/block/prefer tiering, so most setups need no custom formats
- [x] Custom formats as the escape hatch, with per-profile scores
- [x] Consistent release group preference for season downloads

### Downloads
- [x] Download clients: qBittorrent, Transmission, Deluge, rTorrent, uTorrent, Aria2, Flood, Download Station, SABnzbd, NZBGet, torrent/usenet blackhole
- [x] Queue with download monitoring, import pipeline with hardlink-or-move
- [x] History for grabs, imports, renames and deletes
- [x] Import lists (TMDB, Trakt, AniList)
- [x] Plex, Emby and Jellyfin connections (library refresh on import and rename)
- [x] Background jobs for everything heavy; the API stays responsive

### Services
- [x] `Submarine.Api` - the main application
- [x] `Submarine.Metadata` - open source Skyhook alternative proxying and caching TVDB and TMDB
- [x] `Submarine.Mappings` - open source TheXEM alternative for scene numbering plus TVDB <-> AniList mapping with arc/season resolution
- [x] Fully documented API with OpenAPI
- [ ] Web UI (planned, the API is built for it)

## Getting Started

```sh
docker compose up -d
```

The API listens on port 8989, the metadata proxy on 8990, the mappings service on 8991. SQLite is the default database; set `Database__Provider=Postgres` and a `ConnectionStrings__PostgreSQLConnection` for larger instances (see the commented block in `docker-compose.yml`).

### Building from source

Requires the .NET 10 SDK.

```sh
cd backend
dotnet build Submarine.sln
dotnet test Submarine.sln
dotnet run --project Submarine.Api
```

## Built With

* [C#](https://docs.microsoft.com/en-us/dotnet/csharp/) / [.NET 10](https://docs.microsoft.com/en-us/dotnet/) - Language and runtime
* [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) - Web framework
* [EntityFramework Core](https://docs.microsoft.com/en-us/ef/core/) - ORM (SQLite and PostgreSQL)
* [xUnit](https://github.com/xunit/xunit) - Tests

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/SimplyMedia/Submarine/tags).

## Authors

* **DevYukine** - *Initial work* - [DevYukine](https://github.com/DevYukine)

See also the list of [contributors](https://github.com/SimplyMedia/Submarine/contributors) who participated in this project.
