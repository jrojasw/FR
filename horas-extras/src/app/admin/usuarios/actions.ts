"use server";

import { revalidatePath } from "next/cache";
import bcrypt from "bcryptjs";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { createColaboradoraSchema, resetPinSchema } from "@/lib/validation";

export type ColaboradoraFormState = {
  error?: string;
};

export async function createColaboradoraAction(
  _prevState: ColaboradoraFormState,
  formData: FormData
): Promise<ColaboradoraFormState> {
  await requireRole("ADMIN");

  const parsed = createColaboradoraSchema.safeParse({
    name: formData.get("name"),
    pin: formData.get("pin"),
  });
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const pinHash = await bcrypt.hash(parsed.data.pin, 10);

  await prisma.user.create({
    data: { name: parsed.data.name, pinHash, role: "SOLICITANTE" },
  });

  revalidatePath("/admin/usuarios");
  return {};
}

export async function resetPinAction(
  _prevState: ColaboradoraFormState,
  formData: FormData
): Promise<ColaboradoraFormState> {
  await requireRole("ADMIN");

  const parsed = resetPinSchema.safeParse({
    userId: formData.get("userId"),
    pin: formData.get("pin"),
  });
  if (!parsed.success) {
    return { error: parsed.error.issues[0]?.message ?? "Revisa los datos ingresados." };
  }

  const pinHash = await bcrypt.hash(parsed.data.pin, 10);

  await prisma.user.updateMany({
    where: { id: parsed.data.userId, role: "SOLICITANTE" },
    data: { pinHash },
  });

  revalidatePath("/admin/usuarios");
  return {};
}

export async function toggleColaboradoraActiveAction(formData: FormData) {
  await requireRole("ADMIN");

  const userId = String(formData.get("userId") ?? "");
  if (!userId) return;

  const user = await prisma.user.findFirst({ where: { id: userId, role: "SOLICITANTE" } });
  if (!user) return;

  await prisma.user.update({ where: { id: userId }, data: { active: !user.active } });

  revalidatePath("/admin/usuarios");
}
