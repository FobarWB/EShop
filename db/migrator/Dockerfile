# Migrator: a one-off container that applies the EF Core migrations of every service and exits (#77).
# One migrations bundle per DbContext, each into its own schema; migrate.sh runs them in turn.
# Build context: the repository root (docker build -f db/migrator/Dockerfile .)

# ---- build: SDK image with dotnet-ef, never shipped ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only: this layer is reused until a .csproj or a package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/EShop.Persistence/EShop.Persistence.csproj src/EShop.Persistence/
COPY src/EShop.Messaging/EShop.Messaging.csproj src/EShop.Messaging/
COPY src/EShop.ServiceDefaults/EShop.ServiceDefaults.csproj src/EShop.ServiceDefaults/
COPY src/EShop.OrderApi.Contracts/EShop.OrderApi.Contracts.csproj src/EShop.OrderApi.Contracts/
COPY src/EShop.ProductApi.Contracts/EShop.ProductApi.Contracts.csproj src/EShop.ProductApi.Contracts/
COPY src/EShop.CustomerApi.Contracts/EShop.CustomerApi.Contracts.csproj src/EShop.CustomerApi.Contracts/
COPY src/EShop.Catalog.Data/EShop.Catalog.Data.csproj src/EShop.Catalog.Data/
COPY src/EShop.ShoppingCart/EShop.ShoppingCart.csproj src/EShop.ShoppingCart/
COPY src/EShop.OrderApi/EShop.OrderApi.csproj src/EShop.OrderApi/
COPY src/EShop.CustomerApi/EShop.CustomerApi.csproj src/EShop.CustomerApi/
COPY src/EShop.NotificationService/EShop.NotificationService.csproj src/EShop.NotificationService/
RUN for project in EShop.Catalog.Data EShop.ShoppingCart EShop.OrderApi EShop.CustomerApi EShop.NotificationService; do \
        dotnet restore "src/$project/$project.csproj" || exit 1; \
    done

# dotnet-ef, pinned in dotnet-tools.json to the EF Core package version.
COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY src/EShop.Persistence/ src/EShop.Persistence/
COPY src/EShop.Messaging/ src/EShop.Messaging/
COPY src/EShop.ServiceDefaults/ src/EShop.ServiceDefaults/
COPY src/EShop.OrderApi.Contracts/ src/EShop.OrderApi.Contracts/
COPY src/EShop.ProductApi.Contracts/ src/EShop.ProductApi.Contracts/
COPY src/EShop.CustomerApi.Contracts/ src/EShop.CustomerApi.Contracts/
COPY src/EShop.Catalog.Data/ src/EShop.Catalog.Data/
COPY src/EShop.ShoppingCart/ src/EShop.ShoppingCart/
COPY src/EShop.OrderApi/ src/EShop.OrderApi/
COPY src/EShop.CustomerApi/ src/EShop.CustomerApi/
COPY src/EShop.NotificationService/ src/EShop.NotificationService/

# Each project is its own startup project: its IDesignTimeDbContextFactory creates the context, no service starts
# and no connection string is needed at build time. The bundles get the real one through --connection (migrate.sh).
RUN dotnet ef migrations bundle --project src/EShop.Catalog.Data --configuration Release --output /app/catalog \
 && dotnet ef migrations bundle --project src/EShop.ShoppingCart --configuration Release --output /app/cart \
 && dotnet ef migrations bundle --project src/EShop.OrderApi --configuration Release --output /app/ordering \
 && dotnet ef migrations bundle --project src/EShop.CustomerApi --configuration Release --output /app/customer \
 && dotnet ef migrations bundle --project src/EShop.NotificationService --configuration Release --output /app/notification

# ---- runtime: the bundles are framework-dependent and reference the services' assemblies, three of them ASP.NET ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/ .
COPY --chmod=755 db/migrator/migrate.sh .

USER $APP_UID
# Reads ConnectionStrings__EShop from the environment, applies the pending migrations of each schema and exits
# (0 = all applied; the first failing bundle stops the rest).
ENTRYPOINT ["./migrate.sh"]
