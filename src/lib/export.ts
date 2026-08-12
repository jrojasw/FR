import "server-only";
import ExcelJS from "exceljs";
import type { RegistryReport } from "@/lib/registry";
import { documentTypeLabels, reportStatusLabels } from "@/lib/format";

type ExportRow = {
  usuario: string;
  correo: string;
  correlativo: number;
  fecha: string;
  glosa: string;
  proveedor: string;
  tipoDocumento: string;
  numeroDocumento: string;
  montoItem: number;
  fondoPorRendir: number;
  totalRendido: number;
  saldoPorRendir: number;
  esReembolso: string;
  montoReembolso: number;
  estado: string;
  adjuntos: number;
  fechaPago: string;
  certificadoPago: string;
};

const COLUMNS: { key: keyof ExportRow; header: string }[] = [
  { key: "correlativo", header: "N° Correlativo" },
  { key: "usuario", header: "Nombre" },
  { key: "correo", header: "Correo" },
  { key: "fecha", header: "Fecha" },
  { key: "glosa", header: "Glosa" },
  { key: "proveedor", header: "Proveedor" },
  { key: "tipoDocumento", header: "Tipo documento" },
  { key: "numeroDocumento", header: "N° documento" },
  { key: "montoItem", header: "Monto documento" },
  { key: "fondoPorRendir", header: "Fondo por rendir" },
  { key: "totalRendido", header: "Total rendido" },
  { key: "saldoPorRendir", header: "Saldo por rendir" },
  { key: "esReembolso", header: "Es reembolso" },
  { key: "montoReembolso", header: "Monto reembolso" },
  { key: "estado", header: "Estado" },
  { key: "adjuntos", header: "N° adjuntos" },
  { key: "fechaPago", header: "Fecha de pago" },
  { key: "certificadoPago", header: "Certificado de pago" },
];

export function buildExportRows(reports: RegistryReport[]): ExportRow[] {
  const rows: ExportRow[] = [];

  for (const report of reports) {
    const base = {
      usuario: report.user.name ?? report.user.email,
      correo: report.user.email,
      correlativo: report.correlativo,
      fecha: report.fecha.toISOString().slice(0, 10),
      glosa: report.glosa,
      fondoPorRendir: Number(report.fondoPorRendir.toString()),
      totalRendido: Number(report.totalRendido.toString()),
      saldoPorRendir: Number(report.saldoPorRendir.toString()),
      esReembolso: report.esReembolso ? "Sí" : "No",
      montoReembolso: Number(report.montoReembolso.toString()),
      estado: reportStatusLabels[report.status] ?? report.status,
      adjuntos: report.attachments.length,
      fechaPago: report.paidAt ? report.paidAt.toISOString().slice(0, 10) : "",
      certificadoPago: report.paymentCertificateName ?? "",
    };

    if (report.items.length === 0) {
      rows.push({ ...base, proveedor: "", tipoDocumento: "", numeroDocumento: "", montoItem: 0 });
      continue;
    }

    for (const item of report.items) {
      rows.push({
        ...base,
        proveedor: item.proveedor,
        tipoDocumento: documentTypeLabels[item.tipoDocumento] ?? item.tipoDocumento,
        numeroDocumento: item.numeroDocumento,
        montoItem: Number(item.montoTotal.toString()),
      });
    }
  }

  return rows;
}

export async function buildXlsxBuffer(rows: ExportRow[]): Promise<Buffer> {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Rendiciones");

  sheet.columns = COLUMNS.map((c) => ({ header: c.header, key: c.key, width: 18 }));
  sheet.getRow(1).font = { bold: true };
  rows.forEach((row) => sheet.addRow(row));

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
