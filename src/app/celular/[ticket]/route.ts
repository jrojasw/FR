import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { prisma } from "@/lib/prisma";
import { createSession } from "@/lib/auth";
import { verifyUploadTicket } from "@/lib/mobile-handoff";

// Enlace que arma el QR: el celular llega aquí, se valida el ticket de un
// solo propósito (10 min) y se le entrega una sesión normal (acotada a un
// par de horas) para esa persona, directo en la rendición que estaba
// llenando en el computador.
export async function GET(
  request: NextRequest,
  ctx: RouteContext<"/celular/[ticket]">
) {
  const { ticket } = await ctx.params;

  const claims = await verifyUploadTicket(ticket);
  if (!claims) {
    return NextResponse.redirect(new URL("/login?error=enlace-vencido", request.url));
  }

  const user = await prisma.user.findUnique({ where: { id: claims.userId } });
  if (!user || !user.active) {
    return NextResponse.redirect(new URL("/login?error=enlace-vencido", request.url));
  }

  await createSession({ sub: user.id, role: user.role, name: user.name ?? user.email }, 60 * 60 * 2);

  return NextResponse.redirect(new URL(`/rendiciones/${claims.reportId}`, request.url));
}
