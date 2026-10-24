-- CreateTable
CREATE TABLE "DocumentDuplicateAcknowledgment" (
    "id" TEXT NOT NULL,
    "reportId" TEXT NOT NULL,
    "tipoDocumento" "DocumentType" NOT NULL,
    "numeroDocumento" TEXT NOT NULL,
    "matchedReportId" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "DocumentDuplicateAcknowledgment_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "DocumentDuplicateAcknowledgment_reportId_idx" ON "DocumentDuplicateAcknowledgment"("reportId");

-- CreateIndex
CREATE INDEX "DocumentDuplicateAcknowledgment_numeroDocumento_idx" ON "DocumentDuplicateAcknowledgment"("numeroDocumento");

-- CreateIndex
CREATE INDEX "ExpenseItem_numeroDocumento_idx" ON "ExpenseItem"("numeroDocumento");
