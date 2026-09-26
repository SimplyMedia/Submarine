FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Submarine.Contracts/Submarine.Contracts.csproj src/Submarine.Contracts/
COPY src/Submarine.Mappings/Submarine.Mappings.csproj src/Submarine.Mappings/
RUN dotnet restore src/Submarine.Mappings/Submarine.Mappings.csproj

COPY src/Submarine.Contracts/ src/Submarine.Contracts/
COPY src/Submarine.Mappings/ src/Submarine.Mappings/
RUN dotnet publish src/Submarine.Mappings/Submarine.Mappings.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
EXPOSE 8080

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
	ASPNETCORE_HTTP_PORTS=8080 \
	ConnectionStrings__SqliteConnection="Data Source=/config/mappings.db;Cache=Shared"

RUN apk add --no-cache icu-libs icu-data-full tzdata su-exec \
	&& mkdir -p /config

COPY --from=build /app/publish .
COPY src/Submarine.Mappings/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

VOLUME /config

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
	CMD wget -qO- http://127.0.0.1:8080/_status/healthz >/dev/null 2>&1 || exit 1

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["dotnet", "Submarine.Mappings.dll"]
