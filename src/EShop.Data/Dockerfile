# Migrator: a one-off container that applies the EF Core migrations and exits.
# Build context: the repository root (docker build -f src/EShop.Data/Dockerfile .)

# ---- build: SDK image with dotnet-ef, never shipped ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Same restore layers as the ProductApi image, so Docker reuses them from its cache.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/EShop.Data.Entities/EShop.Data.Entities.csproj src/EShop.Data.Entities/
COPY src/EShop.Data/EShop.Data.csproj src/EShop.Data/
COPY src/EShop.ProductApi/EShop.ProductApi.csproj src/EShop.ProductApi/
RUN dotnet restore src/EShop.ProductApi/EShop.ProductApi.csproj

# dotnet-ef, pinned in dotnet-tools.json to the EF Core package version.
COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY src/EShop.Data.Entities/ src/EShop.Data.Entities/
COPY src/EShop.Data/ src/EShop.Data/
COPY src/EShop.ProductApi/ src/EShop.ProductApi/

# The design-time host (ProductApi's Program.cs) needs some connection string to configure the DbContext.
# Nothing connects during the build, and the placeholder is stored neither in the bundle nor in the image.
RUN ConnectionStrings__EShop="Host=build-placeholder" dotnet ef migrations bundle \
    --project src/EShop.Data --startup-project src/EShop.ProductApi \
    --configuration Release --output /app/efbundle

# ---- runtime: the bundle is framework-dependent and hosts ProductApi's startup, so it needs the ASP.NET runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/efbundle .

USER $APP_UID
# Reads ConnectionStrings__EShop from the environment, applies the pending migrations and exits (0 = success).
ENTRYPOINT ["./efbundle"]
