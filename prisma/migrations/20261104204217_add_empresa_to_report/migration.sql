-- CreateEnum
CREATE TYPE "Empresa" AS ENUM ('CPY_NOS', 'COPAYAPU_NOS');

-- AlterTable
ALTER TABLE "ExpenseReport" ADD COLUMN     "empresa" "Empresa";
