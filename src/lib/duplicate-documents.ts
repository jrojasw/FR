import "server-only";
import { prisma } from "@/lib/prisma";

// Un documento (boleta/factura/recibo/retiro) solo se considera "ya
// rendido" si pertenece a una rendición enviada o más adelante en el
// ciclo: un Borrador todavía puede cambiar o borrarse, y una Rechazada
// nunca llegó a buen puerto, así que no cuentan como choque real.
const ALREADY_RENDERED_STATUSES = ["SUBMITTED", "APPROVED", "PAID"] as const;

export type DuplicateDocumentMatch =
  | { kind: "none" }
  | { kind: "exact"; matchedReportId: string; matchedCorrelativo: number }
  | {
      kind: "number";
      matchedReportId: string;
      matchedCorrelativo: number;
      matchedTipoDocumento: string;
    };

// Busca si el mismo N° de documento ya está registrado en otra rendición.
// Si además coincide el tipo de documento, es casi seguro el mismo papel
// ingresado dos veces (por la misma persona o por otra): eso se bloquea
// siempre, nunca se puede "aceptar y continuar".
export async function findDuplicateDocument(params: {
  tipoDocumento: string;
  numeroDocumento: string;
  excludeReportId?: string;
}): Promise<DuplicateDocumentMatch> {
  const numero = params.numeroDocumento.trim();
  if (!numero) return { kind: "none" };

  const match = await prisma.expenseItem.findFirst({
    where: {
      numeroDocumento: numero,
      report: {
        status: { in: [...ALREADY_RENDERED_STATUSES] },
        ...(params.excludeReportId ? { id: { not: params.excludeReportId } } : {}),
      },
    },
    include: { report: { select: { id: true, correlativo: true } } },
    orderBy: { createdAt: "asc" },
  });

  if (!match) return { kind: "none" };

  if (match.tipoDocumento === params.tipoDocumento) {
    return {
      kind: "exact",
      matchedReportId: match.report.id,
      matchedCorrelativo: match.report.correlativo,
    };
  }

  return {
    kind: "number",
    matchedReportId: match.report.id,
    matchedCorrelativo: match.report.correlativo,
    matchedTipoDocumento: match.tipoDocumento,
  };
}
