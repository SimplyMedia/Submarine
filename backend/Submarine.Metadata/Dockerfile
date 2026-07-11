FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Submarine.Metadata/Submarine.Metadata.csproj", "Submarine.Metadata/"]
COPY ["Submarine.Metadata.Contracts/Submarine.Metadata.Contracts.csproj", "Submarine.Metadata.Contracts/"]
RUN dotnet restore "Submarine.Metadata/Submarine.Metadata.csproj"
COPY . .
WORKDIR "/src/Submarine.Metadata"
RUN dotnet build "Submarine.Metadata.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submarine.Metadata.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submarine.Metadata.dll"]
