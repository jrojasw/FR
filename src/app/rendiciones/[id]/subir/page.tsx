import { notFound } from "next/navigation";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { AttachmentUploader } from "@/components/AttachmentUploader";

// Página minimalista para cuando el trabajador escanea el QR de "Usar el
// celular" desde el formulario en el computador: solo deja tomar/subir
// fotos para esta rendición. No muestra el formulario completo (nombre,
// detalle, firma) porque esos datos recién se guardan al enviar, así que
// mostrarlos aquí los vería en blanco y parecería que el computador perdió
// lo que ya se había escrito.
export default async function SubirDesdeCelularPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireUser();
  const { id } = await params;

  const report = await prisma.expenseReport.findFirst({
    where: { id, userId: session.sub, status: "DRAFT" },
    include: { attachments: true },
  });

  if (!report) notFound();

  return (
    <div className="mx-auto w-full max-w-lg px-4 py-8">
      <h1 className="text-xl font-semibold text-slate-900">Rendición N° {report.correlativo}</h1>
      <p className="mt-2 text-sm text-slate-500">
        Toma fotos de tus boletas o facturas. Van a aparecer automáticamente en el formulario del
        computador, sin perder lo que ya escribiste ahí.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <AttachmentUploader
          reportId={report.id}
          initialAttachments={report.attachments}
          allowMobileHandoff={false}
        />
      </div>
    </div>
  );
}
