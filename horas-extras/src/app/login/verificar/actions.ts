"use server";

import { redirect } from "next/navigation";
import { prisma } from "@/lib/prisma";
import { createSession } from "@/lib/auth";
import { verifyOtpSchema } from "@/lib/validation";

export type VerifyOtpState = {
  error?: string;
};

export async function verifyOtpAction(
  _prevState: VerifyOtpState,
  formData: FormData
): Promise<VerifyOtpState> {
  const parsed = verifyOtpSchema.safeParse({
    email: formData.get("email"),
    code: formData.get("code"),
  });
  if (!parsed.success) {
    return { error: "Ingresa un código válido de 4 dígitos." };
  }
  const { email, code } = parsed.data;

  const otp = await prisma.otpCode.findFirst({
    where: { email, consumedAt: null },
    orderBy: { createdAt: "desc" },
  });

  if (!otp || otp.expiresAt < new Date()) {
    return { error: "El código venció. Vuelve a solicitar uno." };
  }

  if (otp.attempts >= 5) {
    return { error: "Demasiados intentos. Vuelve a solicitar un código." };
  }

  if (otp.code !== code) {
    await prisma.otpCode.update({
      where: { id: otp.id },
      data: { attempts: { increment: 1 } },
    });
    return { error: "Código incorrecto." };
  }

  await prisma.otpCode.update({
    where: { id: otp.id },
    data: { consumedAt: new Date() },
  });

  const user = await prisma.user.findUniqueOrThrow({ where: { email } });

  await createSession({ sub: user.id, role: user.role, name: user.name ?? email });

  const next = formData.get("next");
  redirect(typeof next === "string" && next.startsWith("/") ? next : "/");
}
