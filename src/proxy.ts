import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { verifySession, SESSION_COOKIE_NAME } from "@/lib/session";
import { canViewRegistry } from "@/lib/roles";

const PUBLIC_PATHS = new Set(["/login", "/login/verificar"]);

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // El QR de "usar el celular" lleva a un ticket de un solo propósito que
  // valida su propia autorización (ver src/app/celular/[ticket]/route.ts) y
  // debe funcionar sin sesión previa en ese navegador — pasa siempre de
  // largo, sin los redirects normales de login/rol de abajo.
  if (pathname.startsWith("/celular/")) {
    return NextResponse.next();
  }

  const token = request.cookies.get(SESSION_COOKIE_NAME)?.value;
  const session = token ? await verifySession(token) : null;

  if (!session) {
    if (PUBLIC_PATHS.has(pathname)) return NextResponse.next();
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("next", pathname);
    return NextResponse.redirect(loginUrl);
  }

  if (PUBLIC_PATHS.has(pathname)) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  if (pathname.startsWith("/admin")) {
    // El Registro (a diferencia del resto de /admin) también lo pueden ver
    // los correos de solo lectura configurados en REGISTRY_VIEWER_EMAILS.
    const isRegistro = pathname.startsWith("/admin/registro");
    if (session.role !== "ADMIN" && !(isRegistro && canViewRegistry(session))) {
      return NextResponse.redirect(new URL("/", request.url));
    }
  }

  if (pathname.startsWith("/aprobaciones") && session.role === "SOLICITANTE") {
    // El detalle de una rendición (no el listado de pendientes) también lo
    // pueden ver los correos de solo lectura del Registro, para que los
    // enlaces "Ver →" desde /admin/registro les funcionen.
    const isDetail = pathname.startsWith("/aprobaciones/");
    if (!(isDetail && canViewRegistry(session))) {
      return NextResponse.redirect(new URL("/", request.url));
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
