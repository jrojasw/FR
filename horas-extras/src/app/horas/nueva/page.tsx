import { requireRole } from "@/lib/auth";
import { OvertimeEntryForm } from "@/components/OvertimeEntryForm";

export default async function NuevaHoraExtraPage() {
  await requireRole("SOLICITANTE");

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Registrar horas extra</h1>
      <div className="mt-6">
        <OvertimeEntryForm mode="create" />
      </div>
    </div>
  );
}
