"use server";

import { redirect } from "next/navigation";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { requestOtpSchema } from "@/lib/validation";
import { resolveRoleForEmail } from "@/lib/roles";

export type RequestOtpState = {
  error?: string;
};

function generateCode(): string {
  return String(Math.floor(1000 + Math.random() * 9000));
}

export async function requestOtpAction(
  _prevState: RequestOtpState,
  formData: FormData
): Promise<RequestOtpState> {
  const parsed = requestOtpSchema.safeParse({ email: formData.get("email") });
  if (!parsed.success) {
    return { error: "Ingresa un correo válido." };
  }
  const { email } = parsed.data;

  const user = await prisma.user.upsert({
    where: { email },
    update: { role: resolveRoleForEmail(email) },
    create: { email, role: resolveRoleForEmail(email) },
  });

  await prisma.otpCode.updateMany({
    where: { email, consumedAt: null },
    data: { consumedAt: new Date() },
  });

  const code = generateCode();
  const expiresAt = new Date(Date.now() + 10 * 60 * 1000);

  await prisma.otpCode.create({
    data: { email, code, expiresAt, userId: user.id },
  });

  const { delivered } = await sendEmail({
    to: email,
    subject: "Tu código de acceso - Fondos a Rendir",
    html: `<p>Tu código de acceso a Fondos a Rendir es:</p>
      <p style="font-size:28px;font-weight:bold;letter-spacing:6px">${code}</p>
      <p>Vence en 10 minutos. Si no lo solicitaste, ignora este correo.</p>`,
  });

  const params = new URLSearchParams({ email });
  const next = formData.get("next");
  if (typeof next === "string" && next.startsWith("/")) params.set("next", next);
  if (!delivered) params.set("dev", code);

  redirect(`/login/verificar?${params.toString()}`);
}
