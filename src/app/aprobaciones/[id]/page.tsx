import Link from "next/link";
import { notFound } from "next/navigation";
import { requireRoleOrRegistryViewer } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { ReportView } from "@/components/ReportView";
import { ReviewForm } from "@/components/ReviewForm";
import { PaymentCertificateForm } from "@/components/PaymentCertificateForm";

export default async function AprobacionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireRoleOrRegistryViewer("APROBADOR", "ADMIN");
  const canReview = session.role === "APROBADOR" || session.role === "ADMIN";
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
              {session.role === "ADMIN" && (
                <div className="flex items-center justify-between rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
                  <div>
                    <h2 className="text-lg font-semibold text-slate-900">Corregir datos</h2>
                    <p className="mt-1 text-sm text-slate-500">
                      Edita cualquier dato de esta rendición sin cambiar su estado.
                    </p>
                  </div>
                  <Link
                    href={`/aprobaciones/${report.id}/editar`}
                    className="shrink-0 rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
                  >
                    ✏️ Editar
                  </Link>
                </div>
              )}
              {canReview && report.status === "SUBMITTED" && <ReviewForm reportId={report.id} />}
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
