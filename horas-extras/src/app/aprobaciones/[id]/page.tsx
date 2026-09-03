import { notFound } from "next/navigation";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { ReviewEntryForm } from "@/components/ReviewEntryForm";
import {
  formatDate,
  formatHours,
  formatTime,
  overtimeStatusLabels,
  overtimeStatusStyles,
  tipoRegistroLabels,
} from "@/lib/format";

export default async function AprobacionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireRole("ADMIN");
  const { id } = await params;

  const entry = await prisma.overtimeEntry.findUnique({
    where: { id },
    include: { user: { select: { name: true } }, reviewer: { select: { name: true } } },
  });
  if (!entry) notFound();

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">
        {entry.user.name} · {tipoRegistroLabels[entry.tipo]}
      </h1>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <span className="text-sm text-slate-500">Estado</span>
          <span
            className={`rounded-full px-2.5 py-1 text-xs font-medium ${overtimeStatusStyles[entry.status]}`}
          >
            {overtimeStatusLabels[entry.status]}
          </span>
        </div>
        <dl className="mt-4 space-y-2 text-sm">
          <div className="flex justify-between">
            <dt className="text-slate-500">Fecha</dt>
            <dd className="text-slate-800">{formatDate(entry.fecha)}</dd>
          </div>
          {entry.tipo === "HORAS_EXTRA" && entry.horaInicio && entry.horaFin && (
            <>
              <div className="flex justify-between">
                <dt className="text-slate-500">Horario</dt>
                <dd className="text-slate-800">
                  {formatTime(entry.horaInicio)} a {formatTime(entry.horaFin)}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-slate-500">Horas</dt>
                <dd className="text-slate-800">{formatHours(entry.horas?.toString() ?? "0")}</dd>
              </div>
            </>
          )}
          {entry.motivo && (
            <div className="flex justify-between gap-4">
              <dt className="shrink-0 text-slate-500">Motivo</dt>
              <dd className="text-right text-slate-800">{entry.motivo}</dd>
            </div>
          )}
          {entry.status !== "PENDIENTE" && (
            <>
              <div className="flex justify-between">
                <dt className="text-slate-500">Validado contra reloj</dt>
                <dd className="text-slate-800">{entry.validadoReloj ? "Sí" : "No"}</dd>
              </div>
              {entry.reviewer && (
                <div className="flex justify-between">
                  <dt className="text-slate-500">Revisado por</dt>
                  <dd className="text-slate-800">{entry.reviewer.name}</dd>
                </div>
              )}
              {entry.reviewComment && (
                <div className="flex justify-between gap-4">
                  <dt className="shrink-0 text-slate-500">Comentario</dt>
                  <dd className="text-right text-slate-800">{entry.reviewComment}</dd>
                </div>
              )}
            </>
          )}
        </dl>
      </div>

      {entry.status === "PENDIENTE" && (
        <div className="mt-6">
          <ReviewEntryForm entryId={entry.id} />
        </div>
      )}
    </div>
  );
}
