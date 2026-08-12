import "server-only";
import { prisma } from "@/lib/prisma";
import type { ReportStatus } from "@/generated/prisma/enums";

export type RegistryFilters = {
  year?: number;
  month?: number;
  status?: ReportStatus;
};

export async function fetchRegistryReports(filters: RegistryFilters) {
  const where: Record<string, unknown> = { status: { not: "DRAFT" } };

  if (filters.status) {
    where.status = filters.status;
  }

  if (filters.year && filters.month) {
    where.fecha = {
      gte: new Date(Date.UTC(filters.year, filters.month - 1, 1)),
      lt: new Date(Date.UTC(filters.year, filters.month, 1)),
    };
  } else if (filters.year) {
    where.fecha = {
      gte: new Date(Date.UTC(filters.year, 0, 1)),
      lt: new Date(Date.UTC(filters.year + 1, 0, 1)),
    };
  }

  return prisma.expenseReport.findMany({
    where,
    include: {
      user: { select: { name: true, email: true } },
      items: true,
      attachments: true,
    },
    orderBy: { fecha: "desc" },
  });
}

export type RegistryReport = Awaited<ReturnType<typeof fetchRegistryReports>>[number];

export function groupByDate(reports: RegistryReport[]) {
  const groups = new Map<string, Map<string, Map<string, RegistryReport[]>>>();

  for (const report of reports) {
    const y = String(report.fecha.getUTCFullYear());
    const m = String(report.fecha.getUTCMonth() + 1).padStart(2, "0");
    const d = String(report.fecha.getUTCDate()).padStart(2, "0");

    if (!groups.has(y)) groups.set(y, new Map());
    const yearMap = groups.get(y)!;
    if (!yearMap.has(m)) yearMap.set(m, new Map());
    const monthMap = yearMap.get(m)!;
    if (!monthMap.has(d)) monthMap.set(d, []);
    monthMap.get(d)!.push(report);
  }

  return groups;
}

export async function listReportYears(): Promise<number[]> {
  const reports = await prisma.expenseReport.findMany({
    where: { status: { not: "DRAFT" } },
    select: { fecha: true },
  });
  const years = new Set(reports.map((r) => r.fecha.getUTCFullYear()));
  return Array.from(years).sort((a, b) => b - a);
}
