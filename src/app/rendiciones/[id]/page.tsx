import { notFound } from "next/navigation";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { toDateInputValue } from "@/lib/format";
import { ReportEditor } from "@/components/ReportEditor";
import { ReportView } from "@/components/ReportView";
import { deleteDraftReportAction } from "../actions";

export default async function RendicionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireUser();
  const { id } = await params;

  const report = await prisma.expenseReport.findFirst({
    where: { id, userId: session.sub },
    include: {
      items: true,
      attachments: true,
      reviewer: { select: { name: true, email: true } },
      paidBy: { select: { name: true, email: true } },
    },
  });

  if (!report) notFound();

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Rendición N° {report.correlativo}</h1>

      <div className="mt-6">
        {report.status === "DRAFT" ? (
          <>
            <ReportEditor
              reportId={report.id}
              correlativo={report.correlativo}
              initial={{
                nombre: report.nombre,
                apellido: report.apellido,
                segundoApellido: report.segundoApellido,
                cargo: report.cargo,
                fecha: toDateInputValue(report.fecha),
              }}
              initialAttachments={report.attachments}
            />
            <form action={deleteDraftReportAction.bind(null, report.id)} className="mt-6">
              <button type="submit" className="text-sm text-red-600 hover:text-red-800">
                Eliminar borrador
              </button>
            </form>
          </>
        ) : (
          <ReportView report={report} />
        )}
      </div>
    </div>
  );
}
