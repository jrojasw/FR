import { notFound } from "next/navigation";
import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { OvertimeEntryForm } from "@/components/OvertimeEntryForm";
import { toDateInputValue, toTimeInputValue } from "@/lib/format";

export default async function EditarHoraExtraPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireRole("SOLICITANTE");
  const { id } = await params;

  const entry = await prisma.overtimeEntry.findFirst({
    where: { id, userId: session.sub, status: "PENDIENTE" },
  });
  if (!entry) notFound();

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Editar registro</h1>
      <div className="mt-6">
        <OvertimeEntryForm
          mode="edit"
          entryId={entry.id}
          initial={{
            tipo: entry.tipo,
            fecha: toDateInputValue(entry.fecha),
            horaInicio: entry.horaInicio ? toTimeInputValue(entry.horaInicio) : undefined,
            horaFin: entry.horaFin ? toTimeInputValue(entry.horaFin) : undefined,
            motivo: entry.motivo ?? undefined,
          }}
        />
      </div>
    </div>
  );
}
