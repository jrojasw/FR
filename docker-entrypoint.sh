#!/bin/sh
set -e

echo "Aplicando migraciones de la base de datos..."
node node_modules/prisma/build/index.js migrate deploy

echo "Iniciando servidor..."
exec node server.js
