"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { headers } from "next/headers";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { getApproverEmail, getAdminEmail } from "@/lib/roles";
import { overtimeEntrySchema } from "@/lib/validation";
import { buildOvertimeRange } from "@/lib/overtime";
import { formatDate, formatHours, formatTime } from "@/lib/format";

export type EntryFormState = {
  error?: string;
};

async function baseUrl() {
  const h = await headers();
  return process.env.APP_URL || `${h.get("x-forwarded-proto") ?? "http"}://${h.get("host")}`;
}

export async function createEntryAction(
  _prevState: EntryFormState,
  formData: FormData
): Promise<EntryFormState> {
  const session = await requireUser();

  const parsed = overtimeEntrySchema.safeParse({
    fecha: formData.get("fecha"),
    horaInicio: formData.get("horaInicio"),
    horaFin: formData.get("horaFin"),
    motivo: formData.get("motivo"),
  });
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const { fecha, horaInicio, horaFin, motivo } = parsed.data;
  const { fechaInicio, fechaFin, horas } = buildOvertimeRange(fecha, horaInicio, horaFin);

  if (horas <= 0 || horas > 18) {
    return { error: "El rango de horas no es válido." };
  }

  const entry = await prisma.overtimeEntry.create({
    data: {
      fecha: new Date(`${fecha}T00:00:00.000Z`),
      horaInicio: fechaInicio,
      horaFin: fechaFin,
      horas,
      motivo,
      userId: session.sub,
    },
  });

  const url = await baseUrl();
  await sendEmail({
    to: [getApproverEmail(), getAdminEmail()],
    subject: `Nueva solicitud de horas extra - ${session.name}`,
    html: `
      <p>${session.name} registró ${formatHours(horas)} extra el ${formatDate(entry.fecha)}
      (${formatTime(fechaInicio)} a ${formatTime(fechaFin)}).</p>
      <p>Motivo: ${motivo}</p>
      <p><a href="${url}/aprobaciones/${entry.id}">Revisar solicitud</a></p>
    `,
  });

  revalidatePath("/horas");
  redirect("/horas");
}

export async function updateEntryAction(
  entryId: string,
  _prevState: EntryFormState,
  formData: FormData
): Promise<EntryFormState> {
  const session = await requireUser();

  const parsed = overtimeEntrySchema.safeParse({
    fecha: formData.get("fecha"),
    horaInicio: formData.get("horaInicio"),
    horaFin: formData.get("horaFin"),
    motivo: formData.get("motivo"),
  });
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const existing = await prisma.overtimeEntry.findFirst({
    where: { id: entryId, userId: session.sub, status: "PENDIENTE" },
  });
  if (!existing) {
    return { error: "El registro no existe o ya fue revisado." };
  }

  const { fecha, horaInicio, horaFin, motivo } = parsed.data;
  const { fechaInicio, fechaFin, horas } = buildOvertimeRange(fecha, horaInicio, horaFin);

  if (horas <= 0 || horas > 18) {
    return { error: "El rango de horas no es válido." };
  }

  await prisma.overtimeEntry.update({
    where: { id: entryId },
    data: {
      fecha: new Date(`${fecha}T00:00:00.000Z`),
      horaInicio: fechaInicio,
      horaFin: fechaFin,
      horas,
      motivo,
    },
  });

  revalidatePath("/horas");
  redirect("/horas");
}

export async function deleteEntryAction(formData: FormData) {
  const session = await requireUser();

  const entryId = String(formData.get("entryId") ?? "");
  if (!entryId) return;

  await prisma.overtimeEntry.deleteMany({
    where: { id: entryId, userId: session.sub, status: "PENDIENTE" },
  });

  revalidatePath("/horas");
}
