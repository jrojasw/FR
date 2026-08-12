import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { saveAttachmentFile, readAttachmentFile } from "@/lib/storage";

const MAX_FILE_SIZE = 15 * 1024 * 1024;
const ALLOWED_TYPES = new Set(["application/pdf", "image/jpeg", "image/png", "image/webp", "image/heic"]);

export async function POST(
  request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/certificado-pago">
) {
  const session = await getSession();
  if (!session || session.role !== "ADMIN") {
    return NextResponse.json({ error: "No autorizado" }, { status: 401 });
  }

  const { id: reportId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({ where: { id: reportId, status: "APPROVED" } });
  if (!report) {
    return NextResponse.json({ error: "La rendición no está aprobada o no existe." }, { status: 404 });
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
  const relativePath = await saveAttachmentFile(reportId, file.name, buffer);

  await prisma.expenseReport.update({
    where: { id: reportId },
    data: { paymentCertificatePath: relativePath, paymentCertificateName: file.name },
  });

  return NextResponse.json({ fileName: file.name });
}

export async function GET(
  _request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/certificado-pago">
) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "No autorizado" }, { status: 401 });

  const { id: reportId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({ where: { id: reportId } });
  if (!report || !report.paymentCertificatePath) {
    return new NextResponse("No encontrado", { status: 404 });
  }

  const isOwner = report.userId === session.sub;
  const canReview = session.role === "APROBADOR" || session.role === "ADMIN";
  if (!isOwner && !canReview) return new NextResponse("No autorizado", { status: 403 });

  const buffer = await readAttachmentFile(report.paymentCertificatePath);
  return new NextResponse(new Uint8Array(buffer), {
    headers: {
      "Content-Type": "application/octet-stream",
      "Content-Disposition": `inline; filename="${encodeURIComponent(report.paymentCertificateName ?? "certificado-pago")}"`,
      "Cache-Control": "private, max-age=0, no-store",
    },
  });
}
