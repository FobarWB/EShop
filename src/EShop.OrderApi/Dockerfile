# Build context: the repository root (docker build -f src/EShop.OrderApi/Dockerfile .)

# ---- build: SDK image, ~800 MB, never shipped ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only: this layer is reused until a .csproj or a package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/EShop.Data.Entities/EShop.Data.Entities.csproj src/EShop.Data.Entities/
COPY src/EShop.Data/EShop.Data.csproj src/EShop.Data/
COPY src/EShop.ServiceDefaults/EShop.ServiceDefaults.csproj src/EShop.ServiceDefaults/
COPY src/EShop.Messaging/EShop.Messaging.csproj src/EShop.Messaging/
COPY src/EShop.OrderApi.Contracts/EShop.OrderApi.Contracts.csproj src/EShop.OrderApi.Contracts/
COPY src/EShop.OrderApi/EShop.OrderApi.csproj src/EShop.OrderApi/
RUN dotnet restore src/EShop.OrderApi/EShop.OrderApi.csproj

# Then the code: a code change only rebuilds from here.
COPY src/EShop.Data.Entities/ src/EShop.Data.Entities/
COPY src/EShop.Data/ src/EShop.Data/
COPY src/EShop.ServiceDefaults/ src/EShop.ServiceDefaults/
COPY src/EShop.Messaging/ src/EShop.Messaging/
COPY src/EShop.OrderApi.Contracts/ src/EShop.OrderApi.Contracts/
COPY src/EShop.OrderApi/ src/EShop.OrderApi/
RUN dotnet publish src/EShop.OrderApi/EShop.OrderApi.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime: ASP.NET Core runtime only, ~220 MB ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# The image's non-root user; it listens on 8080 (ASPNETCORE_HTTP_PORTS), not on a privileged port.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EShop.OrderApi.dll"]
