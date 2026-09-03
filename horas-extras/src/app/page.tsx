import Link from "next/link";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { overtimeStatusLabels } from "@/lib/format";

export default async function HomePage() {
  const session = await requireUser();

  if (session.role === "ADMIN") {
    const pending = await prisma.overtimeEntry.count({ where: { status: "PENDIENTE" } });

    return (
      <div className="mx-auto w-full max-w-4xl px-4 py-8">
        <h1 className="text-2xl font-semibold text-slate-900">Hola, {session.name}</h1>
        <p className="mt-1 text-sm text-slate-500">Panel de administración</p>

        <div className="mt-6 flex flex-wrap gap-3">
          <Link
            href="/aprobaciones"
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
          >
            Aprobaciones pendientes ({pending})
          </Link>
          <Link
            href="/admin/registro"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            Registro y totales del mes
          </Link>
          <Link
            href="/admin/usuarios"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            Colaboradoras
          </Link>
        </div>
      </div>
    );
  }

  const counts = await prisma.overtimeEntry.groupBy({
    by: ["status"],
    where: { userId: session.sub },
    _count: { _all: true },
  });
  const byStatus = Object.fromEntries(counts.map((c) => [c.status, c._count._all]));

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Hola, {session.name}</h1>
      <p className="mt-1 text-sm text-slate-500">Tus horas extra y turnos domingo</p>

      <div className="mt-6 grid grid-cols-3 gap-4">
        {(["PENDIENTE", "APROBADA", "RECHAZADA"] as const).map((status) => (
          <div key={status} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <p className="text-xs uppercase tracking-wide text-slate-500">
              {overtimeStatusLabels[status]}
            </p>
            <p className="mt-1 text-2xl font-semibold text-slate-900">{byStatus[status] ?? 0}</p>
          </div>
        ))}
      </div>

      <div className="mt-8 flex flex-wrap gap-3">
        <Link
          href="/horas/nueva"
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
        >
          + Registrar horas extra
        </Link>
        <Link
          href="/horas"
          className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
        >
          Mis horas extra
        </Link>
      </div>
    </div>
  );
}
