import Link from "next/link";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { formatDate, formatHours, formatTime, tipoRegistroLabels } from "@/lib/format";

export default async function AprobacionesPage() {
  await requireRole("ADMIN");

  const entries = await prisma.overtimeEntry.findMany({
    where: { status: "PENDIENTE" },
    include: { user: { select: { name: true } } },
    orderBy: { createdAt: "asc" },
  });

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Aprobaciones pendientes</h1>

      <div className="mt-6 flex flex-col gap-3">
        {entries.map((entry) => (
          <Link
            key={entry.id}
            href={`/aprobaciones/${entry.id}`}
            className="block rounded-lg border border-slate-200 bg-white p-4 shadow-sm active:bg-slate-50"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-base font-semibold text-slate-900">{entry.user.name}</span>
              <span className="text-sm font-medium text-slate-700">Revisar →</span>
            </div>
            <p className="mt-1 text-sm text-slate-600">
              {tipoRegistroLabels[entry.tipo]} · {formatDate(entry.fecha)}
              {entry.tipo === "HORAS_EXTRA" && entry.horaInicio && entry.horaFin
                ? ` · ${formatTime(entry.horaInicio)} a ${formatTime(entry.horaFin)} · ${formatHours(entry.horas?.toString() ?? "0")}`
                : null}
            </p>
          </Link>
        ))}
        {entries.length === 0 && (
          <p className="rounded-lg border border-slate-200 bg-white p-6 text-center text-sm text-slate-500 shadow-sm">
            No hay registros pendientes de aprobación.
          </p>
        )}
      </div>
    </div>
  );
}
