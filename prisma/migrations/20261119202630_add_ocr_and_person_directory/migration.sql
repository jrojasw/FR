-- AlterTable
ALTER TABLE "Attachment" ADD COLUMN     "ocrGlosa" TEXT,
ADD COLUMN     "ocrMontoTotal" DECIMAL(12,2),
ADD COLUMN     "ocrNumeroDocumento" TEXT,
ADD COLUMN     "ocrProcessed" BOOLEAN NOT NULL DEFAULT false,
ADD COLUMN     "ocrProveedor" TEXT,
ADD COLUMN     "ocrTipoDocumento" "DocumentType";

-- CreateTable
CREATE TABLE "PersonDirectory" (
    "rut" TEXT NOT NULL,
    "nombre" TEXT,
    "apellido" TEXT,
    "segundoApellido" TEXT,
    "email" TEXT,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "PersonDirectory_pkey" PRIMARY KEY ("rut")
);
