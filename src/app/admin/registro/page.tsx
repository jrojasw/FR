import Link from "next/link";
import { requireRole } from "@/lib/auth";
import { fetchRegistryReports, groupByDate, listReportYears } from "@/lib/registry";
import { formatCurrency, reportStatusLabels } from "@/lib/format";
import { EmailRegistryForm } from "@/components/EmailRegistryForm";
import { DeleteReportForm } from "@/components/DeleteReportForm";
import { deleteReportAsAdminAction } from "./actions";
import type { ReportStatus } from "@/generated/prisma/enums";

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

  const years = await listReportYears();
  const filters = {
    year: year ? Number(year) : undefined,
    month: month ? Number(month) : undefined,
    status: status && status !== "ALL" ? (status as ReportStatus) : undefined,
  };

  const reports = await fetchRegistryReports(filters);
  const grouped = groupByDate(reports);

  const exportQuery = new URLSearchParams();
  if (year) exportQuery.set("year", year);
  if (month) exportQuery.set("month", month);
  if (status) exportQuery.set("status", status);

  return (
    <div className="mx-auto w-full max-w-6xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Registro de rendiciones</h1>
      <p className="mt-1 text-sm text-slate-500">
        Documentos subidos por cada usuario, ordenados por año, mes y día.
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
            {(["SUBMITTED", "APPROVED", "REJECTED", "PAID"] as const).map((s) => (
              <option key={s} value={s}>{reportStatusLabels[s]}</option>
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

      <div className="mt-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <EmailRegistryForm year={year} month={month} status={status} />
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
                {Array.from(days.entries()).map(([d, dayReports]) => (
                  <div key={d} className="mt-2 ml-2 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
                    <p className="border-b border-slate-100 px-4 py-2 text-xs font-medium text-slate-500">
                      Día {d}
                    </p>
                    <table className="w-full text-left text-sm">
                      <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                        <tr>
                          <th className="px-4 py-2">N°</th>
                          <th className="px-4 py-2">Usuario</th>
                          <th className="px-4 py-2">N° documentos</th>
                          <th className="px-4 py-2">Total rendido</th>
                          <th className="px-4 py-2">Estado</th>
                          <th className="px-4 py-2">Adjuntos</th>
                          <th className="px-4 py-2" />
                          <th className="px-4 py-2" />
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {dayReports.map((report) => (
                          <tr key={report.id}>
                            <td className="px-4 py-2 font-medium text-slate-900">{report.correlativo}</td>
                            <td className="px-4 py-2 text-slate-600">{report.user.name ?? report.user.email}</td>
                            <td className="px-4 py-2 text-slate-600">{report.items.length}</td>
                            <td className="px-4 py-2 text-slate-600">{formatCurrency(report.totalRendido.toString())}</td>
                            <td className="px-4 py-2 text-slate-600">{reportStatusLabels[report.status]}</td>
                            <td className="px-4 py-2 text-slate-600">{report.attachments.length}</td>
                            <td className="px-4 py-2 text-right">
                              <Link href={`/aprobaciones/${report.id}`} className="text-sm font-medium text-slate-700 hover:text-slate-900">
                                Ver →
                              </Link>
                            </td>
                            <td className="px-4 py-2 text-right">
                              <DeleteReportForm
                                reportId={report.id}
                                correlativo={report.correlativo}
                                action={deleteReportAsAdminAction}
                              />
                            </td>
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
        {reports.length === 0 && (
          <p className="text-sm text-slate-500">No hay rendiciones para los filtros seleccionados.</p>
        )}
      </div>
    </div>
  );
}
