import { requireRole } from "@/lib/auth";
import { fetchRegistryEntries, groupByDate, listEntryYears, computeTotals } from "@/lib/registry";
import { formatHours, formatTime, overtimeStatusLabels, tipoRegistroLabels } from "@/lib/format";
import type { OvertimeStatus } from "@/generated/prisma/enums";

const MESES = [
  "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
  "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre",
];

export default async function AdminRegistroPage({
  searchParams,
}: {
  searchParams: Promise<{ year?: string; month?: string; status?: string }>;
}) {
  await requireRole("ADMIN");
  const { year, month, status } = await searchParams;

  const years = await listEntryYears();
  const filters = {
    year: year ? Number(year) : undefined,
    month: month ? Number(month) : undefined,
    status: status && status !== "ALL" ? (status as OvertimeStatus) : undefined,
  };

  const entries = await fetchRegistryEntries(filters);
  const totals = computeTotals(entries);
  const grouped = groupByDate(entries);

  const exportQuery = new URLSearchParams();
  if (year) exportQuery.set("year", year);
  if (month) exportQuery.set("month", month);
  if (status) exportQuery.set("status", status);

  return (
    <div className="mx-auto w-full max-w-6xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Registro de horas extra</h1>
      <p className="mt-1 text-sm text-slate-500">
        Registros de cada colaboradora, ordenados por año, mes y día.
      </p>

      <form method="get" className="mt-6 flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div>
          <label htmlFor="year" className="block text-xs font-medium text-slate-700">Año</label>
          <select id="year" name="year" defaultValue={year ?? ""} className="mt-1 rounded-md border border-slate-300 px-2 py-1.5 text-sm">
            <option value="">Todos</option>
            {years.map((y) => (
              <option key={y} value={y}>{y}</option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="month" className="block text-xs font-medium text-slate-700">Mes</label>
          <select id="month" name="month" defaultValue={month ?? ""} className="mt-1 rounded-md border border-slate-300 px-2 py-1.5 text-sm">
            <option value="">Todos</option>
            {MESES.map((m, i) => (
              <option key={m} value={i + 1}>{m}</option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="status" className="block text-xs font-medium text-slate-700">Estado</label>
          <select id="status" name="status" defaultValue={status ?? "ALL"} className="mt-1 rounded-md border border-slate-300 px-2 py-1.5 text-sm">
            <option value="ALL">Todos</option>
            {(["PENDIENTE", "APROBADA", "RECHAZADA"] as const).map((s) => (
              <option key={s} value={s}>{overtimeStatusLabels[s]}</option>
            ))}
          </select>
        </div>
        <button type="submit" className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-semibold text-white hover:bg-slate-700">
          Filtrar
        </button>

        <div className="ml-auto flex gap-2">
          <a
            href={`/admin/registro/exportar?formato=xlsx&${exportQuery.toString()}`}
            className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Descargar Excel
          </a>
          <a
            href={`/admin/registro/exportar?formato=csv&${exportQuery.toString()}`}
            className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Descargar CSV
          </a>
        </div>
      </form>

      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Colaboradora</th>
              <th className="px-4 py-3">Turnos domingo aprobados</th>
              <th className="px-4 py-3">Horas extra aprobadas</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {totals.map((t) => (
              <tr key={t.userId}>
                <td className="px-4 py-2 font-medium text-slate-900">{t.name}</td>
                <td className="px-4 py-2 text-slate-600">{t.turnosDomingo}</td>
                <td className="px-4 py-2 text-slate-600">{formatHours(t.horasExtra)}</td>
              </tr>
            ))}
            {totals.length === 0 && (
              <tr>
                <td colSpan={3} className="px-4 py-6 text-center text-slate-500">
                  No hay registros aprobados para los filtros seleccionados.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-6 space-y-8">
        {Array.from(grouped.entries()).map(([y, months]) => (
          <div key={y}>
            <h2 className="text-xl font-semibold text-slate-900">{y}</h2>
            {Array.from(months.entries()).map(([m, days]) => (
              <div key={m} className="mt-3 ml-2">
                <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
                  {MESES[Number(m) - 1]}
                </h3>
                {Array.from(days.entries()).map(([d, dayEntries]) => (
                  <div key={d} className="mt-2 ml-2 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
                    <p className="border-b border-slate-100 px-4 py-2 text-xs font-medium text-slate-500">
                      Día {d}
                    </p>
                    <table className="w-full text-left text-sm">
                      <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                        <tr>
                          <th className="px-4 py-2">Colaboradora</th>
                          <th className="px-4 py-2">Tipo</th>
                          <th className="px-4 py-2">Horario</th>
                          <th className="px-4 py-2">Horas</th>
                          <th className="px-4 py-2">Estado</th>
                          <th className="px-4 py-2">Reloj</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {dayEntries.map((entry) => (
                          <tr key={entry.id}>
                            <td className="px-4 py-2 text-slate-600">{entry.user.name}</td>
                            <td className="px-4 py-2 text-slate-600">{tipoRegistroLabels[entry.tipo]}</td>
                            <td className="px-4 py-2 text-slate-600">
                              {entry.horaInicio && entry.horaFin
                                ? `${formatTime(entry.horaInicio)} - ${formatTime(entry.horaFin)}`
                                : "—"}
                            </td>
                            <td className="px-4 py-2 text-slate-600">
                              {entry.horas ? formatHours(entry.horas.toString()) : "—"}
                            </td>
                            <td className="px-4 py-2 text-slate-600">{overtimeStatusLabels[entry.status]}</td>
                            <td className="px-4 py-2 text-slate-600">{entry.validadoReloj ? "Sí" : "—"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>
            ))}
          </div>
        ))}
        {entries.length === 0 && (
          <p className="text-sm text-slate-500">No hay registros para los filtros seleccionados.</p>
        )}
      </div>
    </div>
  );
}
