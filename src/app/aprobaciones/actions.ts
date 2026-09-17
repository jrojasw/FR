"use server";

import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { readAttachmentFile } from "@/lib/storage";
import { getPaymentNoticeEmails, getAdminEmail, getApproverEmail } from "@/lib/roles";
import { reviewReportSchema, createReportSchema, saveItemsSchema, finalizeReportSchema } from "@/lib/validation";
import { formatCurrency, formatDate } from "@/lib/format";
import { buildReportPdf } from "@/lib/pdf-export";
import { computeTotals } from "@/lib/reports";
import { isValidRut, formatRut } from "@/lib/rut";

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
    const porTercero = report.esParaOtraPersona
      ? ` a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido}`
      : "";
    await sendEmail({
      to: getAdminEmail(),
      subject: `Falta subir comprobante de pago - Rendición N° ${report.correlativo}`,
      html: `
        <p>La rendición N° ${report.correlativo} de ${report.nombre} ${report.apellido} ${report.segundoApellido} (${formatCurrency(report.totalRendido.toString())})${porTercero} fue aprobada.</p>
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
  const porTercero = report.esParaOtraPersona
    ? ` a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido}`
    : "";

  await sendEmail({
    to: getPaymentNoticeEmails(),
    subject: `Certificado de pago - Rendición N° ${report.correlativo} - ${report.nombre} ${report.apellido} ${report.segundoApellido}`,
    html: `
      <p>Se adjunta el certificado de pago del banco para la rendición N° ${report.correlativo} de ${report.nombre} ${report.apellido} ${report.segundoApellido} (${report.cargo})${porTercero}.</p>
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
    const pdfBuffer = await buildReportPdf({ ...report, status: "PAID", paidAt });
    await sendEmail({
      to: [getApproverEmail(), getAdminEmail()],
      subject: `Expediente en PDF - Rendición N° ${report.correlativo} - ${report.nombre} ${report.apellido} ${report.segundoApellido}`,
      html: `
        <p>Se adjunta en PDF el expediente completo de la rendición N° ${report.correlativo} de ${report.nombre} ${report.apellido} ${report.segundoApellido} (${report.cargo})${porTercero}, con firma y los documentos adjuntos, para imprimir y guardar en carpeta física.</p>
        <p><a href="${baseUrl}/aprobaciones/${reportId}">Ver rendición</a></p>
      `,
      attachments: [
        { filename: `rendicion-${report.correlativo}.pdf`, content: pdfBuffer },
      ],
    });
  } catch (error) {
    console.error("Error generando/enviando el expediente en PDF:", error);
  }

  revalidatePath(`/aprobaciones/${reportId}`);
  redirect(`/aprobaciones/${reportId}`);
}

export type AdminUpdateReportState = {
  error?: string;
};

// Permite al Administrador corregir cualquier dato de una rendición ya
// enviada (Enviada, Aprobada, Rechazada o Pagada) sin reiniciar el ciclo:
// no cambia el estado ni dispara el correo de "nueva rendición", solo
// guarda la corrección en el lugar.
export async function adminUpdateReportAction(
  reportId: string,
  formData: FormData
): Promise<AdminUpdateReportState> {
  await requireRole("ADMIN");

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, status: { not: "DRAFT" } },
  });
  if (!report) return { error: "Rendición no encontrada." };

  const headerParsed = createReportSchema.safeParse({
    nombre: formData.get("nombre"),
    apellido: formData.get("apellido"),
    segundoApellido: formData.get("segundoApellido"),
    cargo: formData.get("cargo"),
    fecha: formData.get("fecha"),
    esParaOtraPersona: formData.get("esParaOtraPersona"),
    beneficiarioNombre: formData.get("beneficiarioNombre"),
    beneficiarioApellido: formData.get("beneficiarioApellido"),
    beneficiarioSegundoApellido: formData.get("beneficiarioSegundoApellido"),
    beneficiarioEmail: formData.get("beneficiarioEmail"),
  });
  if (!headerParsed.success) {
    return { error: headerParsed.error.issues[0]?.message ?? "Revisa el encabezado." };
  }

  let itemsRaw: unknown;
  try {
    itemsRaw = JSON.parse(String(formData.get("items") ?? "[]"));
  } catch {
    return { error: "Detalle de gastos inválido." };
  }
  const itemsParsed = saveItemsSchema.safeParse({ items: itemsRaw });
  if (!itemsParsed.success) {
    return { error: itemsParsed.error.issues[0]?.message ?? "Revisa el detalle de gastos." };
  }

  const finalizeParsed = finalizeReportSchema.safeParse({
    rut: formData.get("rut"),
    signatureData: formData.get("signatureData"),
    beneficiarioRut: formData.get("beneficiarioRut"),
  });
  if (!finalizeParsed.success) {
    return { error: finalizeParsed.error.issues[0]?.message ?? "Falta firmar o ingresar el RUT." };
  }
  if (!isValidRut(finalizeParsed.data.rut)) {
    return { error: "El RUT ingresado no es válido." };
  }

  const {
    nombre,
    apellido,
    segundoApellido,
    cargo,
    fecha,
    esParaOtraPersona,
    beneficiarioNombre,
    beneficiarioApellido,
    beneficiarioSegundoApellido,
    beneficiarioEmail,
  } = headerParsed.data;

  if (esParaOtraPersona && !isValidRut(finalizeParsed.data.beneficiarioRut)) {
    return { error: "El RUT de la persona a nombre de quien se rinde no es válido." };
  }

  const attachmentCount = await prisma.attachment.count({ where: { reportId } });
  if (attachmentCount === 0) {
    return { error: "La rendición debe tener al menos un comprobante adjunto." };
  }

  const { items } = itemsParsed.data;
  const totals = computeTotals(items);

  await prisma.$transaction([
    prisma.expenseItem.deleteMany({ where: { reportId } }),
    prisma.expenseItem.createMany({
      data: items.map((item) => ({
        reportId,
        glosa: item.glosa,
        proveedor: item.proveedor,
        tipoDocumento: item.tipoDocumento,
        numeroDocumento: item.numeroDocumento,
        montoTotal: item.montoTotal,
      })),
    }),
    prisma.expenseReport.update({
      where: { id: reportId },
      data: {
        nombre,
        apellido,
        segundoApellido,
        cargo,
        fecha: new Date(fecha),
        totalRendido: totals.totalRendido,
        montoReembolso: totals.montoReembolso,
        rut: formatRut(finalizeParsed.data.rut),
        signatureData: finalizeParsed.data.signatureData,
        esParaOtraPersona,
        beneficiarioNombre: esParaOtraPersona ? beneficiarioNombre : null,
        beneficiarioApellido: esParaOtraPersona ? beneficiarioApellido : null,
        beneficiarioSegundoApellido: esParaOtraPersona ? beneficiarioSegundoApellido : null,
        beneficiarioRut: esParaOtraPersona ? formatRut(finalizeParsed.data.beneficiarioRut) : null,
        beneficiarioEmail: esParaOtraPersona && beneficiarioEmail ? beneficiarioEmail : null,
        // El estado (Enviada/Aprobada/Rechazada/Pagada) y sus fechas
        // asociadas no cambian: esto es una corrección, no un nuevo ciclo.
      },
    }),
  ]);

  revalidatePath(`/aprobaciones/${reportId}`);
  redirect(`/aprobaciones/${reportId}`);
}
