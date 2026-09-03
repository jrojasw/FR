#!/bin/sh
set -e

echo "Aplicando migraciones de la base de datos..."
node node_modules/prisma/build/index.js migrate deploy

echo "Iniciando servidor..."
# La cuenta de administradora se crea/actualiza automáticamente al arrancar
# (ver src/instrumentation.ts + src/lib/bootstrap-admin.ts).
exec node server.js
