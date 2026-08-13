/*
  Warnings:

  - You are about to drop the column `esReembolso` on the `ExpenseReport` table. All the data in the column will be lost.
  - You are about to drop the column `fondoPorRendir` on the `ExpenseReport` table. All the data in the column will be lost.
  - You are about to drop the column `glosa` on the `ExpenseReport` table. All the data in the column will be lost.
  - You are about to drop the column `saldoPorRendir` on the `ExpenseReport` table. All the data in the column will be lost.
  - Added the required column `glosa` to the `ExpenseItem` table without a default value. This is not possible if the table is not empty.

*/
-- AlterTable
ALTER TABLE "ExpenseItem" ADD COLUMN     "glosa" TEXT NOT NULL;

-- AlterTable
ALTER TABLE "ExpenseReport" DROP COLUMN "esReembolso",
DROP COLUMN "fondoPorRendir",
DROP COLUMN "glosa",
DROP COLUMN "saldoPorRendir";
