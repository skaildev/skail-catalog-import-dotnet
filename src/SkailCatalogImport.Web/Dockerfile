# Build context: repository root.
#   podman build -f src/SkailCatalogImport.Web/Dockerfile -t catalog-import-web .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/SkailCatalogImport.Contracts/SkailCatalogImport.Contracts.csproj src/SkailCatalogImport.Contracts/
COPY src/SkailCatalogImport.Web/SkailCatalogImport.Web.csproj src/SkailCatalogImport.Web/
RUN dotnet restore src/SkailCatalogImport.Web/SkailCatalogImport.Web.csproj
COPY src/SkailCatalogImport.Contracts src/SkailCatalogImport.Contracts
COPY src/SkailCatalogImport.Web src/SkailCatalogImport.Web
RUN dotnet publish src/SkailCatalogImport.Web/SkailCatalogImport.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SkailCatalogImport.Web.dll"]
