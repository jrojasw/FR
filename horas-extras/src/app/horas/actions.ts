"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { overtimeEntrySchema, type OvertimeEntryInput } from "@/lib/validation";
import { buildOvertimeRange, isSunday, toDateOnly } from "@/lib/overtime";

export type EntryFormState = {
  error?: string;
};

function parseEntry(formData: FormData) {
  return overtimeEntrySchema.safeParse({
    tipo: formData.get("tipo"),
    fecha: formData.get("fecha"),
    horaInicio: formData.get("horaInicio") || undefined,
    horaFin: formData.get("horaFin") || undefined,
    motivo: formData.get("motivo") || undefined,
  });
}

function buildEntryData(parsed: OvertimeEntryInput) {
  if (parsed.tipo === "TURNO_DOMINGO") {
    if (!isSunday(parsed.fecha)) {
      return { error: "La fecha del turno debe ser un domingo." } as const;
    }
    return {
      data: {
        tipo: "TURNO_DOMINGO" as const,
        fecha: toDateOnly(parsed.fecha),
        horaInicio: null,
        horaFin: null,
        horas: null,
        motivo: parsed.motivo || null,
      },
    } as const;
  }

  const { fechaInicio, fechaFin, horas } = buildOvertimeRange(
    parsed.fecha,
    parsed.horaInicio,
    parsed.horaFin
  );
  if (horas <= 0 || horas > 18) {
    return { error: "El rango de horas no es válido." } as const;
  }
  return {
    data: {
      tipo: "HORAS_EXTRA" as const,
      fecha: toDateOnly(parsed.fecha),
      horaInicio: fechaInicio,
      horaFin: fechaFin,
      horas,
      motivo: parsed.motivo,
    },
  } as const;
}

export async function createEntryAction(
  _prevState: EntryFormState,
  formData: FormData
): Promise<EntryFormState> {
  const session = await requireUser();

  const parsed = parseEntry(formData);
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const built = buildEntryData(parsed.data);
  if ("error" in built) return { error: built.error };

  await prisma.overtimeEntry.create({
    data: { ...built.data, userId: session.sub },
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

  const parsed = parseEntry(formData);
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const existing = await prisma.overtimeEntry.findFirst({
    where: { id: entryId, userId: session.sub, status: "PENDIENTE" },
  });
  if (!existing) {
    return { error: "El registro no existe o ya fue revisado." };
  }

  const built = buildEntryData(parsed.data);
  if ("error" in built) return { error: built.error };

  await prisma.overtimeEntry.update({
    where: { id: entryId },
    data: built.data,
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
