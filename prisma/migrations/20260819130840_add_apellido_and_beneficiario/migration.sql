-- AlterTable
-- "apellido" se agrega con un default temporal para no romper filas
-- existentes (rendiciones ya enviadas antes de este cambio); se retira el
-- default apenas se aplica para que los nuevos registros lo exijan.
ALTER TABLE "ExpenseReport" ADD COLUMN     "apellido" TEXT NOT NULL DEFAULT '',
ADD COLUMN     "beneficiarioApellido" TEXT,
ADD COLUMN     "beneficiarioNombre" TEXT,
ADD COLUMN     "beneficiarioRut" TEXT,
ADD COLUMN     "esParaOtraPersona" BOOLEAN NOT NULL DEFAULT false;

ALTER TABLE "ExpenseReport" ALTER COLUMN "apellido" DROP DEFAULT;
