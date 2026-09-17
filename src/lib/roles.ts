import type { Role } from "@/generated/prisma/enums";

const ADMIN_EMAIL = (process.env.ADMIN_EMAIL || "jorge.rojas@copayapunos.cl").toLowerCase();
const APPROVER_EMAIL = (process.env.APPROVER_EMAIL || "williams.arce@copayapunos.cl").toLowerCase();
const PAYMENT_NOTICE_EMAILS = (
  process.env.PAYMENT_NOTICE_EMAILS ||
  "contabilidad@copayapunos.cl,andres.rojas@copayapunos.cl"
)
  .split(",")
  .map((email) => email.trim())
  .filter(Boolean);

// Correos que pueden ver el Registro (y el detalle de cada rendición desde
// ahí) sin ser Aprobador ni Administrador — de solo lectura: no pueden
// aprobar, subir comprobantes, eliminar ni enviar el registro por correo.
const REGISTRY_VIEWER_EMAILS = (process.env.REGISTRY_VIEWER_EMAILS || "contabilidad@copayapunos.cl")
  .split(",")
  .map((email) => email.trim().toLowerCase())
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

export function canViewRegistry(session: { role: Role; email: string }): boolean {
  if (session.role === "ADMIN") return true;
  return REGISTRY_VIEWER_EMAILS.includes(session.email.trim().toLowerCase());
}
