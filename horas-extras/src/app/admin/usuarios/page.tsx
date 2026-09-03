import { requireRole } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { CreateColaboradoraForm } from "@/components/CreateColaboradoraForm";
import { ResetPinForm } from "@/components/ResetPinForm";
import { toggleColaboradoraActiveAction } from "./actions";

export default async function UsuariosPage() {
  await requireRole("ADMIN");

  const colaboradoras = await prisma.user.findMany({
    where: { role: "SOLICITANTE" },
    orderBy: { name: "asc" },
  });

  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-8">
      <h1 className="text-2xl font-semibold text-slate-900">Colaboradoras</h1>
      <p className="mt-1 text-sm text-slate-500">
        Crea a cada colaboradora y asígnale un PIN de acceso. Puedes restablecer su PIN o
        desactivarla en cualquier momento.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <CreateColaboradoraForm />
      </div>

      <div className="mt-6 flex flex-col gap-3">
        {colaboradoras.map((c) => (
          <div
            key={c.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
          >
            <div>
              <p className="font-medium text-slate-900">{c.name}</p>
              <p className="text-xs text-slate-500">{c.active ? "Activa" : "Desactivada"}</p>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <ResetPinForm userId={c.id} />
              <form action={toggleColaboradoraActiveAction}>
                <input type="hidden" name="userId" value={c.id} />
                <button
                  type="submit"
                  className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  {c.active ? "Desactivar" : "Activar"}
                </button>
              </form>
            </div>
          </div>
        ))}
        {colaboradoras.length === 0 && (
          <p className="rounded-lg border border-slate-200 bg-white p-6 text-center text-sm text-slate-500 shadow-sm">
            Aún no hay colaboradoras. Agrega la primera arriba.
          </p>
        )}
      </div>
    </div>
  );
}
