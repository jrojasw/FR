-- CreateEnum
CREATE TYPE "Role" AS ENUM ('SOLICITANTE', 'APROBADOR', 'ADMIN');

-- CreateEnum
CREATE TYPE "ReportStatus" AS ENUM ('DRAFT', 'SUBMITTED', 'APPROVED', 'REJECTED');

-- CreateEnum
CREATE TYPE "DocumentType" AS ENUM ('BOLETA', 'FACTURA', 'RECIBO');

-- CreateEnum
CREATE TYPE "AttachmentKind" AS ENUM ('PHOTO', 'DOCUMENT');

-- CreateTable
CREATE TABLE "User" (
    "id" TEXT NOT NULL,
    "name" TEXT,
    "cargo" TEXT,
    "email" TEXT NOT NULL,
    "role" "Role" NOT NULL DEFAULT 'SOLICITANTE',
    "active" BOOLEAN NOT NULL DEFAULT true,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "OtpCode" (
    "id" TEXT NOT NULL,
    "email" TEXT NOT NULL,
    "code" TEXT NOT NULL,
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "consumedAt" TIMESTAMP(3),
    "attempts" INTEGER NOT NULL DEFAULT 0,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "userId" TEXT,

    CONSTRAINT "OtpCode_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ExpenseReport" (
    "id" TEXT NOT NULL,
    "correlativo" SERIAL NOT NULL,
    "nombre" TEXT NOT NULL,
    "cargo" TEXT NOT NULL,
    "fecha" TIMESTAMP(3) NOT NULL,
    "fondoPorRendir" DECIMAL(12,2) NOT NULL,
    "glosa" TEXT NOT NULL,
    "totalRendido" DECIMAL(12,2) NOT NULL DEFAULT 0,
    "saldoPorRendir" DECIMAL(12,2) NOT NULL DEFAULT 0,
    "esReembolso" BOOLEAN NOT NULL DEFAULT false,
    "montoReembolso" DECIMAL(12,2) NOT NULL DEFAULT 0,
    "rut" TEXT,
    "signatureData" TEXT,
    "status" "ReportStatus" NOT NULL DEFAULT 'DRAFT',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "submittedAt" TIMESTAMP(3),
    "reviewedAt" TIMESTAMP(3),
    "reviewComment" TEXT,
    "userId" TEXT NOT NULL,
    "reviewerId" TEXT,

    CONSTRAINT "ExpenseReport_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ExpenseItem" (
    "id" TEXT NOT NULL,
    "proveedor" TEXT NOT NULL,
    "tipoDocumento" "DocumentType" NOT NULL,
    "numeroDocumento" TEXT NOT NULL,
    "montoTotal" DECIMAL(12,2) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "reportId" TEXT NOT NULL,

    CONSTRAINT "ExpenseItem_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Attachment" (
    "id" TEXT NOT NULL,
    "fileName" TEXT NOT NULL,
    "filePath" TEXT NOT NULL,
    "mimeType" TEXT NOT NULL,
    "size" INTEGER NOT NULL,
    "kind" "AttachmentKind" NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "reportId" TEXT NOT NULL,

    CONSTRAINT "Attachment_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "User_email_key" ON "User"("email");

-- CreateIndex
CREATE INDEX "OtpCode_email_idx" ON "OtpCode"("email");

-- CreateIndex
CREATE UNIQUE INDEX "ExpenseReport_correlativo_key" ON "ExpenseReport"("correlativo");

-- CreateIndex
CREATE INDEX "ExpenseReport_userId_idx" ON "ExpenseReport"("userId");

-- CreateIndex
CREATE INDEX "ExpenseReport_status_idx" ON "ExpenseReport"("status");

-- CreateIndex
CREATE INDEX "ExpenseItem_reportId_idx" ON "ExpenseItem"("reportId");

-- CreateIndex
CREATE INDEX "Attachment_reportId_idx" ON "Attachment"("reportId");

-- AddForeignKey
ALTER TABLE "OtpCode" ADD CONSTRAINT "OtpCode_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ExpenseReport" ADD CONSTRAINT "ExpenseReport_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ExpenseReport" ADD CONSTRAINT "ExpenseReport_reviewerId_fkey" FOREIGN KEY ("reviewerId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ExpenseItem" ADD CONSTRAINT "ExpenseItem_reportId_fkey" FOREIGN KEY ("reportId") REFERENCES "ExpenseReport"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Attachment" ADD CONSTRAINT "Attachment_reportId_fkey" FOREIGN KEY ("reportId") REFERENCES "ExpenseReport"("id") ON DELETE CASCADE ON UPDATE CASCADE;
