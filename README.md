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
   | `APP_URL` | URL pública de la app para los enlaces en los correos (opcional, se infiere del request si se deja vacío) |
   | `ATTACHMENTS_DIR` | Carpeta donde se guardan las fotos/documentos adjuntos (por defecto `./storage/attachments`) |

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
  nombre/cargo/fecha/fondo por rendir/glosa, agrega el detalle
  (proveedor, tipo de documento, N° documento, monto — con cálculo en vivo
  de total rendido, saldo por rendir y reembolso), adjunta hasta 25
  fotos/documentos, firma con el dedo/mouse e ingresa su RUT para enviar.
  El aprobador recibe un correo de notificación.
- **Aprobador**: ve la lista de rendiciones pendientes y puede aprobar o
  rechazar con un comentario. El solicitante recibe un correo con la
  decisión.
- **Administrador**: además de aprobar, tiene acceso a `/admin/registro`,
  un registro de todas las rendiciones agrupado por usuario, año, mes y
  día, exportable a Excel (`.xlsx`) o CSV, y con opción de enviarlo por
  correo.

## Notas de despliegue

- Las fotos/documentos se guardan en disco (`ATTACHMENTS_DIR`). Esto
  funciona bien en un servidor propio o contenedor con disco persistente
  (Docker/VPS). Si se despliega en una plataforma serverless sin disco
  persistente (Vercel, etc.), hay que migrar el almacenamiento a un
  servicio externo (S3, Cloudflare R2, etc.).
- El límite de tamaño de body para Server Actions se configuró en 8 MB
  (`next.config.ts`) para la acción de envío final (incluye la firma en
  base64). La subida de fotos/documentos usa un Route Handler dedicado
  (`/api/rendiciones/[id]/adjuntos`), sin ese límite.
