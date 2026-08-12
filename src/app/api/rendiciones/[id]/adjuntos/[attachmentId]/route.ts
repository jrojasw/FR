import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { deleteAttachmentFile } from "@/lib/storage";

export async function DELETE(
  _request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/adjuntos/[attachmentId]">
) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "No autorizado" }, { status: 401 });

  const { id: reportId, attachmentId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, userId: session.sub, status: "DRAFT" },
  });
  if (!report) return NextResponse.json({ error: "No permitido" }, { status: 404 });

  const attachment = await prisma.attachment.findFirst({ where: { id: attachmentId, reportId } });
  if (!attachment) return NextResponse.json({ error: "No encontrado" }, { status: 404 });

  await prisma.attachment.delete({ where: { id: attachment.id } });
  await deleteAttachmentFile(attachment.filePath);

  return NextResponse.json({ ok: true });
}
