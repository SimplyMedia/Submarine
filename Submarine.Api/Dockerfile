FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Submarine.Api/Submarine.Api.csproj", "Submarine.Api/"]
COPY ["Submarine.Core/Submarine.Core.csproj", "Submarine.Core/"]
RUN dotnet restore "Submarine.Api/Submarine.Api.csproj"
COPY . .
WORKDIR "/src/Submarine.Api"
RUN dotnet build "Submarine.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Submarine.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Submarine.Api.dll"]
