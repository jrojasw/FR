import { notFound } from "next/navigation";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { ReportView } from "@/components/ReportView";
import { ReviewForm } from "@/components/ReviewForm";

export default async function AprobacionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireRole("APROBADOR", "ADMIN");
  const { id } = await params;

  const report = await prisma.expenseReport.findFirst({
    where: { id, status: { not: "DRAFT" } },
    include: {
      items: true,
      attachments: true,
      reviewer: { select: { name: true, email: true } },
      user: { select: { name: true, email: true } },
    },
  });

  if (!report) notFound();

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">
        Rendición N° {report.correlativo} · {report.user.name ?? report.user.email}
      </h1>

      <div className="mt-6">
        <ReportView
          report={report}
          actions={report.status === "SUBMITTED" ? <ReviewForm reportId={report.id} /> : undefined}
        />
      </div>
    </div>
  );
}
