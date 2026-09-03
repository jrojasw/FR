"use server";

import { redirect } from "next/navigation";
import bcrypt from "bcryptjs";
import { prisma } from "@/lib/prisma";
import { createSession } from "@/lib/auth";
import { adminLoginSchema, pinLoginSchema } from "@/lib/validation";

export type PinLoginState = {
  error?: string;
};

export async function pinLoginAction(
  _prevState: PinLoginState,
  formData: FormData
): Promise<PinLoginState> {
  const parsed = pinLoginSchema.safeParse({
    userId: formData.get("userId"),
    pin: formData.get("pin"),
  });
  if (!parsed.success) {
    return { error: "Ingresa un PIN válido." };
  }
  const { userId, pin } = parsed.data;

  const user = await prisma.user.findFirst({
    where: { id: userId, role: "SOLICITANTE", active: true },
  });
  if (!user || !user.pinHash) {
    return { error: "Colaboradora no encontrada." };
  }

  const valid = await bcrypt.compare(pin, user.pinHash);
  if (!valid) {
    return { error: "PIN incorrecto." };
  }

  await createSession({ sub: user.id, role: user.role, name: user.name });
  redirect("/");
}

export type AdminLoginState = {
  error?: string;
};

export async function adminLoginAction(
  _prevState: AdminLoginState,
  formData: FormData
): Promise<AdminLoginState> {
  const parsed = adminLoginSchema.safeParse({
    email: formData.get("email"),
    password: formData.get("password"),
  });
  if (!parsed.success) {
    return { error: "Ingresa un correo y clave válidos." };
  }
  const { email, password } = parsed.data;

  const user = await prisma.user.findFirst({
    where: { email, role: "ADMIN", active: true },
  });
  if (!user || !user.passwordHash) {
    return { error: "Credenciales incorrectas." };
  }

  const valid = await bcrypt.compare(password, user.passwordHash);
  if (!valid) {
    return { error: "Credenciales incorrectas." };
  }

  await createSession({ sub: user.id, role: user.role, name: user.name });
  redirect("/");
}
