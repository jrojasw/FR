-- CreateTable
CREATE TABLE "PaymentCertificate" (
    "id" TEXT NOT NULL,
    "fileName" TEXT NOT NULL,
    "filePath" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "reportId" TEXT NOT NULL,

    CONSTRAINT "PaymentCertificate_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "PaymentCertificate_reportId_idx" ON "PaymentCertificate"("reportId");

-- AddForeignKey
ALTER TABLE "PaymentCertificate" ADD CONSTRAINT "PaymentCertificate_reportId_fkey" FOREIGN KEY ("reportId") REFERENCES "ExpenseReport"("id") ON DELETE CASCADE ON UPDATE CASCADE;
