import { notFound } from "next/navigation";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { toDateInputValue } from "@/lib/format";
import { ReportEditor } from "@/components/ReportEditor";
import { adminUpdateReportAction } from "../../actions";

export default async function AdminEditReportPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireRole("ADMIN");
  const { id } = await params;

  const report = await prisma.expenseReport.findFirst({
    where: { id, status: { not: "DRAFT" } },
    include: { items: true, attachments: true },
  });

  if (!report) notFound();

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">
        Editar rendición N° {report.correlativo}
      </h1>
      <p className="mt-1 text-sm text-slate-500">
        Corrección de administrador: guarda los cambios sin reiniciar el estado ni el flujo de
        aprobación de esta rendición.
      </p>

      <div className="mt-6">
        <ReportEditor
          reportId={report.id}
          correlativo={report.correlativo}
          initial={{
            nombre: report.nombre,
            apellido: report.apellido,
            segundoApellido: report.segundoApellido,
            cargo: report.cargo,
            fecha: toDateInputValue(report.fecha),
            rut: report.rut ?? "",
            esParaOtraPersona: report.esParaOtraPersona,
            beneficiarioNombre: report.beneficiarioNombre ?? "",
            beneficiarioApellido: report.beneficiarioApellido ?? "",
            beneficiarioSegundoApellido: report.beneficiarioSegundoApellido ?? "",
            beneficiarioEmail: report.beneficiarioEmail ?? "",
            beneficiarioRut: report.beneficiarioRut ?? "",
            signatureData: report.signatureData ?? "",
            items: report.items.map((item) => ({
              glosa: item.glosa,
              proveedor: item.proveedor,
              tipoDocumento: item.tipoDocumento,
              numeroDocumento: item.numeroDocumento,
              montoTotal: item.montoTotal.toString(),
            })),
          }}
          initialAttachments={report.attachments}
          submitAction={adminUpdateReportAction}
          submitLabel="Guardar cambios"
          submittingLabel="Guardando…"
        />
      </div>
    </div>
  );
}
