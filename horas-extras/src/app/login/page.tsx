import Link from "next/link";
import { prisma } from "@/lib/prisma";

export default async function LoginPage() {
  const colaboradoras = await prisma.user.findMany({
    where: { role: "SOLICITANTE", active: true },
    select: { id: true, name: true },
    orderBy: { name: "asc" },
  });

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-center text-lg font-semibold text-slate-900">Horas Extra</h1>
        <p className="mt-1 text-center text-sm text-slate-500">¿Quién eres?</p>

        <div className="mt-6 flex flex-col gap-3">
          {colaboradoras.map((c) => (
            <Link
              key={c.id}
              href={`/login/pin?userId=${c.id}`}
              className="rounded-md border border-slate-300 bg-white px-4 py-3 text-center text-base font-medium text-slate-800 shadow-sm hover:bg-slate-50 active:bg-slate-100"
            >
              {c.name}
            </Link>
          ))}
          {colaboradoras.length === 0 && (
            <p className="text-center text-sm text-slate-500">
              Aún no hay colaboradoras registradas.
            </p>
          )}
        </div>

        <div className="mt-8 border-t border-slate-100 pt-4 text-center">
          <Link href="/login/admin" className="text-sm text-slate-500 hover:text-slate-700">
            Soy administradora →
          </Link>
        </div>
      </div>
    </div>
  );
}
