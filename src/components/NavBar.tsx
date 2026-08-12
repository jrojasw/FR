import Link from "next/link";
import { getSession } from "@/lib/auth";
import { logoutAction } from "@/app/logout/actions";
import { roleLabels } from "@/lib/format";

export async function NavBar() {
  const session = await getSession();
  if (!session) return null;

  const links: { href: string; label: string }[] = [
    { href: "/", label: "Inicio" },
    { href: "/rendiciones", label: "Mis rendiciones" },
  ];

  if (session.role === "APROBADOR" || session.role === "ADMIN") {
    links.push({ href: "/aprobaciones", label: "Aprobaciones" });
  }

  if (session.role === "ADMIN") {
    links.push({ href: "/admin/registro", label: "Registro" });
  }

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-4 px-4 py-3">
        <div className="flex flex-wrap items-center gap-6">
          <Link href="/" className="text-sm font-semibold text-slate-900">
            Fondos a Rendir
          </Link>
          <nav className="flex flex-wrap gap-4">
            {links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-sm text-slate-600 hover:text-slate-900"
              >
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-sm text-slate-500">
            {session.name} · {roleLabels[session.role]}
          </span>
          <form action={logoutAction}>
            <button
              type="submit"
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
            >
              Cerrar sesión
            </button>
          </form>
        </div>
      </div>
    </header>
  );
}
