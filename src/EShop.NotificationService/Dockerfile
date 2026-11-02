# Build context: the repository root (docker build -f src/EShop.NotificationService/Dockerfile .)

# ---- build: SDK image, never shipped ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only: this layer is reused until a .csproj or a package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/EShop.Data.Entities/EShop.Data.Entities.csproj src/EShop.Data.Entities/
COPY src/EShop.Data/EShop.Data.csproj src/EShop.Data/
COPY src/EShop.Messaging/EShop.Messaging.csproj src/EShop.Messaging/
COPY src/EShop.OrderApi.Contracts/EShop.OrderApi.Contracts.csproj src/EShop.OrderApi.Contracts/
COPY src/EShop.NotificationService/EShop.NotificationService.csproj src/EShop.NotificationService/
RUN dotnet restore src/EShop.NotificationService/EShop.NotificationService.csproj

# Then the code: a code change only rebuilds from here.
COPY src/EShop.Data.Entities/ src/EShop.Data.Entities/
COPY src/EShop.Data/ src/EShop.Data/
COPY src/EShop.Messaging/ src/EShop.Messaging/
COPY src/EShop.OrderApi.Contracts/ src/EShop.OrderApi.Contracts/
COPY src/EShop.NotificationService/ src/EShop.NotificationService/
RUN dotnet publish src/EShop.NotificationService/EShop.NotificationService.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime: the .NET runtime without ASP.NET Core: a worker has no HTTP endpoints ----
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .

# The image's non-root user; no port: the service only connects out (RabbitMQ, PostgreSQL, SMTP).
USER $APP_UID
ENTRYPOINT ["dotnet", "EShop.NotificationService.dll"]
