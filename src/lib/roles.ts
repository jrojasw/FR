import type { Role } from "@/generated/prisma/enums";

const ADMIN_EMAIL = (process.env.ADMIN_EMAIL || "jorge.rojas@copayapunos.cl").toLowerCase();
const APPROVER_EMAIL = (process.env.APPROVER_EMAIL || "williams.arce@copayapunos.cl").toLowerCase();

export function resolveRoleForEmail(email: string): Role {
  const normalized = email.trim().toLowerCase();
  if (normalized === ADMIN_EMAIL) return "ADMIN";
  if (normalized === APPROVER_EMAIL) return "APROBADOR";
  return "SOLICITANTE";
}

export function getApproverEmail(): string {
  return APPROVER_EMAIL;
}

export function getAdminEmail(): string {
  return ADMIN_EMAIL;
}
