# Fondos a Rendir

Aplicación web para digitalizar el proceso de rendición de fondos fijos: el
colaborador ingresa con su correo (código de acceso de 4 dígitos, sin
contraseña), completa el encabezado y el detalle de documentos, adjunta
fotos/documentos de respaldo, firma con el dedo y envía la rendición al
aprobador. El aprobador revisa y aprueba o rechaza, y el administrador cuenta
con un registro exportable a Excel/CSV de todas las rendiciones.

## Stack

- Next.js 16 (App Router, Server Actions, Route Handlers)
- Prisma 7 + PostgreSQL
- Autenticación passwordless (correo + PIN de 4 dígitos) con sesión en cookie firmada (JWT/`jose`)
- Envío de correo con [Resend](https://resend.com) (opcional, con fallback a log en consola)
- Tailwind CSS

## Requisitos

- Node.js 20+
- PostgreSQL (local o remoto)

## Configuración

1. Instala dependencias:

   ```bash
   npm install
   ```

2. Copia `.env.example` a `.env` y completa los valores:

   ```bash
   cp .env.example .env
   ```

   | Variable | Descripción |
   | --- | --- |
   | `DATABASE_URL` | Cadena de conexión a Postgres |
   | `SESSION_SECRET` | Secreto para firmar la cookie de sesión (usa un valor largo y aleatorio) |
   | `RESEND_API_KEY` | API key de Resend para enviar correos reales. **Si se deja vacío**, el PIN de acceso se muestra en pantalla y los correos de notificación solo quedan registrados en el log del servidor — útil para probar el flujo completo sin credenciales |
   | `EMAIL_FROM` | Remitente de los correos |
   | `ADMIN_EMAIL` | Correo con rol Administrador (por defecto `jorge.rojas@copayapunos.cl`) |
   | `APPROVER_EMAIL` | Correo con rol Aprobador (por defecto `williams.arce@copayapunos.cl`) |
   | `PAYMENT_NOTICE_EMAILS` | Correos (separados por coma) que reciben el certificado de pago cuando el administrador lo envía |
   | `APP_URL` | URL pública de la app para los enlaces en los correos (opcional, se infiere del request si se deja vacío) |
   | `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET_NAME` | Credenciales de Cloudflare R2 para guardar fotos/documentos/certificados. **Recomendado en producción** — ver [sección R2](#almacenamiento-de-archivos-cloudflare-r2) |
   | `R2_ENDPOINT` | Opcional. Endpoint S3 exacto de tu bucket (Cloudflare → bucket → Settings → "S3 API"). Si lo defines, se usa tal cual en vez de armarlo desde `R2_ACCOUNT_ID` — útil si tu cuenta usa una jurisdicción distinta a la por defecto |
   | `ATTACHMENTS_DIR` | Si las variables `R2_*` están vacías, carpeta en disco local donde se guardan los adjuntos en su lugar (por defecto `./storage/attachments`, solo para desarrollo) |

   Cualquier otro correo que inicie sesión queda automáticamente como
   **Solicitante**; los roles Aprobador/Administrador son fijos según
   `APPROVER_EMAIL`/`ADMIN_EMAIL`.

3. Aplica las migraciones:

   ```bash
   npx prisma migrate deploy
   ```

4. Levanta el servidor de desarrollo:

   ```bash
   npm run dev
   ```

   Abre http://localhost:3000 — sin `RESEND_API_KEY` configurado, el código
   de acceso de 4 dígitos aparece directamente en la pantalla de
   verificación para poder probar el flujo sin enviar correos reales.

## Flujo de la aplicación

- **Solicitante**: crea una rendición (correlativo automático), completa
  nombre/cargo/fecha, agrega el detalle de documentos (glosa, proveedor,
  tipo de documento, N° documento, monto — con cálculo en vivo del total
  rendido y el reembolso correspondiente), adjunta hasta 25
  fotos/documentos, firma con el dedo/mouse e ingresa su RUT para enviar.
  El aprobador recibe un correo de notificación.
- **Aprobador**: ve la lista de rendiciones pendientes y puede aprobar o
  rechazar con un comentario. El solicitante recibe un correo con la
  decisión, y al aprobar también se avisa al administrador para que suba
  el comprobante de pago.
- **Administrador**: además de aprobar, en cada rendición **Aprobada** sube
  el comprobante de la transferencia del banco (PDF o imagen) y presiona
  "Enviar certificado y marcar como pagada" — esto envía el comprobante por
  correo a `PAYMENT_NOTICE_EMAILS`, avisa al solicitante que fue pagada, y
  cambia el estado a **Pagada**, cerrando el ciclo. También tiene acceso a
  `/admin/registro`, un registro de todas las rendiciones agrupado por
  usuario, año, mes y día, exportable a Excel (`.xlsx`) o CSV, y con
  opción de enviarlo por correo.

## Almacenamiento de archivos (Cloudflare R2)

Las fotos, documentos y certificados de pago se guardan en
[Cloudflare R2](https://developers.cloudflare.com/r2/) cuando las variables
`R2_*` están configuradas — así los archivos no dependen del disco del
servidor donde corre la app (sobreviven aunque se reinicie el contenedor,
se cambie de hosting, etc.). Sin esas variables, cae a disco local
(`ATTACHMENTS_DIR`), solo recomendado para desarrollo.

Para crear el bucket y las credenciales:

1. Crea una cuenta gratis en [dash.cloudflare.com](https://dash.cloudflare.com).
2. En el menú lateral, ve a **R2 Object Storage** → **Create bucket**. Ponle un nombre (ej. `fondos-a-rendir`) y crea el bucket. El plan gratis incluye 10 GB de almacenamiento al mes.
3. Ve a **R2 → Manage API tokens** (o "Account API tokens") → **Create API token**. Dale permisos de **Object Read & Write**, y limita el acceso al bucket que creaste si te lo permite.
4. Copia los 3 valores que te muestra: **Access Key ID**, **Secret Access Key**, y el **Account ID** de Cloudflare (aparece en la URL del dashboard o en la barra lateral de la cuenta).
5. Completa las variables de entorno:
   - `R2_ACCOUNT_ID`
   - `R2_ACCESS_KEY_ID`
   - `R2_SECRET_ACCESS_KEY`
   - `R2_BUCKET_NAME` (el nombre que le pusiste al bucket)

   Si al subir un archivo ves un error de conexión/TLS, en vez de
   `R2_ACCOUNT_ID` define `R2_ENDPOINT` con el valor **exacto** que
   Cloudflare muestra en tu bucket → **Settings** → **"S3 API"** (algo como
   `https://<id>.r2.cloudflarestorage.com`, o `https://<id>.eu.r2.cloudflarestorage.com`
   si tu cuenta usa la jurisdicción UE). Copiarlo tal cual evita errores por
   un Account ID mal copiado o por una jurisdicción no estándar.

Con esas variables configuradas (en Railway: pestaña Variables del
servicio), los adjuntos nuevos se guardan automáticamente en R2 — no hace
falta ningún otro cambio en el código ni en el volumen de disco.

## Notas de despliegue

- El límite de tamaño de body para Server Actions se configuró en 8 MB
  (`next.config.ts`) para la acción de envío final (incluye la firma en
  base64). La subida de fotos/documentos usa un Route Handler dedicado
  (`/api/rendiciones/[id]/adjuntos`), sin ese límite.
- Un hosting compartido tradicional (cPanel/Plesk para WordPress/PHP) **no
  sirve**: esta app necesita Node.js corriendo como proceso persistente y
  PostgreSQL. Los archivos subidos ya no dependen de disco persistente si
  se configura Cloudflare R2 (ver sección anterior).

## Desplegar en Railway (recomendado)

El repo incluye un `Dockerfile` listo para producción (build en dos etapas,
`output: "standalone"` de Next.js, y migraciones automáticas al arrancar).
Railway lo detecta solo.

1. Crea una cuenta en [railway.app](https://railway.app) (puedes entrar con tu cuenta de GitHub).
2. **New Project → Deploy from GitHub repo** → selecciona este repositorio y la rama `claude/fondos-a-rendir-app-cldjg7` (o `main` una vez que se mezcle el PR). Railway detecta el `Dockerfile` automáticamente.
3. En el mismo proyecto, **+ New → Database → Add PostgreSQL**. Railway crea la base y una variable `DATABASE_URL` interna.
4. En el servicio de la app, pestaña **Variables**, agrega:
   - `DATABASE_URL` → referencia la del servicio Postgres (Railway te deja enlazarla con `${{Postgres.DATABASE_URL}}`)
   - `SESSION_SECRET` → un valor largo y aleatorio
   - `ADMIN_EMAIL`, `APPROVER_EMAIL`, `PAYMENT_NOTICE_EMAILS` → como en `.env.example`
   - `RESEND_API_KEY` y `EMAIL_FROM` → cuando tengas cuenta de Resend (mientras tanto puedes dejarlas vacías: el PIN se mostrará en pantalla)
   - `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET_NAME` (y opcionalmente `R2_ENDPOINT`) → credenciales de Cloudflare R2 (ver sección [Almacenamiento de archivos](#almacenamiento-de-archivos-cloudflare-r2))
5. Con R2 configurado **no hace falta** agregar un volumen — los adjuntos ya no se guardan en el disco del contenedor. (Si prefieres no usar R2, en **Settings → Volumes** puedes montar un volumen en `/app/storage/attachments` y dejar `ATTACHMENTS_DIR` apuntando ahí en vez de las variables `R2_*`.)
6. En **Settings → Networking**, genera un dominio público (`*.up.railway.app` gratis, o conecta tu propio dominio).
7. Copia esa URL y agrégala como variable `APP_URL` (ej. `https://tu-app.up.railway.app`) para que los enlaces de los correos apunten bien.
8. Despliega. Railway construye la imagen, corre `prisma migrate deploy` automáticamente al iniciar el contenedor (ver `docker-entrypoint.sh`), y levanta la app.

**Render** funciona de forma muy similar (Web Service desde el repo con Dockerfile + PostgreSQL managed + Persistent Disk).

### Probar el Dockerfile en tu computador (opcional)

```bash
cp .env.example .env   # completa los valores
docker compose up --build
```

Esto levanta Postgres + la app en `http://localhost:3000`, igual que en producción.
