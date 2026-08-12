import Link from "next/link";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { formatCurrency, formatDate, reportStatusLabels, reportStatusStyles } from "@/lib/format";
import { createDraftReportAction } from "./actions";

export default async function RendicionesPage() {
  const session = await requireUser();

  const reports = await prisma.expenseReport.findMany({
    where: { userId: session.sub },
    orderBy: { correlativo: "desc" },
  });

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-8">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Mis rendiciones</h1>
        <form action={createDraftReportAction}>
          <button
            type="submit"
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
          >
            Nueva rendición
          </button>
        </form>
      </div>

      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">N°</th>
              <th className="px-4 py-3">Fecha</th>
              <th className="px-4 py-3">Glosa</th>
              <th className="px-4 py-3">Fondo por rendir</th>
              <th className="px-4 py-3">Estado</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {reports.map((report) => (
              <tr key={report.id}>
                <td className="px-4 py-3 font-medium text-slate-900">{report.correlativo}</td>
                <td className="px-4 py-3 text-slate-600">{formatDate(report.fecha)}</td>
                <td className="px-4 py-3 text-slate-600">{report.glosa || "—"}</td>
                <td className="px-4 py-3 text-slate-600">{formatCurrency(report.fondoPorRendir.toString())}</td>
                <td className="px-4 py-3">
                  <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${reportStatusStyles[report.status]}`}>
                    {reportStatusLabels[report.status]}
                  </span>
                </td>
                <td className="px-4 py-3 text-right">
                  <Link href={`/rendiciones/${report.id}`} className="text-sm font-medium text-slate-700 hover:text-slate-900">
                    {report.status === "DRAFT" ? "Continuar →" : "Ver →"}
                  </Link>
                </td>
              </tr>
            ))}
            {reports.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-500">
                  Aún no tienes rendiciones. Crea la primera con &ldquo;Nueva rendición&rdquo;.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
