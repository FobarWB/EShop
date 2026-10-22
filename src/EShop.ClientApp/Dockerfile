# Build context: the repository root (docker build -f src/EShop.ClientApp/Dockerfile .), like the API images.

# ---- build: Node image, never shipped ----
FROM node:24-alpine AS build
WORKDIR /app

# Install first, from the lock file only: this layer is reused until a dependency changes.
COPY src/EShop.ClientApp/package.json src/EShop.ClientApp/package-lock.json ./
RUN npm ci

COPY src/EShop.ClientApp/ ./
RUN npm run build

# ---- runtime: nginx serves the static files and proxies /api ----
# The unprivileged variant runs as a non-root user and listens on 8080, like the API images.
FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY src/EShop.ClientApp/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/eshop.client-app/browser /usr/share/nginx/html
EXPOSE 8080
