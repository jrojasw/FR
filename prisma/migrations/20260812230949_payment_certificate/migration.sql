-- AlterEnum
ALTER TYPE "ReportStatus" ADD VALUE 'PAID';

-- AlterTable
ALTER TABLE "ExpenseReport" ADD COLUMN     "paidAt" TIMESTAMP(3),
ADD COLUMN     "paidById" TEXT,
ADD COLUMN     "paymentCertificateName" TEXT,
ADD COLUMN     "paymentCertificatePath" TEXT;

-- AddForeignKey
ALTER TABLE "ExpenseReport" ADD CONSTRAINT "ExpenseReport_paidById_fkey" FOREIGN KEY ("paidById") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;
