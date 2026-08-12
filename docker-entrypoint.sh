#!/bin/sh
set -e

echo "Aplicando migraciones de la base de datos..."
npx prisma migrate deploy

echo "Iniciando servidor..."
exec node server.js
