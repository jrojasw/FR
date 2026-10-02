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

  // request.url resuelve contra la dirección interna del contenedor (p.ej.
  // 0.0.0.0:8080) detrás del proxy de Railway, no contra el dominio público,
  // así que los redirects se arman con el mismo patrón baseUrl usado en el
  // resto de la app.
  const baseUrl =
    process.env.APP_URL || `${request.headers.get("x-forwarded-proto") ?? "http"}://${request.headers.get("host")}`;

  const claims = await verifyUploadTicket(ticket);
  if (!claims) {
    return NextResponse.redirect(new URL("/login?error=enlace-vencido", baseUrl));
  }

  const user = await prisma.user.findUnique({ where: { id: claims.userId } });
  if (!user || !user.active) {
    return NextResponse.redirect(new URL("/login?error=enlace-vencido", baseUrl));
  }

  await createSession(
    { sub: user.id, role: user.role, name: user.name ?? user.email, email: user.email },
    60 * 60 * 2
  );

  // Va a una vista acotada a solo subir fotos, no al formulario completo:
  // ese formulario recién se guarda al enviar la rendición, así que abrirlo
  // aquí mostraría en blanco lo que el trabajador ya escribió en el
  // computador, como si se hubiera perdido.
  return NextResponse.redirect(new URL(`/rendiciones/${claims.reportId}/subir`, baseUrl));
}
