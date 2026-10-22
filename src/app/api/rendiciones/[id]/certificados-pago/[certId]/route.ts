import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { readAttachmentFile, deleteAttachmentFile } from "@/lib/storage";
import { canViewRegistry } from "@/lib/roles";

export async function GET(
  _request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/certificados-pago/[certId]">
) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "No autorizado" }, { status: 401 });

  const { id: reportId, certId } = await ctx.params;

  const certificate = await prisma.paymentCertificate.findFirst({ where: { id: certId, reportId } });
  if (!certificate) return new NextResponse("No encontrado", { status: 404 });

  const report = await prisma.expenseReport.findUnique({ where: { id: reportId } });
  if (!report) return new NextResponse("No encontrado", { status: 404 });

  const isOwner = report.userId === session.sub;
  const canReview = session.role === "APROBADOR" || session.role === "ADMIN";
  if (!isOwner && !canReview && !canViewRegistry(session)) {
    return new NextResponse("No autorizado", { status: 403 });
  }

  const buffer = await readAttachmentFile(certificate.filePath);
  return new NextResponse(new Uint8Array(buffer), {
    headers: {
      "Content-Type": "application/octet-stream",
      "Content-Disposition": `inline; filename="${encodeURIComponent(certificate.fileName)}"`,
      "Cache-Control": "private, max-age=0, no-store",
    },
  });
}

export async function DELETE(
  _request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/certificados-pago/[certId]">
) {
  const session = await getSession();
  if (!session || session.role !== "ADMIN") {
    return NextResponse.json({ error: "No autorizado" }, { status: 401 });
  }

  const { id: reportId, certId } = await ctx.params;

  const certificate = await prisma.paymentCertificate.findFirst({ where: { id: certId, reportId } });
  if (!certificate) return NextResponse.json({ error: "No encontrado" }, { status: 404 });

  await prisma.paymentCertificate.delete({ where: { id: certId } });
  await deleteAttachmentFile(certificate.filePath);

  return NextResponse.json({ ok: true });
}
