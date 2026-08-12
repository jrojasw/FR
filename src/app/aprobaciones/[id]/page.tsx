import { notFound } from "next/navigation";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { ReportView } from "@/components/ReportView";
import { ReviewForm } from "@/components/ReviewForm";
import { PaymentCertificateForm } from "@/components/PaymentCertificateForm";

export default async function AprobacionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireRole("APROBADOR", "ADMIN");
  const { id } = await params;

  const report = await prisma.expenseReport.findFirst({
    where: { id, status: { not: "DRAFT" } },
    include: {
      items: true,
      attachments: true,
      reviewer: { select: { name: true, email: true } },
      paidBy: { select: { name: true, email: true } },
      user: { select: { name: true, email: true } },
    },
  });

  if (!report) notFound();

  const canManagePayment =
    session.role === "ADMIN" && (report.status === "APPROVED" || report.status === "PAID");

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">
        Rendición N° {report.correlativo} · {report.user.name ?? report.user.email}
      </h1>

      <div className="mt-6">
        <ReportView
          report={report}
          actions={
            <>
              {report.status === "SUBMITTED" && <ReviewForm reportId={report.id} />}
              {canManagePayment && (
                <PaymentCertificateForm
                  reportId={report.id}
                  initialFileName={report.paymentCertificateName}
                  alreadyPaid={report.status === "PAID"}
                />
              )}
            </>
          }
        />
      </div>
    </div>
  );
}
