import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { getSession } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { buildReportPdf } from "@/lib/pdf-export";

export async function GET(
  _request: NextRequest,
  ctx: RouteContext<"/api/rendiciones/[id]/pdf">
) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "No autorizado" }, { status: 401 });

  const { id: reportId } = await ctx.params;

  const report = await prisma.expenseReport.findFirst({
    where: { id: reportId },
    include: { items: true, attachments: true },
  });
  if (!report || report.status === "DRAFT") {
    return new NextResponse("No encontrado", { status: 404 });
  }

  const isOwner = report.userId === session.sub;
  const canReview = session.role === "APROBADOR" || session.role === "ADMIN";
  if (!isOwner && !canReview) return new NextResponse("No autorizado", { status: 403 });

  const buffer = await buildReportPdf(report);
  return new NextResponse(new Uint8Array(buffer), {
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": `inline; filename="rendicion-${report.correlativo}.pdf"`,
      "Cache-Control": "private, max-age=0, no-store",
    },
  });
}
