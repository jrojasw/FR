import type { Role } from "@/generated/prisma/enums";

const ADMIN_EMAIL = (process.env.ADMIN_EMAIL || "jorge.rojas@copayapunos.cl").toLowerCase();
const APPROVER_EMAIL = (process.env.APPROVER_EMAIL || "williams.arce@copayapunos.cl").toLowerCase();
const PAYMENT_NOTICE_EMAILS = (
  process.env.PAYMENT_NOTICE_EMAILS ||
  "contabilidad@copayapunos.cl,andres.rojas@copayapunos.cl,williams.arce@copayapunos.cl"
)
  .split(",")
  .map((email) => email.trim())
  .filter(Boolean);

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

export function getPaymentNoticeEmails(): string[] {
  return PAYMENT_NOTICE_EMAILS;
}
