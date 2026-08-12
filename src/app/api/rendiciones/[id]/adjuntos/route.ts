import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { saveAttachmentFile } from "@/lib/storage";
import { MAX_ATTACHMENTS } from "@/lib/reports";
import type { AttachmentKind } from "@/generated/prisma/enums";

const MAX_FILE_SIZE = 15 * 1024 * 1024;
const ALLOWED_DOC_TYPES = new Set([
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
]);

export async function POST(
  request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/adjuntos">
) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "No autorizado" }, { status: 401 });

  const { id: reportId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId, userId: session.sub, status: "DRAFT" },
  });
  if (!report) return NextResponse.json({ error: "Rendición no encontrada" }, { status: 404 });

  const existingCount = await prisma.attachment.count({ where: { reportId } });

  const formData = await request.formData();
  const files = formData.getAll("files").filter((f): f is File => f instanceof File);

  if (files.length === 0) {
    return NextResponse.json({ error: "No se recibieron archivos" }, { status: 400 });
  }
  if (existingCount + files.length > MAX_ATTACHMENTS) {
    return NextResponse.json(
      { error: `Máximo ${MAX_ATTACHMENTS} adjuntos por rendición` },
      { status: 400 }
    );
  }

  const created = [];
  for (const file of files) {
    if (file.size > MAX_FILE_SIZE) {
      return NextResponse.json({ error: `${file.name} supera 15MB` }, { status: 400 });
    }
    const isImage = file.type.startsWith("image/");
    const isDoc = ALLOWED_DOC_TYPES.has(file.type);
    if (!isImage && !isDoc) {
      return NextResponse.json({ error: `Tipo de archivo no permitido: ${file.name}` }, { status: 400 });
    }

    const buffer = Buffer.from(await file.arrayBuffer());
    const relativePath = await saveAttachmentFile(reportId, file.name, buffer);
    const kind: AttachmentKind = isImage ? "PHOTO" : "DOCUMENT";

    const attachment = await prisma.attachment.create({
      data: {
        reportId,
        fileName: file.name,
        filePath: relativePath,
        mimeType: file.type || "application/octet-stream",
        size: file.size,
        kind,
      },
    });
    created.push(attachment);
  }

  return NextResponse.json({ attachments: created });
}
