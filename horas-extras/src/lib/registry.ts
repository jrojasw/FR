import "server-only";
import { prisma } from "@/lib/prisma";
import type { OvertimeStatus } from "@/generated/prisma/enums";

export type RegistryFilters = {
  year?: number;
  month?: number;
  status?: OvertimeStatus;
};

export async function fetchRegistryEntries(filters: RegistryFilters) {
  const where: Record<string, unknown> = {};

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

  return prisma.overtimeEntry.findMany({
    where,
    include: {
      user: { select: { name: true } },
      reviewer: { select: { name: true } },
    },
    orderBy: { fecha: "desc" },
  });
}

export type RegistryEntry = Awaited<ReturnType<typeof fetchRegistryEntries>>[number];

export function groupByDate(entries: RegistryEntry[]) {
  const groups = new Map<string, Map<string, Map<string, RegistryEntry[]>>>();

  for (const entry of entries) {
    const y = String(entry.fecha.getUTCFullYear());
    const m = String(entry.fecha.getUTCMonth() + 1).padStart(2, "0");
    const d = String(entry.fecha.getUTCDate()).padStart(2, "0");

    if (!groups.has(y)) groups.set(y, new Map());
    const yearMap = groups.get(y)!;
    if (!yearMap.has(m)) yearMap.set(m, new Map());
    const monthMap = yearMap.get(m)!;
    if (!monthMap.has(d)) monthMap.set(d, []);
    monthMap.get(d)!.push(entry);
  }

  return groups;
}

export async function listEntryYears(): Promise<number[]> {
  const entries = await prisma.overtimeEntry.findMany({ select: { fecha: true } });
  const years = new Set(entries.map((e) => e.fecha.getUTCFullYear()));
  return Array.from(years).sort((a, b) => b - a);
}

export type PersonTotals = {
  userId: string;
  name: string;
  turnosDomingo: number;
  horasExtra: number;
};

/** Totales solo de registros Aprobados (la validación biométrica ya se confirmó). */
export function computeTotals(entries: RegistryEntry[]): PersonTotals[] {
  const totals = new Map<string, PersonTotals>();

  for (const entry of entries) {
    if (entry.status !== "APROBADA") continue;

    if (!totals.has(entry.userId)) {
      totals.set(entry.userId, {
        userId: entry.userId,
        name: entry.user.name,
        turnosDomingo: 0,
        horasExtra: 0,
      });
    }
    const t = totals.get(entry.userId)!;

    if (entry.tipo === "TURNO_DOMINGO") {
      t.turnosDomingo += 1;
    } else if (entry.horas) {
      t.horasExtra += Number(entry.horas.toString());
    }
  }

  return Array.from(totals.values()).sort((a, b) => a.name.localeCompare(b.name));
}
