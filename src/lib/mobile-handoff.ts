import "server-only";
import { SignJWT, jwtVerify } from "jose";

const PURPOSE = "mobile-upload";
const TICKET_TTL = "10m";

function secretKey() {
  const secret = process.env.SESSION_SECRET;
  if (!secret) throw new Error("SESSION_SECRET no está definido");
  return new TextEncoder().encode(secret);
}

// Ticket de un solo propósito para que el trabajador pase del computador al
// celular: solo sirve para iniciar sesión como sí mismo y llegar directo a
// esta rendición, nunca se confunde con un token de sesión normal porque no
// lleva los campos (role/name) que verifySession exige.
export async function createUploadTicket(userId: string, reportId: string): Promise<string> {
  return new SignJWT({ purpose: PURPOSE, reportId })
    .setProtectedHeader({ alg: "HS256" })
    .setSubject(userId)
    .setIssuedAt()
    .setExpirationTime(TICKET_TTL)
    .sign(secretKey());
}

export async function verifyUploadTicket(
  token: string
): Promise<{ userId: string; reportId: string } | null> {
  try {
    const { payload } = await jwtVerify(token, secretKey());
    if (
      payload.purpose !== PURPOSE ||
      typeof payload.sub !== "string" ||
      typeof payload.reportId !== "string"
    ) {
      return null;
    }
    return { userId: payload.sub, reportId: payload.reportId };
  } catch {
    return null;
  }
}
