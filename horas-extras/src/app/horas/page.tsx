import Link from "next/link";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import {
  formatDate,
  formatHours,
  formatTime,
  overtimeStatusLabels,
  overtimeStatusStyles,
  tipoRegistroLabels,
} from "@/lib/format";
import { deleteEntryAction } from "./actions";

export default async function MisHorasPage() {
  const session = await requireRole("SOLICITANTE");

  const entries = await prisma.overtimeEntry.findMany({
    where: { userId: session.sub },
    orderBy: { correlativo: "desc" },
  });

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Mis horas extra</h1>
        <Link
          href="/horas/nueva"
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
        >
          Registrar
        </Link>
      </div>

      <div className="mt-6 flex flex-col gap-3">
        {entries.map((entry) => (
          <div key={entry.id} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm font-semibold text-slate-900">
                {tipoRegistroLabels[entry.tipo]}
              </span>
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-medium ${overtimeStatusStyles[entry.status]}`}
              >
                {overtimeStatusLabels[entry.status]}
              </span>
            </div>
            <p className="mt-1 text-sm text-slate-600">
              {formatDate(entry.fecha)}
              {entry.tipo === "HORAS_EXTRA" && entry.horaInicio && entry.horaFin
                ? ` · ${formatTime(entry.horaInicio)} a ${formatTime(entry.horaFin)} · ${formatHours(entry.horas?.toString() ?? "0")}`
                : null}
            </p>
            {entry.motivo && <p className="mt-1 text-sm text-slate-500">{entry.motivo}</p>}
            {entry.status === "RECHAZADA" && entry.reviewComment && (
              <p className="mt-2 text-sm text-red-600">Motivo del rechazo: {entry.reviewComment}</p>
            )}
            {entry.status === "PENDIENTE" && (
              <div className="mt-3 flex gap-3">
                <Link
                  href={`/horas/${entry.id}`}
                  className="text-sm font-medium text-slate-700 hover:text-slate-900"
                >
                  Editar
                </Link>
                <form action={deleteEntryAction}>
                  <input type="hidden" name="entryId" value={entry.id} />
                  <button type="submit" className="text-sm font-medium text-red-600 hover:text-red-800">
                    Eliminar
                  </button>
                </form>
              </div>
            )}
          </div>
        ))}
        {entries.length === 0 && (
          <p className="rounded-lg border border-slate-200 bg-white p-6 text-center text-sm text-slate-500 shadow-sm">
            Aún no tienes registros. Crea el primero con &ldquo;Registrar&rdquo;.
          </p>
        )}
      </div>
    </div>
  );
}
