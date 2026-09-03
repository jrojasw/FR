-- CreateEnum
CREATE TYPE "Role" AS ENUM ('SOLICITANTE', 'ADMIN');

-- CreateEnum
CREATE TYPE "TipoRegistro" AS ENUM ('TURNO_DOMINGO', 'HORAS_EXTRA');

-- CreateEnum
CREATE TYPE "OvertimeStatus" AS ENUM ('PENDIENTE', 'APROBADA', 'RECHAZADA');

-- CreateTable
CREATE TABLE "User" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "role" "Role" NOT NULL DEFAULT 'SOLICITANTE',
    "active" BOOLEAN NOT NULL DEFAULT true,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "pinHash" TEXT,
    "email" TEXT,
    "passwordHash" TEXT,

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "OvertimeEntry" (
    "id" TEXT NOT NULL,
    "correlativo" SERIAL NOT NULL,
    "tipo" "TipoRegistro" NOT NULL,
    "fecha" TIMESTAMP(3) NOT NULL,
    "horaInicio" TIMESTAMP(3),
    "horaFin" TIMESTAMP(3),
    "horas" DECIMAL(5,2),
    "motivo" TEXT,
    "status" "OvertimeStatus" NOT NULL DEFAULT 'PENDIENTE',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "reviewedAt" TIMESTAMP(3),
    "reviewComment" TEXT,
    "validadoReloj" BOOLEAN NOT NULL DEFAULT false,
    "userId" TEXT NOT NULL,
    "reviewerId" TEXT,

    CONSTRAINT "OvertimeEntry_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "User_email_key" ON "User"("email");

-- CreateIndex
CREATE UNIQUE INDEX "OvertimeEntry_correlativo_key" ON "OvertimeEntry"("correlativo");

-- CreateIndex
CREATE INDEX "OvertimeEntry_userId_idx" ON "OvertimeEntry"("userId");

-- CreateIndex
CREATE INDEX "OvertimeEntry_status_idx" ON "OvertimeEntry"("status");

-- AddForeignKey
ALTER TABLE "OvertimeEntry" ADD CONSTRAINT "OvertimeEntry_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "OvertimeEntry" ADD CONSTRAINT "OvertimeEntry_reviewerId_fkey" FOREIGN KEY ("reviewerId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;
