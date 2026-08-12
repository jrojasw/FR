const currencyFormatter = new Intl.NumberFormat("es-CL", {
  style: "currency",
  currency: "CLP",
  maximumFractionDigits: 0,
});

const dateFormatter = new Intl.DateTimeFormat("es-CL", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

export function formatCurrency(value: number | string): string {
  const num = typeof value === "string" ? Number(value) : value;
  return currencyFormatter.format(num);
}

export function formatDate(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return dateFormatter.format(date);
}

export function toDateInputValue(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return date.toISOString().slice(0, 10);
}

export const documentTypeLabels: Record<string, string> = {
  BOLETA: "Boleta",
  FACTURA: "Factura",
  RECIBO: "Recibo",
};

export const reportStatusLabels: Record<string, string> = {
  DRAFT: "Borrador",
  SUBMITTED: "Enviada",
  APPROVED: "Aprobada",
  REJECTED: "Rechazada",
};

export const roleLabels: Record<string, string> = {
  SOLICITANTE: "Solicitante",
  APROBADOR: "Aprobador",
  ADMIN: "Administrador",
};
