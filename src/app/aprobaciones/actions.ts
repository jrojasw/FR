"use server";

import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { readAttachmentFile } from "@/lib/storage";
import { getPaymentNoticeEmails, getAdminEmail, getApproverEmail } from "@/lib/roles";
import { reviewReportSchema } from "@/lib/validation";
import { formatCurrency, formatDate } from "@/lib/format";
import { buildReportDocx } from "@/lib/docx-export";

export type ReviewState = {
  error?: string;
};

export async function reviewReportAction(
  reportId: string,
  _prevState: ReviewState,
  formData: FormData
): Promise<ReviewState> {
  const session = await requireRole("APROBADOR", "ADMIN");

  const parsed = reviewReportSchema.safeParse({
    decision: formData.get("decision"),
    reviewComment: formData.get("reviewComment") || undefined,
  });
  if (!parsed.success) return { error: "Selecciona aprobar o rechazar." };

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, status: "SUBMITTED" },
    include: { user: true },
  });
  if (!report) return { error: "Rendición no encontrada o ya revisada." };

  await prisma.expenseReport.update({
    where: { id: reportId },
    data: {
      status: parsed.data.decision,
      reviewedAt: new Date(),
      reviewComment: parsed.data.reviewComment,
      reviewerId: session.sub,
    },
  });

  const h = await headers();
  const baseUrl = process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;
  const decisionLabel = parsed.data.decision === "APPROVED" ? "aprobada" : "rechazada";

  await sendEmail({
    to: report.user.email,
    subject: `Tu rendición N° ${report.correlativo} fue ${decisionLabel}`,
    html: `
      <p>Tu rendición N° ${report.correlativo} (${formatCurrency(report.totalRendido.toString())}) fue <strong>${decisionLabel}</strong>.</p>
      ${parsed.data.reviewComment ? `<p>Comentario: ${parsed.data.reviewComment}</p>` : ""}
      <p><a href="${baseUrl}/rendiciones/${reportId}">Ver detalle</a></p>
    `,
  });

  if (parsed.data.decision === "APPROVED") {
    await sendEmail({
      to: getAdminEmail(),
      subject: `Falta subir comprobante de pago - Rendición N° ${report.correlativo}`,
      html: `
        <p>La rendición N° ${report.correlativo} de ${report.nombre} (${formatCurrency(report.totalRendido.toString())}) fue aprobada.</p>
        <p>Para cerrar el ciclo, sube el comprobante de la transferencia del banco como prueba de pago.</p>
        <p><a href="${baseUrl}/aprobaciones/${reportId}">Subir comprobante de pago</a></p>
      `,
    });
  }

  revalidatePath("/aprobaciones");
  redirect("/aprobaciones");
}

export type SendPaymentCertificateState = {
  error?: string;
};

export async function sendPaymentCertificateAction(
  reportId: string
): Promise<SendPaymentCertificateState> {
  const session = await requireRole("ADMIN");

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, status: "APPROVED" },
    include: { user: true, items: true, attachments: true },
  });
  if (!report) return { error: "Rendición no encontrada o no está aprobada." };
  if (!report.paymentCertificatePath) {
    return { error: "Primero sube el certificado de pago del banco." };
  }

  const certificateBuffer = await readAttachmentFile(report.paymentCertificatePath);

  const h = await headers();
  const baseUrl = process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;

  await sendEmail({
    to: getPaymentNoticeEmails(),
    subject: `Certificado de pago - Rendición N° ${report.correlativo} - ${report.nombre}`,
    html: `
      <p>Se adjunta el certificado de pago del banco para la rendición N° ${report.correlativo} de ${report.nombre} (${report.cargo}).</p>
      <ul>
        <li>Fecha rendición: ${formatDate(report.fecha)}</li>
        <li>Total rendido / Reembolso correspondiente: ${formatCurrency(report.totalRendido.toString())}</li>
      </ul>
      <p><a href="${baseUrl}/aprobaciones/${reportId}">Ver rendición</a></p>
    `,
    attachments: [
      { filename: report.paymentCertificateName ?? "certificado-pago", content: certificateBuffer },
    ],
  });

  const paidAt = new Date();

  await prisma.expenseReport.update({
    where: { id: reportId },
    data: { status: "PAID", paidAt, paidById: session.sub },
  });

  await sendEmail({
    to: report.user.email,
    subject: `Tu rendición N° ${report.correlativo} fue pagada`,
    html: `
      <p>Tu rendición N° ${report.correlativo} (${formatCurrency(report.totalRendido.toString())}) ya fue pagada.</p>
      <p><a href="${baseUrl}/rendiciones/${reportId}">Ver detalle</a></p>
    `,
  });

  try {
    const docxBuffer = await buildReportDocx({ ...report, paidAt });
    await sendEmail({
      to: getApproverEmail(),
      subject: `Expediente en Word - Rendición N° ${report.correlativo} - ${report.nombre}`,
      html: `
        <p>Se adjunta en Word el expediente completo de la rendición N° ${report.correlativo} de ${report.nombre} (${report.cargo}), con firma y los documentos adjuntos, para imprimir y guardar en carpeta física.</p>
        <p><a href="${baseUrl}/aprobaciones/${reportId}">Ver rendición</a></p>
      `,
      attachments: [
        { filename: `rendicion-${report.correlativo}.docx`, content: docxBuffer },
      ],
    });
  } catch (error) {
    console.error("Error generando/enviando el expediente en Word:", error);
  }

  revalidatePath(`/aprobaciones/${reportId}`);
  redirect(`/aprobaciones/${reportId}`);
}
