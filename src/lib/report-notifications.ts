import "server-only";
import { headers } from "next/headers";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { getApproverEmail, getAdminEmail } from "@/lib/roles";
import { formatCurrency } from "@/lib/format";
import { buildReportPdf } from "@/lib/pdf-export";

// Se usa cuando una rendición Pagada cambia después de cerrado el ciclo
// (una corrección de datos, o un comprobante de pago adicional): reenvía el
// expediente en PDF actualizado a aprobador/administrador/contabilidad, y
// si es a nombre de otra persona, también le avisa a quien se rindió.
export async function sendReportUpdatedNotice(reportId: string, reason: string): Promise<void> {
  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, status: "PAID" },
    include: { items: true, attachments: true, additionalPaymentCertificates: true, user: true },
  });
  if (!report) return;

  const h = await headers();
  const baseUrl = process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;
  const porTercero = report.esParaOtraPersona
    ? ` a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido}`
    : "";
  const subjectBeneficiario = report.esParaOtraPersona
    ? ` (a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido})`
    : "";

  try {
    const pdfBuffer = await buildReportPdf(report);
    await sendEmail({
      to: [getApproverEmail(), getAdminEmail(), "contabilidad@copayapunos.cl"],
      subject: `Expediente en PDF actualizado - Rendición N° ${report.correlativo} - ${report.nombre} ${report.apellido} ${report.segundoApellido}${subjectBeneficiario}`,
      html: `
        <p>La rendición N° ${report.correlativo} de ${report.nombre} ${report.apellido} ${report.segundoApellido} (${report.cargo})${porTercero} se actualizó (${reason}). Se adjunta el expediente en PDF actualizado.</p>
        <p><a href="${baseUrl}/aprobaciones/${reportId}">Ver rendición</a></p>
      `,
      attachments: [{ filename: `rendicion-${report.correlativo}.pdf`, content: pdfBuffer }],
    });
  } catch (error) {
    console.error("Error generando/enviando el expediente actualizado:", error);
  }

  if (report.esParaOtraPersona && report.beneficiarioEmail) {
    const rendidoPorLine = `<p>Rendición ingresada por ${report.nombre} ${report.apellido} ${report.segundoApellido} a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido}.</p>`;
    await sendEmail({
      to: [report.user.email, report.beneficiarioEmail],
      subject: `Tu rendición N° ${report.correlativo} fue actualizada`,
      html: `
        <p>Tu rendición N° ${report.correlativo} (${formatCurrency(report.totalRendido.toString())}) se actualizó (${reason}).</p>
        ${rendidoPorLine}
        <p><a href="${baseUrl}/rendiciones/${reportId}">Ver detalle</a></p>
      `,
    });
  }
}
