import Link from "next/link";
import { requireUser } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { reportStatusLabels } from "@/lib/format";

export default async function HomePage() {
  const session = await requireUser();

  const myReports = await prisma.expenseReport.groupBy({
    by: ["status"],
    where: { userId: session.sub },
    _count: { _all: true },
  });

  const myCounts = Object.fromEntries(myReports.map((r) => [r.status, r._count._all]));

  let pendingApproval = 0;
  if (session.role === "APROBADOR" || session.role === "ADMIN") {
    pendingApproval = await prisma.expenseReport.count({ where: { status: "SUBMITTED" } });
  }

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Hola, {session.name}</h1>
      <p className="mt-1 text-sm text-slate-500">Panel de fondos a rendir</p>

      <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
        {(["DRAFT", "SUBMITTED", "APPROVED", "REJECTED"] as const).map((status) => (
          <div key={status} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <p className="text-xs uppercase tracking-wide text-slate-500">{reportStatusLabels[status]}</p>
            <p className="mt-1 text-2xl font-semibold text-slate-900">{myCounts[status] ?? 0}</p>
          </div>
        ))}
      </div>

      <div className="mt-8 flex flex-wrap gap-3">
        <Link
          href="/rendiciones"
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
        >
          Mis rendiciones
        </Link>
        {(session.role === "APROBADOR" || session.role === "ADMIN") && (
          <Link
            href="/aprobaciones"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            Aprobaciones pendientes ({pendingApproval})
          </Link>
        )}
        {session.role === "ADMIN" && (
          <Link
            href="/admin/registro"
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            Registro y exportación
          </Link>
        )}
      </div>
    </div>
  );
}
