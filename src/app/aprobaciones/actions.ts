"use server";

import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { reviewReportSchema } from "@/lib/validation";
import { formatCurrency } from "@/lib/format";

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
      <p>Tu rendición N° ${report.correlativo} (${formatCurrency(report.fondoPorRendir.toString())}) fue <strong>${decisionLabel}</strong>.</p>
      ${parsed.data.reviewComment ? `<p>Comentario: ${parsed.data.reviewComment}</p>` : ""}
      <p><a href="${baseUrl}/rendiciones/${reportId}">Ver detalle</a></p>
    `,
  });

  revalidatePath("/aprobaciones");
  redirect("/aprobaciones");
}
