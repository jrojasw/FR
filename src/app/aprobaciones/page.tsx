import Link from "next/link";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { formatCurrency, formatDate } from "@/lib/format";

export default async function AprobacionesPage() {
  await requireRole("APROBADOR", "ADMIN");

  const reports = await prisma.expenseReport.findMany({
    where: { status: "SUBMITTED" },
    include: { user: { select: { email: true } } },
    orderBy: { submittedAt: "asc" },
  });

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Aprobaciones pendientes</h1>

      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">N°</th>
              <th className="px-4 py-3">Nombre</th>
              <th className="px-4 py-3">Fecha</th>
              <th className="px-4 py-3">Total rendido</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {reports.map((report) => (
              <tr key={report.id}>
                <td className="px-4 py-3 font-medium text-slate-900">{report.correlativo}</td>
                <td className="px-4 py-3 text-slate-600">
                  {report.nombre} <span className="text-slate-400">· {report.user.email}</span>
                </td>
                <td className="px-4 py-3 text-slate-600">{formatDate(report.fecha)}</td>
                <td className="px-4 py-3 text-slate-600">{formatCurrency(report.totalRendido.toString())}</td>
                <td className="px-4 py-3 text-right">
                  <Link href={`/aprobaciones/${report.id}`} className="text-sm font-medium text-slate-700 hover:text-slate-900">
                    Revisar →
                  </Link>
                </td>
              </tr>
            ))}
            {reports.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-500">
                  No hay rendiciones pendientes de aprobación.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
