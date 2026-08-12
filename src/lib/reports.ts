export type ReportTotals = {
  totalRendido: number;
  saldoPorRendir: number;
  esReembolso: boolean;
  montoReembolso: number;
};

export function computeTotals(
  fondoPorRendir: number,
  items: { montoTotal: number }[]
): ReportTotals {
  const totalRendido = items.reduce((sum, item) => sum + (Number(item.montoTotal) || 0), 0);
  const diff = fondoPorRendir - totalRendido;

  return {
    totalRendido,
    saldoPorRendir: diff > 0 ? diff : 0,
    esReembolso: diff < 0,
    montoReembolso: diff < 0 ? Math.abs(diff) : 0,
  };
}

export const MAX_ATTACHMENTS = 25;
