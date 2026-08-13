#!/bin/sh
set -e

echo "Aplicando migraciones de la base de datos..."
# Recuperación puntual: un intento anterior de esta migración falló antes de
# corregirse el SQL, y Prisma la marca como fallida bloqueando nuevos deploys.
# Reintentar marcarla como revertida es inofensivo si ya no aplica (no-op).
node node_modules/prisma/build/index.js migrate resolve --rolled-back 20260813042913_remove_fondo_add_item_glosa 2>&1 || true
node node_modules/prisma/build/index.js migrate deploy

echo "Iniciando servidor..."
exec node server.js
