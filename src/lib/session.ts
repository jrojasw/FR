import { SignJWT, jwtVerify } from "jose";
import type { Role } from "@/generated/prisma/enums";

export const SESSION_COOKIE_NAME = "fr_session";

export type SessionPayload = {
  sub: string;
  role: Role;
  name: string;
  email: string;
};

function secretKey() {
  const secret = process.env.SESSION_SECRET;
  if (!secret) throw new Error("SESSION_SECRET no está definido");
  return new TextEncoder().encode(secret);
}

export async function signSession(payload: SessionPayload, expiresIn: string = "7d"): Promise<string> {
  return new SignJWT({ role: payload.role, name: payload.name, email: payload.email })
    .setProtectedHeader({ alg: "HS256" })
    .setSubject(payload.sub)
    .setIssuedAt()
    .setExpirationTime(expiresIn)
    .sign(secretKey());
}

export async function verifySession(token: string): Promise<SessionPayload | null> {
  try {
    const { payload } = await jwtVerify(token, secretKey());
    if (typeof payload.sub !== "string" || typeof payload.role !== "string" || typeof payload.name !== "string") {
      return null;
    }
    // "email" se agregó después: una sesión emitida antes de este cambio no
    // la trae, así que se tolera ausente en vez de invalidar sesiones ya
    // activas.
    const email = typeof payload.email === "string" ? payload.email : "";
    return { sub: payload.sub, role: payload.role as Role, name: payload.name, email };
  } catch {
    return null;
  }
}
