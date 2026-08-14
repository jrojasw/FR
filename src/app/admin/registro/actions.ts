"use server";

import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth";
import { sendEmail } from "@/lib/email";
import { prisma } from "@/lib/prisma";
import { deleteAttachmentFile } from "@/lib/storage";
import { fetchRegistryReports } from "@/lib/registry";
import { buildExportRows, buildXlsxBuffer } from "@/lib/export";
import type { ReportStatus } from "@/generated/prisma/enums";

export type SendRegistryEmailState = {
  error?: string;
  success?: boolean;
};

export async function sendRegistryEmailAction(
  _prevState: SendRegistryEmailState,
  formData: FormData
): Promise<SendRegistryEmailState> {
  await requireRole("ADMIN");

  const to = String(formData.get("to") ?? "").trim();
  if (!to || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(to)) {
    return { error: "Ingresa un correo válido." };
  }

  const year = formData.get("year") ? Number(formData.get("year")) : undefined;
  const month = formData.get("month") ? Number(formData.get("month")) : undefined;
  const statusParam = String(formData.get("status") ?? "");
  const status = statusParam && statusParam !== "ALL" ? (statusParam as ReportStatus) : undefined;

  const reports = await fetchRegistryReports({ year, month, status });
  const rows = buildExportRows(reports);
  const buffer = await buildXlsxBuffer(rows);

  await sendEmail({
    to,
    subject: "Registro de rendiciones - Fondos a Rendir",
    html: `<p>Adjunto el registro de rendiciones solicitado (${rows.length} filas).</p>`,
    attachments: [{ filename: "rendiciones.xlsx", content: buffer }],
  });

  return { success: true };
}

export async function deleteReportAsAdminAction(formData: FormData) {
  await requireRole("ADMIN");

  const reportId = String(formData.get("reportId") ?? "");
  if (!reportId) return;

  const report = await prisma.expenseReport.findUnique({
    where: { id: reportId },
    include: { attachments: { select: { filePath: true } } },
  });
  if (!report) return;

  const filePaths = report.attachments.map((a) => a.filePath);
  if (report.paymentCertificatePath) filePaths.push(report.paymentCertificatePath);
  await Promise.all(filePaths.map((filePath) => deleteAttachmentFile(filePath)));

  await prisma.expenseReport.delete({ where: { id: reportId } });

  revalidatePath("/admin/registro");
}
