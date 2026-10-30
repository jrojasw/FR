# PrecioHerramientas

Comparador de precios de herramientas para Chile (estilo knasta.cl / solotodo.cl).
Recolecta precios desde tiendas online, agrupa el mismo producto entre tiendas,
guarda el historial de precios y lo muestra en un sitio con buscador y gráficos.

## Stack

- .NET 8 / C# 12
- ASP.NET Core Razor Pages + Bootstrap 5 (servido localmente desde `wwwroot/lib`)
- EF Core 8 + PostgreSQL (Npgsql, migraciones, nombres `snake_case`, índice trigram para búsqueda)
- Hangfire + Hangfire.PostgreSql para los trabajos programados, con dashboard protegido
- HttpClient (con reintentos y circuit breaker) + AngleSharp para scraping; Playwright for .NET listo para tiendas que pintan precios con JavaScript
- Chart.js para el historial de precios
- Docker + docker-compose (PostgreSQL, web y Caddy opcional para HTTPS)

## Estructura

```
PrecioHerramientas.sln
├── src/
│   ├── PrecioHerramientas.Core            Entidades, interfaces y lógica de negocio
│   │   ├── Entities/                      Store, Product, Offer, PricePoint, Brand, Category, ScrapeRun
│   │   ├── Matching/                      Marcas conocidas, extracción de modelo, identidad del producto
│   │   ├── Services/PriceIngestionService Agrupa, actualiza ofertas y registra historial
│   │   ├── Text/TextNormalizer            Precios CLP, EAN/GTIN, slugs, texto de búsqueda
│   │   └── Queries/                       Contratos de lectura para el sitio
│   ├── PrecioHerramientas.Infrastructure  AppDbContext, configuraciones, repositorios, consultas, migraciones
│   ├── PrecioHerramientas.Scrapers        IStoreScraper, bases por plataforma y una clase por tienda
│   │   ├── Platforms/                     VtexScraperBase, HtmlListingScraperBase
│   │   ├── Http/                          HttpPageFetcher, PlaywrightPageFetcher
│   │   ├── Parsing/                       Selectores CSS y extractor JSON-LD (schema.org)
│   │   ├── Stores/                        EasyScraper, SodimacScraper, ImperialScraper, DemoStoreScraper
│   │   └── Jobs/ScrapeJob                 Lo que ejecuta Hangfire por tienda
│   └── PrecioHerramientas.Web             Sitio público, API JSON, Hangfire
└── tests/PrecioHerramientas.Tests         xUnit: normalización, matching, parsers e ingesta
```

## Cómo funciona

1. **Recolección.** Hangfire ejecuta `ScrapeJob.RunAsync("<tienda>")` según el cron de
   cada tienda. El scraper entrega productos como `IAsyncEnumerable<ScrapedProduct>`.
2. **Ingesta por lotes** (`PriceIngestionService`). Cada producto se busca por
   `(tienda, id externo)`:
   - si la oferta existe, se actualiza; si cambió precio, precio normal o stock, se
     agrega un `PricePoint` (el historial guarda **solo cambios**);
   - si es nueva, se asocia a un producto canónico.
3. **Agrupamiento** del mismo producto entre tiendas, en este orden:
   1. **EAN/GTIN** normalizado a 13 dígitos y con dígito verificador válido
      (muchas tiendas ponen códigos internos en ese campo; se descartan).
   2. **Marca + modelo**: la marca se unifica (`BLACK+DECKER`, `Black & Decker` →
      `Black+Decker`) y el modelo se compacta (`DCD-778 D2` → `DCD778D2`). Si la
      tienda no informa el modelo, se extrae del título ignorando medidas (`20V`,
      `650W`, `13mm`, `1/2"`…).
   3. **Modelo dentro del título**: si no hubo coincidencia, se busca un producto de
      la misma marca cuyo modelo (≥ 5 caracteres) aparezca en el título.
   4. Si nada coincide, se crea un producto nuevo.
4. **Agotados.** Al terminar, las ofertas de la tienda que no aparecieron se marcan
   sin stock, salvo que la ejecución haya traído menos del 50 % de lo esperado
   (`Ingestion:MinCoverageToMarkUnavailable`), para no "vaciar" una tienda cuando el
   scraper se rompe a medias.
5. Cada ejecución queda registrada en `scrape_runs` (visible en `/tiendas`).

## Desarrollo local

Requisitos: .NET 8 SDK y PostgreSQL 14+ (o `docker compose up db`).

```bash
# Base de datos (credenciales por defecto de appsettings.json: postgres/postgres)
docker run -d --name ph-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=precioherramientas -p 5432:5432 postgres:16-alpine

dotnet tool restore
dotnet run --project src/PrecioHerramientas.Web      # http://localhost:5007
dotnet test
```

En `Development` están habilitadas dos tiendas ficticias (`demo-norte` y
`demo-sur`) que generan datos sin salir a internet, y Easy está deshabilitada.
Para no esperar al cron, abra `http://localhost:5007/hangfire` (usuario `admin`,
clave `dev`) → *Recurring jobs* → *Trigger now*.

Las migraciones se aplican solas al iniciar (`Database:MigrateOnStartup`). Para
crear una nueva tras cambiar el modelo:

```bash
dotnet ef migrations add NombreDelCambio \
  -p src/PrecioHerramientas.Infrastructure -s src/PrecioHerramientas.Infrastructure \
  -o Persistence/Migrations
```

## Configuración

Todo se puede definir en `appsettings.json` o por variables de entorno
(`Seccion__Clave`).

| Clave | Descripción |
| --- | --- |
| `ConnectionStrings:Default` | Cadena de conexión PostgreSQL (la usan EF Core y Hangfire) |
| `Hangfire:DashboardUser` / `DashboardPassword` | Credenciales del dashboard. **Sin clave, el dashboard queda bloqueado** |
| `Hangfire:WorkerCount` | Trabajos simultáneos (por defecto 2) |
| `Hangfire:TimeZone` | Zona de los cron (por defecto `America/Santiago`) |
| `Hangfire:RunServer` | `false` para levantar solo la web, sin procesar trabajos |
| `Scrapers:UserAgent` | User-Agent de las solicitudes; conviene identificar el bot con una URL de contacto |
| `Scrapers:Stores:<clave>:Enabled` | Habilita la tienda y crea su trabajo recurrente |
| `Scrapers:Stores:<clave>:Cron` | Frecuencia (formato cron de 5 campos) |
| `Scrapers:Stores:<clave>:Queries` | Búsquedas o rutas de categoría a recorrer (vacío = valores por defecto de la clase) |
| `Scrapers:Stores:<clave>:Selectors` | Sobrescribe selectores CSS de scrapers HTML sin recompilar (`Card`, `Title`, `Link`, `Price`, `ListPrice`, `Image`, `Brand`, `SkuAttribute`, `OutOfStock`, `WaitFor`) |
| `Scrapers:Stores:<clave>:UsePlaywright` | Fuerza (o desactiva) el navegador en scrapers HTML |
| `Scrapers:Stores:<clave>:DelayBetweenRequestsMs` | Pausa entre solicitudes a la tienda |
| `Scrapers:Stores:<clave>:MaxPagesPerQuery` | Tope de páginas por búsqueda |

> Las claves con guion (`demo-norte`) funcionan igual como variables de entorno en
> docker-compose, aunque `export` de bash no las acepte.

## Tiendas incluidas

| Clave | Tienda | Plataforma / técnica |
| --- | --- | --- |
| `easy` | Easy | API pública de catálogo VTEX (`/api/catalog_system/pub/products/search`), JSON con precio, precio normal, stock, marca, referencia y EAN |
| `sodimac` | Sodimac | Playwright + JSON `__NEXT_DATA__` de la plataforma Falabella, con selectores como respaldo. Ignora el precio con tarjeta CMR |
| `imperial` | Imperial | HTML con AngleSharp mediante selectores configurables; si no encuentra la grilla, lee JSON-LD |
| `demo-norte`, `demo-sur` | Ficticias | Datos generados para desarrollo |

> **Importante:** las URLs, el JSON y los selectores de Easy, Sodimac e Imperial se
> escribieron según la estructura conocida de cada plataforma, pero **no se
> pudieron verificar contra los sitios reales** porque el entorno donde se
> desarrolló no tenía acceso a esas tiendas. Antes de habilitarlas en producción,
> ejecute cada una manualmente desde el dashboard y revise el log y `/tiendas`.
> Si una tienda cambió su sitio, normalmente basta con ajustar `Queries` o
> `Selectors` en la configuración. Los parsers tienen tests con fixtures en
> `tests/PrecioHerramientas.Tests/Fixtures`.

### Agregar una tienda

1. Crear `src/PrecioHerramientas.Scrapers/Stores/MiTiendaScraper.cs`:
   - tienda VTEX → heredar de `VtexScraperBase` y definir `Store` y `DefaultQueries`;
   - HTML → heredar de `HtmlListingScraperBase`, definir `DefaultSelectors` y
     `BuildPageUrl`; con `UsesBrowserByDefault => true` usa Playwright;
   - otro caso (API propia, sitemap, etc.) → implementar `IStoreScraper` directamente.
2. Registrarla en `Scrapers/ServiceCollectionExtensions.cs`.
3. Habilitarla en `appsettings.json` → `Scrapers:Stores:<clave>`.
4. Agregar un test con un fixture HTML/JSON real guardado de la tienda.

### Buenas prácticas de scraping

- Revise `robots.txt` y los términos de uso de cada tienda; prefiera sus APIs o feeds
  públicos cuando existan.
- Mantenga pausas entre solicitudes (`DelayBetweenRequestsMs`) y frecuencias
  razonables; el cliente HTTP ya reintenta con espera exponencial ante 429/5xx.
- Identifique el bot en el `UserAgent` con una URL o correo de contacto.

## Sitio web

| Ruta | Contenido |
| --- | --- |
| `/` | Buscador, categorías, bajas de precio de los últimos 7 días y productos en más tiendas |
| `/buscar?q=&categoria=&marca=&tienda=&min=&max=&orden=&pagina=` | Resultados con filtros y orden (relevancia, precio, nombre, recientes) |
| `/producto/{slug}` | Ofertas por tienda, mejor precio, mínimo histórico y gráfico (30/90/365 días) |
| `/tiendas` | Tiendas, productos con stock y estado de la última recolección |
| `/api/productos?q=` | Búsqueda en JSON |
| `/api/productos/{id}/historial?dias=90` | Series de precio por tienda (lo usa el gráfico) |
| `/health` | Health check (incluye la base de datos) |
| `/hangfire` | Dashboard de trabajos (HTTP Basic) |

## Despliegue en un VPS Linux con Docker

```bash
git clone <repo> && cd <repo>/PrecioHerramientas
cp .env.example .env        # completar POSTGRES_PASSWORD, HANGFIRE_PASSWORD, DOMAIN...
docker compose up -d --build
```

- La web queda en `127.0.0.1:8080` (no expuesta a internet).
- Para publicarla con HTTPS automático (Let's Encrypt) apunte el DNS de `DOMAIN` al
  VPS y levante también Caddy: `docker compose --profile proxy up -d`.
  Si ya usa nginx, haga `proxy_pass http://127.0.0.1:8080;` y envíe
  `X-Forwarded-For` / `X-Forwarded-Proto` (la app los respeta).
- El dashboard `/hangfire` usa HTTP Basic: úselo **solo detrás de HTTPS**.
- Para tiendas que requieren navegador (Sodimac), construya con
  `INSTALL_PLAYWRIGHT=true` en `.env`; la imagen instala Chromium y sus dependencias
  (~400 MB adicionales).
- Datos persistentes: volúmenes `pgdata` (base de datos), `dpkeys` (claves de
  ASP.NET Core) y `caddy_data` (certificados).

Respaldo de la base:

```bash
docker compose exec db pg_dump -U precio precioherramientas | gzip > respaldo-$(date +%F).sql.gz
```

## Tests

```bash
dotnet test
```

Cubren el parseo de precios chilenos y EAN, la unificación de marcas, la
extracción de modelos, los scrapers VTEX/HTML/JSON-LD/Sodimac con fixtures y la
ingesta completa (agrupamiento por EAN y modelo, historial, agotados y
descartes) sobre EF Core InMemory.
