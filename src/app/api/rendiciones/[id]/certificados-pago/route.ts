import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { saveAttachmentFile } from "@/lib/storage";

const MAX_FILE_SIZE = 15 * 1024 * 1024;
const ALLOWED_TYPES = new Set(["application/pdf", "image/jpeg", "image/png", "image/webp", "image/heic"]);

// Comprobantes de pago adicionales: para cuando el certificado original
// tenía un error y ya se hizo otra transferencia para corregirlo, sin
// borrar el registro del comprobante anterior.
export async function POST(
  request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/certificados-pago">
) {
  const session = await getSession();
  if (!session || session.role !== "ADMIN") {
    return NextResponse.json({ error: "No autorizado" }, { status: 401 });
  }

  const { id: reportId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({ where: { id: reportId, status: "PAID" } });
  if (!report) {
    return NextResponse.json({ error: "La rendición no está pagada o no existe." }, { status: 404 });
  }

  const formData = await request.formData();
  const file = formData.get("file");
  if (!(file instanceof File)) {
    return NextResponse.json({ error: "No se recibió el archivo." }, { status: 400 });
  }
  if (file.size > MAX_FILE_SIZE) {
    return NextResponse.json({ error: "El archivo supera 15MB." }, { status: 400 });
  }
  if (!ALLOWED_TYPES.has(file.type)) {
    return NextResponse.json({ error: "Formato no permitido. Sube un PDF o una imagen." }, { status: 400 });
  }

  const buffer = Buffer.from(await file.arrayBuffer());

  let relativePath: string;
  try {
    relativePath = await saveAttachmentFile(reportId, file.name, buffer);
  } catch (error) {
    console.error("Error guardando comprobante de pago adicional en el almacenamiento:", error);
    const message = error instanceof Error ? error.message : "Error desconocido";
    return NextResponse.json(
      { error: `No se pudo guardar el archivo en el almacenamiento (${message})` },
      { status: 502 }
    );
  }

  const created = await prisma.paymentCertificate.create({
    data: { reportId, fileName: file.name, filePath: relativePath },
  });

  return NextResponse.json({ id: created.id, fileName: created.fileName });
}
