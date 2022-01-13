FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Submarine.Api/Submarine.Api.csproj", "Submarine.Api/"]
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
