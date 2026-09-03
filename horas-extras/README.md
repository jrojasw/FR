# Horas Extra

Aplicación web para registrar y aprobar horas extra y turnos domingo de las
colaboradoras de una casa. Cada registro se valida cruzándolo contra el
reloj de marcación biométrica físico (la huella se marca en ese aparato, no
en esta app — ver [Validación biométrica](#validación-biométrica) más
abajo).

## Personas y roles

- **Colaboradoras** (rol `SOLICITANTE`): ingresan con un **PIN fijo** de 4-6
  dígitos que les asigna la administradora (sin correo ni SMS). Registran
  sus horas extra y turnos domingo.
- **Administradora** (rol `ADMIN`): ingresa con **correo y clave**. Revisa
  cada registro contra el reloj biométrico, lo aprueba o rechaza, gestiona a
  las colaboradoras (crear, resetear PIN, desactivar) y descarga el registro
  mensual en Excel/CSV con los totales por persona.

## Tipos de registro

- **Turno domingo (aseo)**: una unidad completa (sin horario), para cuando
  una colaboradora hace de aseadora un domingo.
- **Horas extra semana**: fecha + hora de inicio/término (con motivo), las
  horas se calculan automáticamente.

Ambos quedan en estado **Pendiente** hasta que la administradora los
aprueba o rechaza. El sistema no calcula dinero: solo totaliza cantidades
(N° de turnos domingo aprobados y suma de horas extra aprobadas) por
colaboradora y por mes.

## Validación biométrica

Esta app **no controla hardware de huella directamente** (un navegador no
puede hacerlo). El proceso es:

1. Cada colaboradora marca entrada/salida en un reloj biométrico físico
   (aparato standalone, ej. ZKTeco/Anviz) cuando hace un turno extra o
   domingo.
2. Registra el turno/horas en esta app.
3. La administradora, al revisar, compara el registro contra el reporte del
   reloj (la mayoría exporta a Excel o tiene pantalla local) y solo entonces
   marca el check "Confirmo que revisé el reloj biométrico" antes de
   aprobar.

## Stack

- Next.js 16 (App Router, Server Actions, Route Handlers)
- Prisma 7 + PostgreSQL
- Sesión en cookie firmada (JWT/`jose`); PIN y clave con hash `bcryptjs`
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
   | `ADMIN_EMAIL` / `ADMIN_PASSWORD` / `ADMIN_NAME` | Credenciales de la cuenta de Administradora. Se crean/actualizan automáticamente cada vez que arranca el servidor (ver `src/instrumentation.ts`) |

3. Aplica las migraciones:

   ```bash
   npx prisma migrate deploy
   ```

4. Levanta el servidor de desarrollo:

   ```bash
   npm run dev
   ```

   Abre http://localhost:3000 — la administradora entra con el correo/clave
   de `.env`, luego desde **Colaboradoras** crea a cada una y les asigna su
   PIN.

## Flujo de la aplicación

- **Colaboradora**: entra eligiendo su nombre e ingresando su PIN, registra
  un turno domingo o sus horas extra de la semana (con motivo), y puede
  editar o eliminar sus registros mientras sigan **Pendientes**.
- **Administradora**: entra con correo/clave, revisa **Aprobaciones**
  pendientes cruzando cada registro contra el reloj biométrico, aprueba o
  rechaza con comentario, y desde **Registro** ve los registros agrupados
  por año/mes/día junto con los totales del mes por colaboradora,
  exportables a Excel (con hoja de totales) o CSV.

## Notas de despliegue

Un hosting compartido tradicional (cPanel/Plesk) **no sirve**: esta app
necesita Node.js corriendo como proceso persistente y PostgreSQL.

## Desplegar en Railway (recomendado)

El repo incluye un `Dockerfile` listo para producción (build en dos etapas,
`output: "standalone"` de Next.js, migraciones y creación de la cuenta de
administradora automáticas al arrancar). Railway lo detecta solo.

1. Crea una cuenta en [railway.app](https://railway.app).
2. **New Project → Deploy from GitHub repo** → selecciona este repositorio,
   con **Root Directory** apuntando a `horas-extras/` (es un proyecto
   independiente dentro del monorepo).
3. En el mismo proyecto, **+ New → Database → Add PostgreSQL**.
4. En el servicio de la app, pestaña **Variables**, agrega:
   - `DATABASE_URL` → referencia la del servicio Postgres (`${{Postgres.DATABASE_URL}}`)
   - `SESSION_SECRET` → un valor largo y aleatorio
   - `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `ADMIN_NAME`
5. En **Settings → Networking**, genera un dominio público.
6. Despliega. Railway construye la imagen, corre las migraciones al iniciar
   el contenedor (`docker-entrypoint.sh`) y luego el propio servidor crea/
   actualiza la cuenta de administradora al arrancar (`src/instrumentation.ts`).

### Probar el Dockerfile en tu computador (opcional)

```bash
cp .env.example .env   # completa los valores
docker compose up --build
```

Esto levanta Postgres + la app en `http://localhost:3000`, igual que en
producción.
