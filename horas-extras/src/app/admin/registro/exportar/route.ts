import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { fetchRegistryEntries, computeTotals } from "@/lib/registry";
import { buildExportRows, buildXlsxBuffer, buildCsv } from "@/lib/export";
import type { OvertimeStatus } from "@/generated/prisma/enums";

export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session || session.role !== "ADMIN") {
    return NextResponse.json({ error: "No autorizado" }, { status: 401 });
  }

  const { searchParams } = new URL(request.url);
  const formato = searchParams.get("formato") === "csv" ? "csv" : "xlsx";
  const year = searchParams.get("year") ? Number(searchParams.get("year")) : undefined;
  const month = searchParams.get("month") ? Number(searchParams.get("month")) : undefined;
  const statusParam = searchParams.get("status");
  const status = statusParam && statusParam !== "ALL" ? (statusParam as OvertimeStatus) : undefined;

  const entries = await fetchRegistryEntries({ year, month, status });
  const rows = buildExportRows(entries);

  if (formato === "csv") {
    const csv = buildCsv(rows);
    return new NextResponse(csv, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="horas-extra.csv"',
      },
    });
  }

  const totals = computeTotals(entries);
  const buffer = await buildXlsxBuffer(rows, totals);
  return new NextResponse(new Uint8Array(buffer), {
    headers: {
      "Content-Type": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "Content-Disposition": 'attachment; filename="horas-extra.xlsx"',
    },
  });
}
