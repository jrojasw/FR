"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { reviewEntrySchema } from "@/lib/validation";

export type ReviewState = {
  error?: string;
};

export async function reviewEntryAction(
  entryId: string,
  _prevState: ReviewState,
  formData: FormData
): Promise<ReviewState> {
  const session = await requireRole("ADMIN");

  const parsed = reviewEntrySchema.safeParse({
    decision: formData.get("decision"),
    reviewComment: formData.get("reviewComment") || undefined,
    validadoReloj: formData.get("validadoReloj") ? "true" : "false",
  });
  if (!parsed.success) return { error: "Selecciona aprobar o rechazar." };

  if (parsed.data.decision === "APROBADA" && !parsed.data.validadoReloj) {
    return { error: "Marca que revisaste el reloj biométrico antes de aprobar." };
  }

  const entry = await prisma.overtimeEntry.findFirst({
    where: { id: entryId, status: "PENDIENTE" },
  });
  if (!entry) return { error: "Registro no encontrado o ya revisado." };

  await prisma.overtimeEntry.update({
    where: { id: entryId },
    data: {
      status: parsed.data.decision,
      reviewedAt: new Date(),
      reviewComment: parsed.data.reviewComment,
      validadoReloj: parsed.data.validadoReloj,
      reviewerId: session.sub,
    },
  });

  revalidatePath("/aprobaciones");
  redirect("/aprobaciones");
}
