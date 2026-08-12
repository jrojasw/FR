import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { readAttachmentFile } from "@/lib/storage";

export async function GET(
  request: NextRequest,
  ctx: RouteContext<"/api/adjuntos/[attachmentId]">
) {
  const session = await getSession();
  if (!session) return NextResponse.redirect(new URL("/login", request.url));

  const { attachmentId } = await ctx.params;

  const attachment = await prisma.attachment.findUnique({
    where: { id: attachmentId },
    include: { report: true },
  });
  if (!attachment) return new NextResponse("No encontrado", { status: 404 });

  const isOwner = attachment.report.userId === session.sub;
  const canReview = session.role === "APROBADOR" || session.role === "ADMIN";
  if (!isOwner && !canReview) return new NextResponse("No autorizado", { status: 403 });

  const buffer = await readAttachmentFile(attachment.filePath);
  return new NextResponse(new Uint8Array(buffer), {
    headers: {
      "Content-Type": attachment.mimeType,
      "Content-Disposition": `inline; filename="${encodeURIComponent(attachment.fileName)}"`,
      "Cache-Control": "private, max-age=0, no-store",
    },
  });
}
