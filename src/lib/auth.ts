import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { signSession, verifySession, SESSION_COOKIE_NAME, type SessionPayload } from "./session";
import { canViewRegistry } from "./roles";
import type { Role } from "@/generated/prisma/enums";

export async function createSession(payload: SessionPayload, maxAgeSeconds: number = 60 * 60 * 24 * 7) {
  const expiresIn = `${maxAgeSeconds}s`;
  const token = await signSession(payload, expiresIn);
  const cookieStore = await cookies();
  cookieStore.set(SESSION_COOKIE_NAME, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: maxAgeSeconds,
  });
}

export async function destroySession() {
  const cookieStore = await cookies();
  cookieStore.delete(SESSION_COOKIE_NAME);
}

export async function getSession(): Promise<SessionPayload | null> {
  const cookieStore = await cookies();
  const token = cookieStore.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return null;
  return verifySession(token);
}

export async function requireUser(): Promise<SessionPayload> {
  const session = await getSession();
  if (!session) redirect("/login");
  return session;
}

export async function requireRole(...roles: Role[]): Promise<SessionPayload> {
  const session = await requireUser();
  if (!roles.includes(session.role)) redirect("/");
  return session;
}

// Igual que requireRole, pero además deja pasar a los correos de solo
// lectura del Registro (contabilidad, etc.) aunque su rol no esté en la
// lista — usado en las pantallas de ver el registro y el detalle de una
// rendición, nunca en las acciones que aprueban, pagan o eliminan.
export async function requireRoleOrRegistryViewer(...roles: Role[]): Promise<SessionPayload> {
  const session = await requireUser();
  if (!roles.includes(session.role) && !canViewRegistry(session)) redirect("/");
  return session;
}
