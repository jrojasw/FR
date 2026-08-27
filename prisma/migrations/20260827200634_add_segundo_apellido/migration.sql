-- AlterTable
ALTER TABLE "ExpenseReport" ADD COLUMN     "beneficiarioSegundoApellido" TEXT,
ADD COLUMN     "segundoApellido" TEXT NOT NULL DEFAULT '';
