# Build context: the repository root (docker build -f src/EShop.CategoryApi/Dockerfile .)

# ---- build: SDK image, ~800 MB, never shipped ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only: this layer is reused until a .csproj or a package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/EShop.Data.Entities/EShop.Data.Entities.csproj src/EShop.Data.Entities/
COPY src/EShop.Data/EShop.Data.csproj src/EShop.Data/
COPY src/EShop.ServiceDefaults/EShop.ServiceDefaults.csproj src/EShop.ServiceDefaults/
COPY src/EShop.CategoryApi/EShop.CategoryApi.csproj src/EShop.CategoryApi/
RUN dotnet restore src/EShop.CategoryApi/EShop.CategoryApi.csproj

# Then the code: a code change only rebuilds from here.
COPY src/EShop.Data.Entities/ src/EShop.Data.Entities/
COPY src/EShop.Data/ src/EShop.Data/
COPY src/EShop.ServiceDefaults/ src/EShop.ServiceDefaults/
COPY src/EShop.CategoryApi/ src/EShop.CategoryApi/
RUN dotnet publish src/EShop.CategoryApi/EShop.CategoryApi.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime: ASP.NET Core runtime only, ~220 MB ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# The image's non-root user; it listens on 8080 (ASPNETCORE_HTTP_PORTS), not on a privileged port.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EShop.CategoryApi.dll"]
