export type ReportTotals = {
  totalRendido: number;
  montoReembolso: number;
};

export function computeTotals(items: { montoTotal: number }[]): ReportTotals {
  const totalRendido = items.reduce((sum, item) => sum + (Number(item.montoTotal) || 0), 0);
  return { totalRendido, montoReembolso: totalRendido };
}

export const MAX_ATTACHMENTS = 25;
