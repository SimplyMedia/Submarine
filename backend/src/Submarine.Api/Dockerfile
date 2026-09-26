# syntax=docker/dockerfile:1

# ---- Frontend build ---------------------------------------------------------
# Context for this Dockerfile is the repository root: docker build -f backend/src/Submarine.Api/Dockerfile .
FROM node:22-alpine AS frontend
RUN corepack enable
WORKDIR /src/frontend
# corepack runs the pnpm version pinned in package.json; the packages allowed to run
# install scripts are listed in pnpm-workspace.yaml.
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm generate

# ---- Backend build ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/backend

# Restore cache: project files and central package management first.
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/Submarine.Core/Submarine.Core.csproj src/Submarine.Core/
COPY backend/src/Submarine.Contracts/Submarine.Contracts.csproj src/Submarine.Contracts/
COPY backend/src/Submarine.Infrastructure/Submarine.Infrastructure.csproj src/Submarine.Infrastructure/
COPY backend/src/Submarine.Api/Submarine.Api.csproj src/Submarine.Api/
COPY backend/src/Submarine.Metadata/Submarine.Metadata.csproj src/Submarine.Metadata/
COPY backend/src/Submarine.Mappings/Submarine.Mappings.csproj src/Submarine.Mappings/
RUN dotnet restore src/Submarine.Api/Submarine.Api.csproj

COPY backend/ .
RUN dotnet publish src/Submarine.Api/Submarine.Api.csproj -c Release -o /app/publish -p:OpenApiGenerateDocumentsOnBuild=false

# ---- Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache icu-libs icu-data-full ffmpeg postgresql17-client tzdata su-exec

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
	ASPNETCORE_URLS=http://+:8080 \
	Database__Provider=Sqlite \
	ConnectionStrings__Sqlite="Data Source=/config/submarine.db"

WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=frontend /src/frontend/.output/public ./wwwroot

RUN mkdir -p /config && \
	addgroup -g 911 submarine && \
	adduser -D -H -u 911 -G submarine submarine && \
	chown -R 911:911 /config
VOLUME /config
EXPOSE 8080

HEALTHCHECK --interval=60s --timeout=15s --start-period=30s --retries=3 \
	CMD wget -q -O /dev/null http://127.0.0.1:8080/_status/healthz || exit 1

COPY backend/src/Submarine.Api/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["dotnet", "Submarine.Api.dll"]
