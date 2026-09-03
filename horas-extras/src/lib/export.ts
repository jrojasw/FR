import "server-only";
import ExcelJS from "exceljs";
import type { RegistryEntry, PersonTotals } from "@/lib/registry";
import { overtimeStatusLabels, tipoRegistroLabels } from "@/lib/format";

type ExportRow = {
  colaboradora: string;
  correlativo: number;
  tipo: string;
  fecha: string;
  horaInicio: string;
  horaFin: string;
  horas: number | string;
  motivo: string;
  estado: string;
  validadoReloj: string;
  revisadoPor: string;
  comentario: string;
};

const COLUMNS: { key: keyof ExportRow; header: string }[] = [
  { key: "correlativo", header: "N°" },
  { key: "colaboradora", header: "Colaboradora" },
  { key: "tipo", header: "Tipo de registro" },
  { key: "fecha", header: "Fecha" },
  { key: "horaInicio", header: "Hora inicio" },
  { key: "horaFin", header: "Hora término" },
  { key: "horas", header: "Horas" },
  { key: "motivo", header: "Motivo" },
  { key: "estado", header: "Estado" },
  { key: "validadoReloj", header: "Validado contra reloj" },
  { key: "revisadoPor", header: "Revisado por" },
  { key: "comentario", header: "Comentario" },
];

export function buildExportRows(entries: RegistryEntry[]): ExportRow[] {
  return entries.map((entry) => ({
    colaboradora: entry.user.name,
    correlativo: entry.correlativo,
    tipo: tipoRegistroLabels[entry.tipo] ?? entry.tipo,
    fecha: entry.fecha.toISOString().slice(0, 10),
    horaInicio: entry.horaInicio ? entry.horaInicio.toISOString().slice(11, 16) : "",
    horaFin: entry.horaFin ? entry.horaFin.toISOString().slice(11, 16) : "",
    horas: entry.horas ? Number(entry.horas.toString()) : "",
    motivo: entry.motivo ?? "",
    estado: overtimeStatusLabels[entry.status] ?? entry.status,
    validadoReloj: entry.validadoReloj ? "Sí" : "No",
    revisadoPor: entry.reviewer?.name ?? "",
    comentario: entry.reviewComment ?? "",
  }));
}

export async function buildXlsxBuffer(rows: ExportRow[], totals: PersonTotals[]): Promise<Buffer> {
  const workbook = new ExcelJS.Workbook();

  const sheet = workbook.addWorksheet("Registros");
  sheet.columns = COLUMNS.map((c) => ({ header: c.header, key: c.key, width: 16 }));
  sheet.getRow(1).font = { bold: true };
  rows.forEach((row) => sheet.addRow(row));

  const totalsSheet = workbook.addWorksheet("Totales del mes");
  totalsSheet.columns = [
    { header: "Colaboradora", key: "name", width: 24 },
    { header: "Turnos domingo aprobados", key: "turnosDomingo", width: 24 },
    { header: "Horas extra aprobadas", key: "horasExtra", width: 22 },
  ];
  totalsSheet.getRow(1).font = { bold: true };
  totals.forEach((t) => totalsSheet.addRow(t));

  const buffer = await workbook.xlsx.writeBuffer();
  return Buffer.from(buffer);
}

function csvEscape(value: string | number): string {
  const str = String(value);
  if (/[",\n;]/.test(str)) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

export function buildCsv(rows: ExportRow[]): string {
  const header = COLUMNS.map((c) => csvEscape(c.header)).join(";");
  const lines = rows.map((row) => COLUMNS.map((c) => csvEscape(row[c.key])).join(";"));
  return [header, ...lines].join("\n");
}
