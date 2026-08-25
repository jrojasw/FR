import "server-only";
import path from "node:path";
import { readFile } from "node:fs/promises";
import PDFDocument from "pdfkit";
import sharp from "sharp";
import { readAttachmentFile } from "@/lib/storage";
import { formatCurrency, formatDate, documentTypeLabels, reportStatusLabels } from "@/lib/format";

const CONTENT_WIDTH_PX = 495; // A4 usable width at 50pt margins (595.28 - 2*50)
const SIGNATURE_WIDTH_PX = 260;
const LOGO_WIDTH_PX = 90;
const LOGO_PATH = path.join(process.cwd(), "src/assets/logo-cpynos.jpg");

type ReportForPdf = {
  correlativo: number;
  status: string;
  nombre: string;
  apellido: string;
  cargo: string;
  fecha: Date;
  rut: string | null;
  esParaOtraPersona: boolean;
  beneficiarioNombre: string | null;
  beneficiarioApellido: string | null;
  beneficiarioRut: string | null;
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
      // La firma (y cualquier PNG con transparencia) debe quedar sobre fondo
      // blanco: sin esto, JPEG rellena lo transparente con negro y la firma
      // sale "en negativo".
      .flatten({ background: "#ffffff" })
      .jpeg({ quality: 82 })
      .toBuffer();
    const meta = await sharp(resized).metadata();
    return { data: resized, width: meta.width ?? maxWidthPx, height: meta.height ?? maxWidthPx };
  } catch {
    return null;
  }
}

const MIN_IMAGE_HEIGHT_PX = 200;
const IMAGE_CAPTION_ALLOWANCE_PX = 28;

// Ajusta a la caja disponible sin superar el tamaño natural: preferimos una
// imagen más chica antes que forzar una hoja nueva casi en blanco.
function fitWithinBox(
  naturalWidth: number,
  naturalHeight: number,
  maxWidth: number,
  maxHeight: number
) {
  const scale = Math.min(maxWidth / naturalWidth, maxHeight / naturalHeight, 1);
  return { width: naturalWidth * scale, height: naturalHeight * scale };
}

function ensureSpace(doc: PDFKit.PDFDocument, heightNeeded: number) {
  const bottom = doc.page.height - doc.page.margins.bottom;
  if (doc.y + heightNeeded > bottom) {
    doc.addPage();
  }
}

// pdfkit keeps advancing the text cursor's x from the last explicit-x call
// (e.g. a table cell or an image). Free-flowing text after those needs to be
// pulled back to the left margin explicitly, or it renders indented.
function resetX(doc: PDFKit.PDFDocument) {
  doc.x = doc.page.margins.left;
}

function sectionHeading(doc: PDFKit.PDFDocument, text: string) {
  ensureSpace(doc, 40);
  doc.moveDown(0.6);
  resetX(doc);
  doc.font("Helvetica-Bold").fontSize(13).fillColor("#1f2937").text(text);
  doc.moveDown(0.3);
  resetX(doc);
  doc.font("Helvetica").fontSize(10).fillColor("#000000");
}

function drawTable(
  doc: PDFKit.PDFDocument,
  columns: { header: string; width: number }[],
  rows: string[][]
) {
  const startX = doc.page.margins.left;
  const headerHeight = 20;

  function drawHeader() {
    let x = startX;
    doc.font("Helvetica-Bold").fontSize(9).fillColor("#ffffff");
    const y = doc.y;
    doc.rect(startX, y, columns.reduce((s, c) => s + c.width, 0), headerHeight).fill("#334155");
    doc.fillColor("#ffffff");
    for (const col of columns) {
      doc.text(col.header, x + 4, y + 6, { width: col.width - 8 });
      x += col.width;
    }
    doc.y = y + headerHeight;
    doc.fillColor("#000000").font("Helvetica").fontSize(9);
  }

  ensureSpace(doc, headerHeight + 20);
  drawHeader();

  let rowIndex = 0;
  for (const row of rows) {
    const rowHeights = row.map((cell, i) => doc.heightOfString(cell, { width: columns[i].width - 8 }));
    const rowHeight = Math.max(16, ...rowHeights) + 8;

    if (doc.y + rowHeight > doc.page.height - doc.page.margins.bottom) {
      doc.addPage();
      drawHeader();
    }

    const y = doc.y;
    if (rowIndex % 2 === 1) {
      doc.rect(startX, y, columns.reduce((s, c) => s + c.width, 0), rowHeight).fill("#f1f5f9");
      doc.fillColor("#000000");
    }
    let x = startX;
    for (let i = 0; i < row.length; i++) {
      doc.text(row[i], x + 4, y + 4, { width: columns[i].width - 8 });
      x += columns[i].width;
    }
    doc.y = y + rowHeight;
    rowIndex++;
  }

  resetX(doc);
  doc.moveDown(0.5);
}

export async function buildReportPdf(report: ReportForPdf): Promise<Buffer> {
  const doc = new PDFDocument({ size: "A4", margin: 50, bufferPages: true });
  const chunks: Buffer[] = [];
  doc.on("data", (chunk: Buffer) => chunks.push(chunk));
  const done = new Promise<Buffer>((resolve) => {
    doc.on("end", () => resolve(Buffer.concat(chunks)));
  });

  let logoWidth = 0;
  let logoHeight = 0;
  try {
    const logoBuffer = await readFile(LOGO_PATH);
    const embeddableLogo = await toEmbeddableJpeg(logoBuffer, LOGO_WIDTH_PX);
    if (embeddableLogo) {
      logoWidth = embeddableLogo.width;
      logoHeight = (embeddableLogo.height / embeddableLogo.width) * LOGO_WIDTH_PX;
      doc.image(embeddableLogo.data, doc.page.width - doc.page.margins.right - logoWidth, doc.page.margins.top, {
        width: logoWidth,
      });
    }
  } catch {
    // Sin logo disponible: se omite, el resto del documento sigue igual.
  }

  resetX(doc);
  doc.font("Helvetica-Bold").fontSize(20).fillColor("#1f2937").text(`Rendición de Fondos N° ${report.correlativo}`, {
    width: CONTENT_WIDTH_PX - logoWidth - 10,
  });
  doc
    .font("Helvetica-Oblique")
    .fontSize(11)
    .fillColor("#475569")
    .text(
      `${reportStatusLabels[report.status] ?? report.status}${report.paidAt ? ` — ${formatDate(report.paidAt)}` : ""}`,
      { width: CONTENT_WIDTH_PX - logoWidth - 10 }
    );
  resetX(doc);
  if (doc.y < doc.page.margins.top + logoHeight) {
    doc.y = doc.page.margins.top + logoHeight;
  }

  sectionHeading(doc, "Datos del solicitante");
  drawTable(
    doc,
    [
      { header: "Campo", width: 130 },
      { header: "Valor", width: CONTENT_WIDTH_PX - 130 },
    ],
    [
      ["Nombre", `${report.nombre} ${report.apellido}`],
      ["Cargo", report.cargo],
      ["Fecha", formatDate(report.fecha)],
      [report.esParaOtraPersona ? "RUT (quien rinde)" : "RUT", report.rut ?? "—"],
    ]
  );

  if (report.esParaOtraPersona) {
    sectionHeading(doc, "Datos de la persona a nombre de quien se rinde");
    drawTable(
      doc,
      [
        { header: "Campo", width: 130 },
        { header: "Valor", width: CONTENT_WIDTH_PX - 130 },
      ],
      [
        ["Nombre", `${report.beneficiarioNombre ?? ""} ${report.beneficiarioApellido ?? ""}`],
        ["RUT", report.beneficiarioRut ?? "—"],
      ]
    );
  }

  sectionHeading(doc, "Detalle de documentos");
  const colWidths = [110, 110, 85, 85, 105];
  drawTable(
    doc,
    [
      { header: "Glosa", width: colWidths[0] },
      { header: "Proveedor", width: colWidths[1] },
      { header: "Tipo documento", width: colWidths[2] },
      { header: "N° documento", width: colWidths[3] },
      { header: "Monto", width: colWidths[4] },
    ],
    report.items.map((item) => [
      item.glosa,
      item.proveedor,
      documentTypeLabels[item.tipoDocumento] ?? item.tipoDocumento,
      item.numeroDocumento,
      formatCurrency(item.montoTotal.toString()),
    ])
  );

  ensureSpace(doc, 40);
  resetX(doc);
  doc.font("Helvetica-Bold").fontSize(11).fillColor("#000000");
  doc.text(`Total rendido: ${formatCurrency(report.totalRendido.toString())}`);
  doc.text(`Reembolso correspondiente: ${formatCurrency(report.montoReembolso.toString())}`);

  sectionHeading(doc, "Firma");
  if (report.signatureData?.startsWith("data:image")) {
    const base64 = report.signatureData.split(",")[1] ?? "";
    const buffer = Buffer.from(base64, "base64");
    const embeddable = await toEmbeddableJpeg(buffer, SIGNATURE_WIDTH_PX);
    if (embeddable) {
      const scaledHeight = (embeddable.height / embeddable.width) * SIGNATURE_WIDTH_PX;
      ensureSpace(doc, scaledHeight + 10);
      doc.image(embeddable.data, doc.page.margins.left, doc.y, { width: SIGNATURE_WIDTH_PX });
      doc.y += scaledHeight + 8;
      resetX(doc);
    } else {
      doc.text("(No se pudo incluir la imagen de la firma)");
    }
  } else {
    doc.text("(Sin firma registrada)");
  }

  // Los adjuntos arrancan en una hoja nueva (si hay alguno): la primera hoja
  // queda completa con encabezado, detalle y firma, lista para archivar
  // aparte. Si no hay adjuntos, no se agrega una hoja extra solo por el
  // título, para no gastar papel de más al imprimir.
  if (report.attachments.length > 0) {
    doc.addPage();
    sectionHeading(doc, "Fotos y documentos adjuntos");
  }

  for (const attachment of report.attachments) {
    const isImage = attachment.kind === "PHOTO" || attachment.mimeType.startsWith("image/");
    let embedded = false;

    if (isImage) {
      try {
        const buffer = await readAttachmentFile(attachment.filePath);
        const embeddable = await toEmbeddableJpeg(buffer, CONTENT_WIDTH_PX);
        if (embeddable) {
          const bottom = doc.page.height - doc.page.margins.bottom;
          let availableHeight = bottom - doc.y - IMAGE_CAPTION_ALLOWANCE_PX;
          // Si queda muy poco espacio en la hoja actual, recién ahí se pasa
          // de hoja; si no, la imagen se ajusta para caber sin dejar una
          // hoja casi en blanco.
          if (availableHeight < MIN_IMAGE_HEIGHT_PX) {
            doc.addPage();
            availableHeight = bottom - doc.y - IMAGE_CAPTION_ALLOWANCE_PX;
          }
          const box = fitWithinBox(embeddable.width, embeddable.height, CONTENT_WIDTH_PX, availableHeight);
          doc.image(embeddable.data, doc.page.margins.left, doc.y, { width: box.width, height: box.height });
          doc.y += box.height + 4;
          resetX(doc);
          doc.font("Helvetica-Oblique").fontSize(8).fillColor("#64748b").text(attachment.fileName);
          doc.font("Helvetica").fontSize(10).fillColor("#000000");
          resetX(doc);
          doc.moveDown(0.6);
          embedded = true;
        }
      } catch {
        embedded = false;
      }
    }

    if (!embedded) {
      ensureSpace(doc, 20);
      resetX(doc);
      doc.text(
        isImage
          ? `- ${attachment.fileName} (no se pudo incluir la imagen; disponible en el sistema)`
          : `- ${attachment.fileName} (documento adjunto, disponible en el sistema)`
      );
    }
  }

  ensureSpace(doc, 30);
  doc.moveDown(1);
  resetX(doc);
  doc.font("Helvetica-Oblique").fontSize(8).fillColor("#94a3b8");
  doc.text(`Generado automáticamente por Elon el ${formatDate(new Date())}.`);

  doc.end();
  return done;
}
