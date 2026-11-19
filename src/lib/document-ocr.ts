import "server-only";
import path from "node:path";
import sharp from "sharp";
import { createWorker } from "tesseract.js";

// Lectura automática de boletas/facturas: gratis y sin servicios externos.
// Fotos → OCR con Tesseract (español). PDF → primero intenta leer el texto
// real embebido (lo normal en una factura electrónica chilena); si el PDF
// resulta ser solo una imagen escaneada sin texto, se rasteriza la primera
// página y también se lee con OCR.
export type DocumentOcrSuggestion = {
  tipoDocumento: "BOLETA" | "FACTURA" | "RECIBO" | null;
  numeroDocumento: string | null;
  proveedor: string | null;
  montoTotal: string | null;
  glosa: string | null;
};

const MIN_USABLE_TEXT_LENGTH = 15;

// El modelo de idioma (español) va empaquetado en el repo en vez de
// descargarse de un CDN en cada arranque: más rápido, no depende de que el
// servidor tenga salida a internet, y sigue siendo 100% gratis.
const TESSDATA_PATH = path.join(process.cwd(), "src/assets/tessdata");

async function ocrImageBuffer(buffer: Buffer): Promise<string> {
  // Tesseract no entiende HEIC/HEIF ni todos los formatos de cámara; se
  // normaliza siempre a PNG con sharp (ya es dependencia del proyecto).
  const pngBuffer = await sharp(buffer).rotate().png().toBuffer();
  const worker = await createWorker("spa", undefined, {
    langPath: TESSDATA_PATH,
    cacheMethod: "none",
    gzip: true,
  });
  try {
    const { data } = await worker.recognize(pngBuffer);
    return data.text ?? "";
  } finally {
    await worker.terminate();
  }
}

async function extractPdfText(buffer: Buffer): Promise<string> {
  const { PDFParse } = await import("pdf-parse");
  const parser = new PDFParse({ data: buffer });
  try {
    const result = await parser.getText({ first: 1 });
    const text = result.text ?? "";
    if (text.trim().length >= MIN_USABLE_TEXT_LENGTH) {
      return text;
    }

    // Sin texto embebido: probablemente es un PDF escaneado (solo imagen).
    // Se rasteriza la primera página y se lee con OCR, igual que una foto.
    const screenshot = await parser.getScreenshot({ first: 1, scale: 2 });
    const pageImage = screenshot.pages[0]?.data;
    if (!pageImage) return text;
    const imageBuffer = Buffer.isBuffer(pageImage) ? pageImage : Buffer.from(pageImage);
    return await ocrImageBuffer(imageBuffer);
  } catch (error) {
    console.error("Error leyendo texto del PDF:", error);
    return "";
  } finally {
    await parser.destroy();
  }
}

function cleanNumber(raw: string): string {
  // "12.345" o "12,345" (miles) -> "12345"; conserva solo dígitos.
  return raw.replace(/\D/g, "");
}

function parseReceiptText(text: string): DocumentOcrSuggestion {
  let tipoDocumento: DocumentOcrSuggestion["tipoDocumento"] = null;
  if (/FACTURA/i.test(text)) tipoDocumento = "FACTURA";
  else if (/BOLETA/i.test(text)) tipoDocumento = "BOLETA";
  else if (/RECIBO/i.test(text)) tipoDocumento = "RECIBO";

  let numeroDocumento: string | null = null;
  // El OCR suele leer mal el símbolo "°" (lo confunde con *, º, o incluso
  // nada), así que se acepta cualquiera de esas variantes.
  const numMatch = text.match(/(?:N[°ºo*]\.?|NRO\.?|FOLIO)\s*[:\-]?\s*(\d{3,10})/i);
  if (numMatch) numeroDocumento = numMatch[1];

  let montoTotal: string | null = null;
  const totalMatch = text.match(/TOTAL(?:\s*A\s*PAGAR)?[^\d]{0,15}\$?\s*(\d{1,3}(?:[.,]\d{3})*)(?!\d)/i);
  if (totalMatch) {
    montoTotal = cleanNumber(totalMatch[1]);
  } else {
    const amounts = Array.from(text.matchAll(/\$\s?(\d{1,3}(?:[.,]\d{3})+)/g))
      .map((m) => Number(cleanNumber(m[1])))
      .filter((n) => n > 0);
    if (amounts.length > 0) montoTotal = String(Math.max(...amounts));
  }

  let proveedor: string | null = null;
  const lines = text
    .split("\n")
    .map((l) => l.trim())
    .filter(Boolean);
  for (const line of lines.slice(0, 8)) {
    if (/^(BOLETA|FACTURA|RECIBO|R\.?U\.?T|GIRO|FECHA|N°|FOLIO)/i.test(line)) continue;
    if (/^\d+[\d.,\-\s]*$/.test(line)) continue;
    if (line.length >= 3 && /[A-Za-zÁÉÍÓÚÑáéíóúñ]/.test(line)) {
      proveedor = line.slice(0, 80);
      break;
    }
  }

  return {
    tipoDocumento,
    numeroDocumento,
    proveedor,
    montoTotal,
    glosa: proveedor ? `Compra en ${proveedor}` : null,
  };
}

export async function extractDocumentSuggestion(params: {
  buffer: Buffer;
  mimeType: string;
}): Promise<DocumentOcrSuggestion | null> {
  let text = "";
  try {
    if (params.mimeType.startsWith("image/")) {
      text = await ocrImageBuffer(params.buffer);
    } else if (params.mimeType === "application/pdf") {
      text = await extractPdfText(params.buffer);
    } else {
      return null;
    }
  } catch (error) {
    console.error("Error leyendo el documento automáticamente:", error);
    return null;
  }

  if (!text || text.trim().length < MIN_USABLE_TEXT_LENGTH) return null;
  return parseReceiptText(text);
}
