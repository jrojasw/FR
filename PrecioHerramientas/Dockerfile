# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaurar primero solo con los .csproj aprovecha la caché de capas.
COPY Directory.Build.props PrecioHerramientas.sln ./
COPY src/PrecioHerramientas.Core/PrecioHerramientas.Core.csproj src/PrecioHerramientas.Core/
COPY src/PrecioHerramientas.Infrastructure/PrecioHerramientas.Infrastructure.csproj src/PrecioHerramientas.Infrastructure/
COPY src/PrecioHerramientas.Scrapers/PrecioHerramientas.Scrapers.csproj src/PrecioHerramientas.Scrapers/
COPY src/PrecioHerramientas.Web/PrecioHerramientas.Web.csproj src/PrecioHerramientas.Web/
RUN dotnet restore src/PrecioHerramientas.Web/PrecioHerramientas.Web.csproj

COPY src/ src/
RUN dotnet publish src/PrecioHerramientas.Web/PrecioHerramientas.Web.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# true = instala Chromium y sus dependencias para los scrapers con Playwright (~400 MB más).
ARG INSTALL_PLAYWRIGHT=false

# La imagen base ya escucha en el puerto 8080 (ASPNETCORE_HTTP_PORTS).
ENV ASPNETCORE_ENVIRONMENT=Production \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    DataProtection__KeysPath=/app/keys

WORKDIR /app
COPY --from=build /app/publish .

# El paquete Microsoft.Playwright trae su propio Node y CLI en .playwright/.
RUN if [ "$INSTALL_PLAYWRIGHT" = "true" ]; then \
        NODE="$(ls -d ./.playwright/node/linux-*/ | head -n 1)node" \
        && "$NODE" ./.playwright/package/cli.js install --with-deps chromium \
        && rm -rf /var/lib/apt/lists/* \
        && chmod -R a+rX /ms-playwright; \
    fi \
    && mkdir -p /app/keys && chown "$APP_UID" /app/keys

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PrecioHerramientas.Web.dll"]
