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
5. Textos más largos que su columna (`FieldLimits`) se recortan antes de guardar, con
   una advertencia en el log: un producto con un campo enorme no hace fallar la tienda.
6. Cada ejecución queda registrada en `scrape_runs` (visible en `/tiendas`). Queda como
   **fallida** ("Con errores") si todas las búsquedas fallaron (404, bloqueo, respuesta
   que no es la esperada) o si ninguna devolvió productos; si falla solo alguna, la
   ejecución sigue y el log indica cuál.

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
| `Scrapers:Stores:<clave>:UsePlaywright` | Scrapers HTML: `true` = siempre navegador, `false` = solo HTTP, vacío = lo que defina la tienda (Sodimac: HTTP con respaldo en navegador) |
| `Scrapers:Stores:<clave>:SearchUrlTemplate` | Scrapers HTML: URL de búsqueda con `{query}` y `{page}`, para ajustarla sin recompilar |
| `Scrapers:Stores:<clave>:ApiBaseUrl` | Tiendas con `VtexScraperBase`: host de la API de catálogo, si no es el dominio de la tienda |
| `Scrapers:BrowserUserAgent` | User-Agent de Playwright; vacío = Chrome de escritorio con la versión real del Chromium instalado |
| `Scrapers:Stores:<clave>:DelayBetweenRequestsMs` | Pausa entre solicitudes a la tienda |
| `Scrapers:Stores:<clave>:MaxPagesPerQuery` | Tope de páginas por búsqueda |

> Las claves con guion (`demo-norte`) funcionan igual como variables de entorno en
> docker-compose, aunque `export` de bash no las acepte.

## Tiendas incluidas

| Clave | Tienda | Plataforma / técnica |
| --- | --- | --- |
| `easy` | Easy | JSON de Next.js por HTTP: la página `/search/{término}` trae en `__NEXT_DATA__` los productos (`pageProps.serverProductsResponse.productList`), el total (`recordsFiltered`) y el `buildId`; las páginas siguientes salen de `/_next/data/{buildId}/search/{término}.json?search={término}&page=N`. El `buildId` se lee en cada ejecución (y se relee si Easy publica una versión nueva). Si Easy bloquea HTTP se usa Chromium; si la página no trae ese JSON, se leen las respuestas que pide la página |
| `sodimac` | Sodimac | Búsqueda `/sodimac-cl/buscar?Ntt=…`; lee el JSON `__NEXT_DATA__` (`props.pageProps.results`). Pide primero por HTTP y, si la tienda lo bloquea o no entrega ese JSON, cambia a Chromium (Playwright). Ignora el precio con tarjeta CMR; el precio tachado queda como precio normal |
| `imperial` | Imperial | API de búsqueda guiada de Oracle Commerce: `/ccstore/v1/assembler/pages/Default/services/guidedsearch?Ntt={término}&searchType=simple&Nrpp=36` resuelve el término (p. ej. "taladro" → `N-1033696118`) y `/guidedsearch/_/N-…?Nrpp=36&No={offset}` entrega los productos (`sku.activePrice`, `sku.availabilityStatus`, `product.route`…) y `totalNumRecs`. HTTP con encabezados de navegador; si responde 403, la API se pide dentro de una sesión de Chromium. Una búsqueda `N-…` se usa directo como categoría |
| `demo-norte`, `demo-sur` | Ficticias | Datos generados para desarrollo |

> **Importante:** las URLs, el JSON y los selectores de Easy, Sodimac e Imperial se
> escribieron según la estructura documentada públicamente de cada plataforma, pero **no se
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
     `BuildPageUrl`; `DefaultFetchMode` elige HTTP, navegador o HTTP con respaldo en navegador;
   - listado armado por JavaScript con una API propia → heredar de
     `BrowserCaptureScraperBase` y definir `BuildSearchPageUrl`: se abre la búsqueda en
     Chromium y se leen las respuestas JSON que pide la página (ver `CapturedJsonProductParser`);
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

## Despliegue en Railway

La app lee `PORT` y `DATABASE_URL` (formato `postgres://…`) automáticamente, así que
en Railway no hace falta armar la cadena de conexión a mano.

1. En Railway: *New Project* → *Deploy from GitHub repo* → elegir este repositorio.
2. En el servicio creado → *Settings*: **Root Directory** = `PrecioHerramientas` y la
   rama que corresponda. Railway detecta el `Dockerfile`.
3. En el proyecto: *+ New* → *Database* → *PostgreSQL*.
4. En el servicio web → *Variables*:
   - `DATABASE_URL` = `${{Postgres.DATABASE_URL}}`
   - `Hangfire__DashboardPassword` = una clave larga
   - opcional, solo para probar con datos de ejemplo: `Scrapers__Stores__demo-norte__Enabled=true` y `Scrapers__Stores__demo-sur__Enabled=true` (bórrelas después)

   La imagen ya instala Chromium para Sodimac (`INSTALL_PLAYWRIGHT` es `true` por defecto).
5. *Settings* → *Networking* → *Generate Domain*. La web queda en `https://<nombre>.up.railway.app`.

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
- La imagen incluye Chromium y sus dependencias (para Sodimac), lo que suma
  ~400 MB. Para una imagen liviana sin navegador use `INSTALL_PLAYWRIGHT=false`
  en `.env` y deshabilite Sodimac.
- Datos persistentes: volúmenes `pgdata` (base de datos, que también guarda las
  claves de ASP.NET Core Data Protection) y `caddy_data` (certificados).

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
