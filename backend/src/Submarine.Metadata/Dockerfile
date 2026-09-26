FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Submarine.Contracts/Submarine.Contracts.csproj src/Submarine.Contracts/
COPY src/Submarine.Metadata/Submarine.Metadata.csproj src/Submarine.Metadata/
RUN dotnet restore src/Submarine.Metadata/Submarine.Metadata.csproj
COPY . .
RUN dotnet publish src/Submarine.Metadata/Submarine.Metadata.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN apk add --no-cache icu-libs icu-data-full tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
# The runtime image ships a non-root user named app.
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
	CMD wget --quiet --tries=1 --spider http://127.0.0.1:8080/_status/healthz || exit 1
ENTRYPOINT ["dotnet", "Submarine.Metadata.dll"]
