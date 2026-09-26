using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Submarine.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TmdbCollectionId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    PosterUrl = table.Column<string>(type: "text", nullable: true),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    RootFolderId = table.Column<int>(type: "integer", nullable: true),
                    QualityProfileId = table.Column<int>(type: "integer", nullable: true),
                    LanguageProfileId = table.Column<int>(type: "integer", nullable: true),
                    MinimumAvailability = table.Column<int>(type: "integer", nullable: false),
                    SearchOnAdd = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Commands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    BodyHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFormats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IncludeCustomFormatWhenRenaming = table.Column<bool>(type: "boolean", nullable: false),
                    Specifications = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelayProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PreferredProtocol = table.Column<int>(type: "integer", nullable: false),
                    UsenetDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    TorrentDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    BypassIfHighestQuality = table.Column<bool>(type: "boolean", nullable: false),
                    BypassIfAboveCustomFormatScore = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumCustomFormatScore = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Enable = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    RemoveCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveFailed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EnableCompletedDownloadHandling = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveCompletedDownloads = table.Column<bool>(type: "boolean", nullable: false),
                    EnableFailedDownloadHandling = table.Column<bool>(type: "boolean", nullable: false),
                    RedownloadFailedReleases = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveFailedDownloads = table.Column<bool>(type: "boolean", nullable: false),
                    CheckForFinishedDownloadInterval = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AuthMethod = table.Column<int>(type: "integer", nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FeedToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UrlBase = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    InstanceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LogLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Branch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdateAutomatically = table.Column<bool>(type: "boolean", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HealthIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    WikiUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportListExclusions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListExclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Enable = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAutomaticAdd = table.Column<bool>(type: "boolean", nullable: false),
                    SearchOnAdd = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    MediaKind = table.Column<int>(type: "integer", nullable: false),
                    QualityProfileId = table.Column<int>(type: "integer", nullable: true),
                    LanguageProfileId = table.Column<int>(type: "integer", nullable: true),
                    RootFolderId = table.Column<int>(type: "integer", nullable: true),
                    Monitor = table.Column<int>(type: "integer", nullable: false),
                    MinimumAvailability = table.Column<int>(type: "integer", nullable: true),
                    SeriesType = table.Column<int>(type: "integer", nullable: true),
                    SeasonFolder = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexerConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RssSyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumAgeMinutes = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaximumSizeMb = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityDelayDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexerDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefinitionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    Links = table.Column<string>(type: "text", nullable: false),
                    Yaml = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpstreamUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexerProxies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    RequestTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerProxies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LanguageProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    Cutoff = table.Column<int>(type: "integer", nullable: false),
                    UpgradeAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaManagementConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UseHardlinks = table.Column<bool>(type: "boolean", nullable: false),
                    ImportExtraFiles = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraFileExtensions = table.Column<string>(type: "text", nullable: false),
                    MinimumFreeSpaceMb = table.Column<int>(type: "integer", nullable: false),
                    SkipFreeSpaceCheck = table.Column<bool>(type: "boolean", nullable: false),
                    WriteNfo = table.Column<bool>(type: "boolean", nullable: false),
                    RecycleBinPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RecycleBinCleanupDays = table.Column<int>(type: "integer", nullable: false),
                    CreateEmptySeriesFolders = table.Column<bool>(type: "boolean", nullable: false),
                    CreateEmptyMovieFolders = table.Column<bool>(type: "boolean", nullable: false),
                    DeleteEmptyFolders = table.Column<bool>(type: "boolean", nullable: false),
                    UnmonitorDeletedFiles = table.Column<bool>(type: "boolean", nullable: false),
                    ChmodFolder = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ChmodFile = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ChownGroup = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DownloadPropersAndRepacks = table.Column<int>(type: "integer", nullable: false),
                    EnableMediaInfo = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaManagementConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TmdbId = table.Column<int>(type: "integer", nullable: false),
                    ImdbId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SortTitle = table.Column<string>(type: "text", nullable: false),
                    CleanTitle = table.Column<string>(type: "text", nullable: false),
                    OriginalTitle = table.Column<string>(type: "text", nullable: true),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Runtime = table.Column<int>(type: "integer", nullable: true),
                    Studio = table.Column<string>(type: "text", nullable: true),
                    PosterUrl = table.Column<string>(type: "text", nullable: true),
                    BackdropUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InCinemasDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DigitalReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhysicalReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAnime = table.Column<bool>(type: "boolean", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumAvailability = table.Column<int>(type: "integer", nullable: false),
                    TmdbCollectionId = table.Column<int>(type: "integer", nullable: true),
                    CollectionTitle = table.Column<string>(type: "text", nullable: true),
                    Genres = table.Column<string>(type: "text", nullable: false),
                    Certification = table.Column<string>(type: "text", nullable: true),
                    YouTubeTrailerId = table.Column<string>(type: "text", nullable: true),
                    LastSearchTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamingConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RenameEpisodes = table.Column<bool>(type: "boolean", nullable: false),
                    RenameMovies = table.Column<bool>(type: "boolean", nullable: false),
                    ReplaceIllegalCharacters = table.Column<bool>(type: "boolean", nullable: false),
                    ColonReplacement = table.Column<int>(type: "integer", nullable: false),
                    StandardEpisodeFormat = table.Column<string>(type: "text", nullable: false),
                    DailyEpisodeFormat = table.Column<string>(type: "text", nullable: false),
                    AnimeEpisodeFormat = table.Column<string>(type: "text", nullable: false),
                    SeriesFolderFormat = table.Column<string>(type: "text", nullable: false),
                    SeasonFolderFormat = table.Column<string>(type: "text", nullable: false),
                    SpecialsFolderFormat = table.Column<string>(type: "text", nullable: false),
                    MovieFormat = table.Column<string>(type: "text", nullable: false),
                    MovieFolderFormat = table.Column<string>(type: "text", nullable: false),
                    MultiEpisodeStyle = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Enable = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    OnGrab = table.Column<bool>(type: "boolean", nullable: false),
                    OnImport = table.Column<bool>(type: "boolean", nullable: false),
                    OnUpgrade = table.Column<bool>(type: "boolean", nullable: false),
                    OnRename = table.Column<bool>(type: "boolean", nullable: false),
                    OnDelete = table.Column<bool>(type: "boolean", nullable: false),
                    OnHealthIssue = table.Column<bool>(type: "boolean", nullable: false),
                    OnHealthRestored = table.Column<bool>(type: "boolean", nullable: false),
                    OnApplicationUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    OnManualInteractionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeHealthWarnings = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<int>(type: "integer", nullable: true),
                    Resolution = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MinSizeMbPerMinute = table.Column<double>(type: "double precision", nullable: true),
                    MaxSizeMbPerMinute = table.Column<double>(type: "double precision", nullable: true),
                    PreferredSizeMbPerMinute = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpgradeAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Cutoff = table.Column<int>(type: "integer", nullable: false),
                    Items = table.Column<string>(type: "text", nullable: false),
                    FormatItems = table.Column<string>(type: "text", nullable: false),
                    MinFormatScore = table.Column<int>(type: "integer", nullable: false),
                    CutoffFormatScore = table.Column<int>(type: "integer", nullable: false),
                    MinUpgradeFormatScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Values = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseGroupQualityOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseGroup = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseGroupQualityOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Required = table.Column<string>(type: "text", nullable: false),
                    Ignored = table.Column<string>(type: "text", nullable: false),
                    IndexerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemotePathMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Host = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RemotePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    LocalPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemotePathMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RootFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MediaKind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RootFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastRun = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRun = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TvdbId = table.Column<int>(type: "integer", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    ImdbId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SortTitle = table.Column<string>(type: "text", nullable: false),
                    CleanTitle = table.Column<string>(type: "text", nullable: false),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    Network = table.Column<string>(type: "text", nullable: true),
                    Runtime = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    PosterUrl = table.Column<string>(type: "text", nullable: true),
                    BackdropUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    MetadataProvider = table.Column<int>(type: "integer", nullable: false),
                    Numbering = table.Column<int>(type: "integer", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    MonitorNewItems = table.Column<int>(type: "integer", nullable: false),
                    SeasonFolder = table.Column<bool>(type: "boolean", nullable: false),
                    Genres = table.Column<string>(type: "text", nullable: false),
                    Certification = table.Column<string>(type: "text", nullable: true),
                    FirstAired = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UiConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<int>(type: "integer", nullable: false),
                    FirstDayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    CalendarWeekColumnHeader = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDateFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LongDateFormat = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShowRelativeDates = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Indexers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Implementation = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    EnableRss = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAutomaticSearch = table.Column<bool>(type: "boolean", nullable: false),
                    EnableInteractiveSearch = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    DownloadClientId = table.Column<int>(type: "integer", nullable: true),
                    ProxyId = table.Column<int>(type: "integer", nullable: true),
                    Categories = table.Column<string>(type: "text", nullable: false),
                    AnimeCategories = table.Column<string>(type: "text", nullable: false),
                    MinimumSeeders = table.Column<int>(type: "integer", nullable: true),
                    SeedRatio = table.Column<double>(type: "double precision", nullable: true),
                    SeedTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    SeasonPackSeedTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    AnimeStandardFormatSearch = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indexers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Indexers_DownloadClients_DownloadClientId",
                        column: x => x.DownloadClientId,
                        principalTable: "DownloadClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Indexers_IndexerProxies_ProxyId",
                        column: x => x.ProxyId,
                        principalTable: "IndexerProxies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AlternativeTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SceneSeasonNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlternativeTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlternativeTitles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlternativeTitles_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: false),
                    AbsoluteEpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    SceneSeasonNumber = table.Column<int>(type: "integer", nullable: true),
                    SceneEpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    SceneAbsoluteEpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    AirDate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AirDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Runtime = table.Column<int>(type: "integer", nullable: true),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    LastSearchTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    QualityProfileId = table.Column<int>(type: "integer", nullable: false),
                    LanguageProfileId = table.Column<int>(type: "integer", nullable: false),
                    RootFolderId = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaVersions_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaVersions_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendingReleases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Release = table.Column<string>(type: "text", nullable: false),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    EpisodeIds = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingReleases_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PendingReleases_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Monitored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DelayProfileTag",
                columns: table => new
                {
                    DelayProfileId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayProfileTag", x => new { x.DelayProfileId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_DelayProfileTag_DelayProfiles_DelayProfileId",
                        column: x => x.DelayProfileId,
                        principalTable: "DelayProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DelayProfileTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadClientTag",
                columns: table => new
                {
                    DownloadClientId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadClientTag", x => new { x.DownloadClientId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_DownloadClientTag_DownloadClients_DownloadClientId",
                        column: x => x.DownloadClientId,
                        principalTable: "DownloadClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadClientTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportListTag",
                columns: table => new
                {
                    ImportListId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListTag", x => new { x.ImportListId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ImportListTag_ImportLists_ImportListId",
                        column: x => x.ImportListId,
                        principalTable: "ImportLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportListTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexerProxyTag",
                columns: table => new
                {
                    IndexerProxyId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerProxyTag", x => new { x.IndexerProxyId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_IndexerProxyTag_IndexerProxies_IndexerProxyId",
                        column: x => x.IndexerProxyId,
                        principalTable: "IndexerProxies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexerProxyTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieTag",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieTag", x => new { x.MovieId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_MovieTag_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTag",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTag", x => new { x.NotificationId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_NotificationTag_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseProfileTag",
                columns: table => new
                {
                    ReleaseProfileId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseProfileTag", x => new { x.ReleaseProfileId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ReleaseProfileTag_ReleaseProfiles_ReleaseProfileId",
                        column: x => x.ReleaseProfileId,
                        principalTable: "ReleaseProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReleaseProfileTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesTag",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesTag", x => new { x.SeriesId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_SeriesTag_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlocklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseTitle = table.Column<string>(type: "text", nullable: false),
                    Guid = table.Column<string>(type: "text", nullable: true),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    IndexerId = table.Column<int>(type: "integer", nullable: true),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    EpisodeIds = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlocklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlocklistItems_Indexers_IndexerId",
                        column: x => x.IndexerId,
                        principalTable: "Indexers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BlocklistItems_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BlocklistItems_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IndexerHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IndexerId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Successful = table.Column<bool>(type: "boolean", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: true),
                    Categories = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    ElapsedMs = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexerHistories_Indexers_IndexerId",
                        column: x => x.IndexerId,
                        principalTable: "Indexers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexerStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IndexerId = table.Column<int>(type: "integer", nullable: false),
                    DisabledUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitialFailure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MostRecentFailure = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    LastRssSyncReleaseInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexerStatuses_Indexers_IndexerId",
                        column: x => x.IndexerId,
                        principalTable: "Indexers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexerTag",
                columns: table => new
                {
                    IndexerId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexerTag", x => new { x.IndexerId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_IndexerTag_Indexers_IndexerId",
                        column: x => x.IndexerId,
                        principalTable: "Indexers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexerTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EpisodeFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: true),
                    SceneName = table.Column<string>(type: "text", nullable: true),
                    Edition = table.Column<string>(type: "text", nullable: true),
                    MediaInfo = table.Column<string>(type: "text", nullable: true),
                    NamedFromPlaceholder = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodeFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpisodeFiles_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EpisodeFiles_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    EpisodeId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: true),
                    SourceTitle = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: true),
                    Languages = table.Column<string>(type: "text", nullable: true),
                    DownloadId = table.Column<string>(type: "text", nullable: true),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MovieFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: true),
                    SceneName = table.Column<string>(type: "text", nullable: true),
                    Edition = table.Column<string>(type: "text", nullable: true),
                    MediaInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieFiles_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieFiles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackedDownloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DownloadClientId = table.Column<int>(type: "integer", nullable: false),
                    DownloadId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReleaseTitle = table.Column<string>(type: "text", nullable: true),
                    Quality = table.Column<string>(type: "text", nullable: true),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    ReleaseGroup = table.Column<string>(type: "text", nullable: true),
                    IndexerId = table.Column<int>(type: "integer", nullable: true),
                    OutputPath = table.Column<string>(type: "text", nullable: true),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    MovieId = table.Column<int>(type: "integer", nullable: true),
                    MediaVersionId = table.Column<int>(type: "integer", nullable: true),
                    EpisodeIds = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    SizeLeft = table.Column<long>(type: "bigint", nullable: false),
                    StatusMessages = table.Column<string>(type: "text", nullable: false),
                    Imported = table.Column<bool>(type: "boolean", nullable: false),
                    Added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MissedPolls = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedDownloads_DownloadClients_DownloadClientId",
                        column: x => x.DownloadClientId,
                        principalTable: "DownloadClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackedDownloads_Indexers_IndexerId",
                        column: x => x.IndexerId,
                        principalTable: "Indexers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrackedDownloads_MediaVersions_MediaVersionId",
                        column: x => x.MediaVersionId,
                        principalTable: "MediaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrackedDownloads_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrackedDownloads_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EpisodeFileEpisode",
                columns: table => new
                {
                    EpisodesId = table.Column<int>(type: "integer", nullable: false),
                    FilesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodeFileEpisode", x => new { x.EpisodesId, x.FilesId });
                    table.ForeignKey(
                        name: "FK_EpisodeFileEpisode_EpisodeFiles_FilesId",
                        column: x => x.FilesId,
                        principalTable: "EpisodeFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EpisodeFileEpisode_Episodes_EpisodesId",
                        column: x => x.EpisodesId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_MovieId",
                table: "AlternativeTitles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_SeriesId",
                table: "AlternativeTitles",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_BlocklistItems_IndexerId",
                table: "BlocklistItems",
                column: "IndexerId");

            migrationBuilder.CreateIndex(
                name: "IX_BlocklistItems_MovieId",
                table: "BlocklistItems",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_BlocklistItems_SeriesId",
                table: "BlocklistItems",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TmdbCollectionId",
                table: "Collections",
                column: "TmdbCollectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commands_Name_BodyHash",
                table: "Commands",
                columns: new[] { "Name", "BodyHash" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_Status",
                table: "Commands",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DelayProfileTag_TagsId",
                table: "DelayProfileTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadClientTag_TagsId",
                table: "DownloadClientTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeFileEpisode_FilesId",
                table: "EpisodeFileEpisode",
                column: "FilesId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeFiles_MediaVersionId",
                table: "EpisodeFiles",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeFiles_SeriesId",
                table: "EpisodeFiles",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeriesId_SeasonNumber_EpisodeNumber",
                table: "Episodes",
                columns: new[] { "SeriesId", "SeasonNumber", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_Date",
                table: "HistoryEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_DownloadId",
                table: "HistoryEvents",
                column: "DownloadId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_EpisodeId",
                table: "HistoryEvents",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_MediaVersionId",
                table: "HistoryEvents",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_MovieId",
                table: "HistoryEvents",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_SeriesId",
                table: "HistoryEvents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportListTag_TagsId",
                table: "ImportListTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexerDefinitions_DefinitionId",
                table: "IndexerDefinitions",
                column: "DefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexerHistories_Date",
                table: "IndexerHistories",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_IndexerHistories_IndexerId",
                table: "IndexerHistories",
                column: "IndexerId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexerProxyTag_TagsId",
                table: "IndexerProxyTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Indexers_DownloadClientId",
                table: "Indexers",
                column: "DownloadClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Indexers_ProxyId",
                table: "Indexers",
                column: "ProxyId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexerStatuses_IndexerId",
                table: "IndexerStatuses",
                column: "IndexerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexerTag_TagsId",
                table: "IndexerTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaVersions_MovieId",
                table: "MediaVersions",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaVersions_SeriesId",
                table: "MediaVersions",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieFiles_MediaVersionId",
                table: "MovieFiles",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieFiles_MovieId",
                table: "MovieFiles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies",
                column: "TmdbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieTag_TagsId",
                table: "MovieTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTag_TagsId",
                table: "NotificationTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingReleases_MovieId",
                table: "PendingReleases",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingReleases_SeriesId",
                table: "PendingReleases",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityDefinitions_Source_Resolution",
                table: "QualityDefinitions",
                columns: new[] { "Source", "Resolution" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroupQualityOverrides_ReleaseGroup",
                table: "ReleaseGroupQualityOverrides",
                column: "ReleaseGroup",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseProfileTag_TagsId",
                table: "ReleaseProfileTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_RootFolders_Path",
                table: "RootFolders",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_Name",
                table: "ScheduledTasks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_SeriesId_SeasonNumber",
                table: "Seasons",
                columns: new[] { "SeriesId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_TvdbId",
                table: "Series",
                column: "TvdbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesTag_TagsId",
                table: "SeriesTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Label",
                table: "Tags",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_DownloadClientId_DownloadId",
                table: "TrackedDownloads",
                columns: new[] { "DownloadClientId", "DownloadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_IndexerId",
                table: "TrackedDownloads",
                column: "IndexerId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_MediaVersionId",
                table: "TrackedDownloads",
                column: "MediaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_MovieId",
                table: "TrackedDownloads",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedDownloads_SeriesId",
                table: "TrackedDownloads",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlternativeTitles");

            migrationBuilder.DropTable(
                name: "BlocklistItems");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "Commands");

            migrationBuilder.DropTable(
                name: "CustomFormats");

            migrationBuilder.DropTable(
                name: "DelayProfileTag");

            migrationBuilder.DropTable(
                name: "DownloadClientTag");

            migrationBuilder.DropTable(
                name: "DownloadConfig");

            migrationBuilder.DropTable(
                name: "EpisodeFileEpisode");

            migrationBuilder.DropTable(
                name: "GeneralConfig");

            migrationBuilder.DropTable(
                name: "HealthIssues");

            migrationBuilder.DropTable(
                name: "HistoryEvents");

            migrationBuilder.DropTable(
                name: "ImportListExclusions");

            migrationBuilder.DropTable(
                name: "ImportListTag");

            migrationBuilder.DropTable(
                name: "IndexerConfig");

            migrationBuilder.DropTable(
                name: "IndexerDefinitions");

            migrationBuilder.DropTable(
                name: "IndexerHistories");

            migrationBuilder.DropTable(
                name: "IndexerProxyTag");

            migrationBuilder.DropTable(
                name: "IndexerStatuses");

            migrationBuilder.DropTable(
                name: "IndexerTag");

            migrationBuilder.DropTable(
                name: "LanguageProfiles");

            migrationBuilder.DropTable(
                name: "MediaManagementConfig");

            migrationBuilder.DropTable(
                name: "MovieFiles");

            migrationBuilder.DropTable(
                name: "MovieTag");

            migrationBuilder.DropTable(
                name: "NamingConfig");

            migrationBuilder.DropTable(
                name: "NotificationTag");

            migrationBuilder.DropTable(
                name: "PendingReleases");

            migrationBuilder.DropTable(
                name: "QualityDefinitions");

            migrationBuilder.DropTable(
                name: "QualityProfiles");

            migrationBuilder.DropTable(
                name: "ReleaseFilters");

            migrationBuilder.DropTable(
                name: "ReleaseGroupQualityOverrides");

            migrationBuilder.DropTable(
                name: "ReleaseProfileTag");

            migrationBuilder.DropTable(
                name: "RemotePathMappings");

            migrationBuilder.DropTable(
                name: "RootFolders");

            migrationBuilder.DropTable(
                name: "ScheduledTasks");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "SeriesTag");

            migrationBuilder.DropTable(
                name: "TrackedDownloads");

            migrationBuilder.DropTable(
                name: "UiConfig");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "DelayProfiles");

            migrationBuilder.DropTable(
                name: "EpisodeFiles");

            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "ImportLists");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ReleaseProfiles");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Indexers");

            migrationBuilder.DropTable(
                name: "MediaVersions");

            migrationBuilder.DropTable(
                name: "DownloadClients");

            migrationBuilder.DropTable(
                name: "IndexerProxies");

            migrationBuilder.DropTable(
                name: "Movies");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
