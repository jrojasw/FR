import "server-only";
import {
  AlignmentType,
  Document,
  HeadingLevel,
  ImageRun,
  Packer,
  Paragraph,
  Table,
  TableCell,
  TableRow,
  TextRun,
  WidthType,
} from "docx";
import sharp from "sharp";
import { readAttachmentFile } from "@/lib/storage";
import { formatCurrency, formatDate, documentTypeLabels } from "@/lib/format";

const PAGE_WIDTH_PX = 560;
const SIGNATURE_WIDTH_PX = 320;

type ReportForDocx = {
  correlativo: number;
  nombre: string;
  cargo: string;
  fecha: Date;
  rut: string | null;
  signatureData: string | null;
  totalRendido: { toString(): string };
  montoReembolso: { toString(): string };
  paidAt: Date | null;
  items: {
    glosa: string;
    proveedor: string;
    tipoDocumento: string;
    numeroDocumento: string;
    montoTotal: { toString(): string };
  }[];
  attachments: {
    fileName: string;
    filePath: string;
    mimeType: string;
    kind: string;
  }[];
};

async function toEmbeddableJpeg(
  buffer: Buffer,
  maxWidthPx: number
): Promise<{ data: Buffer; width: number; height: number } | null> {
  try {
    const resized = await sharp(buffer)
      .rotate()
      .resize({ width: maxWidthPx, withoutEnlargement: true })
      .jpeg({ quality: 82 })
      .toBuffer();
    const meta = await sharp(resized).metadata();
    return { data: resized, width: meta.width ?? maxWidthPx, height: meta.height ?? maxWidthPx };
  } catch {
    return null;
  }
}

function labelValueRow(label: string, value: string): TableRow {
  return new TableRow({
    children: [
      new TableCell({
        width: { size: 25, type: WidthType.PERCENTAGE },
        children: [new Paragraph({ children: [new TextRun({ text: label, bold: true })] })],
      }),
      new TableCell({
        width: { size: 75, type: WidthType.PERCENTAGE },
        children: [new Paragraph(value)],
      }),
    ],
  });
}

function headerCell(text: string): TableCell {
  return new TableCell({
    shading: { fill: "E2E8F0" },
    children: [new Paragraph({ children: [new TextRun({ text, bold: true })] })],
  });
}

export async function buildReportDocx(report: ReportForDocx): Promise<Buffer> {
  const children: (Paragraph | Table)[] = [];

  children.push(
    new Paragraph({
      heading: HeadingLevel.TITLE,
      children: [new TextRun(`Rendición de Fondos N° ${report.correlativo}`)],
    }),
    new Paragraph({
      children: [
        new TextRun({
          text: `Pagada${report.paidAt ? ` — ${formatDate(report.paidAt)}` : ""}`,
          italics: true,
        }),
      ],
    }),
    new Paragraph({ text: "" }),
    new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun("Datos del solicitante")] }),
    new Table({
      width: { size: 100, type: WidthType.PERCENTAGE },
      rows: [
        labelValueRow("Nombre", report.nombre),
        labelValueRow("Cargo", report.cargo),
        labelValueRow("Fecha", formatDate(report.fecha)),
        labelValueRow("RUT", report.rut ?? "—"),
      ],
    }),
    new Paragraph({ text: "" }),
    new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun("Detalle de documentos")] }),
    new Table({
      width: { size: 100, type: WidthType.PERCENTAGE },
      rows: [
        new TableRow({
          children: [
            headerCell("Glosa"),
            headerCell("Proveedor"),
            headerCell("Tipo documento"),
            headerCell("N° documento"),
            headerCell("Monto"),
          ],
        }),
        ...report.items.map(
          (item) =>
            new TableRow({
              children: [
                new TableCell({ children: [new Paragraph(item.glosa)] }),
                new TableCell({ children: [new Paragraph(item.proveedor)] }),
                new TableCell({
                  children: [new Paragraph(documentTypeLabels[item.tipoDocumento] ?? item.tipoDocumento)],
                }),
                new TableCell({ children: [new Paragraph(item.numeroDocumento)] }),
                new TableCell({ children: [new Paragraph(formatCurrency(item.montoTotal.toString()))] }),
              ],
            })
        ),
      ],
    }),
    new Paragraph({ text: "" }),
    new Paragraph({
      children: [
        new TextRun({ text: "Total rendido: ", bold: true }),
        new TextRun(formatCurrency(report.totalRendido.toString())),
      ],
    }),
    new Paragraph({
      children: [
        new TextRun({ text: "Reembolso correspondiente: ", bold: true }),
        new TextRun(formatCurrency(report.montoReembolso.toString())),
      ],
    })
  );

  children.push(new Paragraph({ text: "" }), new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun("Firma")] }));

  if (report.signatureData?.startsWith("data:image")) {
    const base64 = report.signatureData.split(",")[1] ?? "";
    const buffer = Buffer.from(base64, "base64");
    const embeddable = await toEmbeddableJpeg(buffer, SIGNATURE_WIDTH_PX);
    if (embeddable) {
      children.push(
        new Paragraph({
          children: [
            new ImageRun({
              type: "jpg",
              data: embeddable.data,
              transformation: { width: embeddable.width, height: embeddable.height },
            }),
          ],
        })
      );
    }
  } else {
    children.push(new Paragraph("(Sin firma registrada)"));
  }

  children.push(
    new Paragraph({ text: "" }),
    new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun("Fotos y documentos adjuntos")] })
  );

  if (report.attachments.length === 0) {
    children.push(new Paragraph("(Sin adjuntos)"));
  }

  for (const attachment of report.attachments) {
    const isImage = attachment.kind === "PHOTO" || attachment.mimeType.startsWith("image/");
    let embedded = false;

    if (isImage) {
      try {
        const buffer = await readAttachmentFile(attachment.filePath);
        const embeddable = await toEmbeddableJpeg(buffer, PAGE_WIDTH_PX);
        if (embeddable) {
          children.push(
            new Paragraph({
              alignment: AlignmentType.CENTER,
              children: [
                new ImageRun({
                  type: "jpg",
                  data: embeddable.data,
                  transformation: { width: embeddable.width, height: embeddable.height },
                }),
              ],
            }),
            new Paragraph({
              alignment: AlignmentType.CENTER,
              children: [new TextRun({ text: attachment.fileName, italics: true, size: 18 })],
            }),
            new Paragraph({ text: "" })
          );
          embedded = true;
        }
      } catch {
        embedded = false;
      }
    }

    if (!embedded) {
      children.push(
        new Paragraph({
          children: [
            new TextRun(
              isImage
                ? `📎 ${attachment.fileName} (no se pudo incluir la imagen en este documento; disponible en el sistema)`
                : `📎 ${attachment.fileName} (documento adjunto, disponible en el sistema)`
            ),
          ],
        })
      );
    }
  }

  children.push(
    new Paragraph({ text: "" }),
    new Paragraph({
      children: [
        new TextRun({
          text: `Generado automáticamente por Elon el ${formatDate(new Date())}.`,
          italics: true,
          size: 16,
        }),
      ],
    })
  );

  const doc = new Document({
    sections: [
      {
        properties: {},
        children,
      },
    ],
  });

  return Packer.toBuffer(doc);
}
