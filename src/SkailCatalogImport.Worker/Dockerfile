# Build context: repository root.
#   podman build -f src/SkailCatalogImport.Worker/Dockerfile -t catalog-import-worker .
# dotnet publish runs in the SDK image; the final image only needs the .NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/SkailCatalogImport.Contracts/SkailCatalogImport.Contracts.csproj src/SkailCatalogImport.Contracts/
COPY src/SkailCatalogImport.Worker/SkailCatalogImport.Worker.csproj src/SkailCatalogImport.Worker/
RUN dotnet restore src/SkailCatalogImport.Worker/SkailCatalogImport.Worker.csproj
COPY src/SkailCatalogImport.Contracts src/SkailCatalogImport.Contracts
COPY src/SkailCatalogImport.Worker src/SkailCatalogImport.Worker
RUN dotnet publish src/SkailCatalogImport.Worker/SkailCatalogImport.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SkailCatalogImport.Worker.dll"]
