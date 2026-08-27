"use server";

import { redirect } from "next/navigation";
import { headers } from "next/headers";
import QRCode from "qrcode";
import { prisma } from "@/lib/prisma";
import { requireUser } from "@/lib/auth";
import { sendEmail } from "@/lib/email";
import { getApproverEmail, getAdminEmail } from "@/lib/roles";
import { formatCurrency, formatDate, documentTypeLabels } from "@/lib/format";
import { computeTotals } from "@/lib/reports";
import { isValidRut, formatRut } from "@/lib/rut";
import { createUploadTicket } from "@/lib/mobile-handoff";
import {
  createReportSchema,
  saveItemsSchema,
  finalizeReportSchema,
} from "@/lib/validation";

export async function createDraftReportAction() {
  const session = await requireUser();

  const user = await prisma.user.findUniqueOrThrow({ where: { id: session.sub } });

  const report = await prisma.expenseReport.create({
    data: {
      nombre: user.name ?? "",
      apellido: "",
      segundoApellido: "",
      cargo: user.cargo ?? "",
      fecha: new Date(),
      userId: session.sub,
    },
  });

  redirect(`/rendiciones/${report.id}`);
}

export async function deleteDraftReportAction(reportId: string) {
  const session = await requireUser();

  await prisma.expenseReport.deleteMany({
    where: { id: reportId, userId: session.sub, status: "DRAFT" },
  });

  redirect("/rendiciones");
}

export type MobileUploadLinkState = {
  error?: string;
  url?: string;
  qrDataUrl?: string;
  expiresInMinutes?: number;
};

// Genera un enlace de un solo uso (10 min) para que la persona pueda abrir
// esta rendición en su celular —escaneando el QR— y usar la cámara ahí,
// mientras sigue con el resto del formulario en el computador.
export async function createMobileUploadLinkAction(reportId: string): Promise<MobileUploadLinkState> {
  const session = await requireUser();

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, userId: session.sub, status: "DRAFT" },
  });
  if (!report) return { error: "Rendición no encontrada." };

  const ticket = await createUploadTicket(session.sub, reportId);

  const h = await headers();
  const baseUrl = process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;
  const url = `${baseUrl}/celular/${ticket}`;

  const qrDataUrl = await QRCode.toDataURL(url, { width: 320, margin: 1 });

  return { url, qrDataUrl, expiresInMinutes: 10 };
}

export type FinalizeReportState = {
  error?: string;
};

export async function finalizeReportAction(
  reportId: string,
  formData: FormData
): Promise<FinalizeReportState> {
  const session = await requireUser();

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, userId: session.sub },
  });
  if (!report) return { error: "Rendición no encontrada." };
  if (report.status !== "DRAFT") return { error: "Esta rendición ya fue enviada." };

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
    return { error: "Adjunta al menos un comprobante." };
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
        status: "SUBMITTED",
        submittedAt: new Date(),
      },
    }),
    prisma.user.update({
      where: { id: session.sub },
      data: { name: `${nombre} ${apellido} ${segundoApellido}`.trim(), cargo },
    }),
  ]);

  const h = await headers();
  const baseUrl = process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;

  const itemsHtml = items
    .map(
      (item) =>
        `<li>${documentTypeLabels[item.tipoDocumento]} N° ${item.numeroDocumento} — ${item.proveedor} (${item.glosa}): ${formatCurrency(item.montoTotal)}</li>`
    )
    .join("");

  const nombreCompleto = `${nombre} ${apellido} ${segundoApellido}`.trim();
  const nombreBeneficiario = `${beneficiarioNombre} ${beneficiarioApellido} ${beneficiarioSegundoApellido}`.trim();
  const porTercero = esParaOtraPersona ? `<li>A nombre de: ${nombreBeneficiario}</li>` : "";

  await sendEmail({
    to: [getApproverEmail(), getAdminEmail()],
    subject: `Nueva rendición N° ${report.correlativo} de ${nombreCompleto}`,
    html: `
      <p>${nombreCompleto} (${cargo}) envió una rendición de fondos para tu revisión${esParaOtraPersona ? ` a nombre de ${nombreBeneficiario}` : ""}.</p>
      <ul>
        <li>Correlativo: N° ${report.correlativo}</li>
        <li>Fecha: ${formatDate(new Date(fecha))}</li>
        ${porTercero}
      </ul>
      <p>Documentos:</p>
      <ul>${itemsHtml}</ul>
      <p><strong>Total rendido / Reembolso correspondiente: ${formatCurrency(totals.totalRendido)}</strong></p>
      <p><a href="${baseUrl}/aprobaciones/${reportId}">Revisar rendición</a></p>
    `,
  });

  redirect(`/rendiciones/${reportId}`);
}
