FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Submarine.Mappings/Submarine.Mappings.csproj", "Submarine.Mappings/"]
COPY ["Submarine.Mappings.Contracts/Submarine.Mappings.Contracts.csproj", "Submarine.Mappings.Contracts/"]
RUN dotnet restore "Submarine.Mappings/Submarine.Mappings.csproj"
COPY . .
WORKDIR "/src/Submarine.Mappings"
RUN dotnet build "Submarine.Mappings.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submarine.Mappings.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submarine.Mappings.dll"]
