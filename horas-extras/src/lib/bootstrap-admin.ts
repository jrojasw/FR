import "server-only";
import bcrypt from "bcryptjs";
import { prisma } from "@/lib/prisma";

/**
 * Crea o actualiza la cuenta de Administradora a partir de ADMIN_EMAIL /
 * ADMIN_PASSWORD al iniciar el servidor (ver src/instrumentation.ts).
 * Idempotente: se puede correr en cada arranque sin duplicar la cuenta.
 */
export async function ensureAdminAccount() {
  const email = (process.env.ADMIN_EMAIL || "").trim().toLowerCase();
  const password = process.env.ADMIN_PASSWORD || "";
  const name = process.env.ADMIN_NAME || "Administradora";

  if (!email || !password) {
    console.warn(
      "ADMIN_EMAIL / ADMIN_PASSWORD no están definidos: no se creó la cuenta de administradora."
    );
    return;
  }

  const passwordHash = await bcrypt.hash(password, 10);

  await prisma.user.upsert({
    where: { email },
    update: { passwordHash, name, role: "ADMIN", active: true },
    create: { email, passwordHash, name, role: "ADMIN" },
  });

  console.log(`Cuenta de administradora lista: ${email}`);
}
