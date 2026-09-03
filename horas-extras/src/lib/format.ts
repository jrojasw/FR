const dateFormatter = new Intl.DateTimeFormat("es-CL", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

const timeFormatter = new Intl.DateTimeFormat("es-CL", {
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
  timeZone: "UTC",
});

export function formatDate(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return dateFormatter.format(date);
}

export function formatTime(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return timeFormatter.format(date);
}

export function formatHours(value: number | string): string {
  const num = typeof value === "string" ? Number(value) : value;
  return `${num.toFixed(2)} h`;
}

export function toDateInputValue(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return date.toISOString().slice(0, 10);
}

export function toTimeInputValue(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return timeFormatter.format(date);
}

export const overtimeStatusLabels: Record<string, string> = {
  PENDIENTE: "Pendiente",
  APROBADA: "Aprobada",
  RECHAZADA: "Rechazada",
};

export const overtimeStatusStyles: Record<string, string> = {
  PENDIENTE: "bg-amber-100 text-amber-800",
  APROBADA: "bg-emerald-100 text-emerald-800",
  RECHAZADA: "bg-red-100 text-red-800",
};

export const roleLabels: Record<string, string> = {
  SOLICITANTE: "Colaboradora",
  ADMIN: "Administradora",
};

export const tipoRegistroLabels: Record<string, string> = {
  TURNO_DOMINGO: "Turno domingo (aseo)",
  HORAS_EXTRA: "Horas extra semana",
};
