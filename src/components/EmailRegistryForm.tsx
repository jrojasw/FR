"use client";

import { useActionState } from "react";
import { sendRegistryEmailAction, type SendRegistryEmailState } from "@/app/admin/registro/actions";

const initialState: SendRegistryEmailState = {};

export function EmailRegistryForm({
  year,
  month,
  status,
}: {
  year?: string;
  month?: string;
  status?: string;
}) {
  const [state, formAction, pending] = useActionState(sendRegistryEmailAction, initialState);

  return (
    <form action={formAction} className="flex flex-wrap items-end gap-2">
      {year ? <input type="hidden" name="year" value={year} /> : null}
      {month ? <input type="hidden" name="month" value={month} /> : null}
      {status ? <input type="hidden" name="status" value={status} /> : null}
      <div>
        <label htmlFor="to" className="block text-xs font-medium text-slate-700">
          Enviar por correo a
        </label>
        <input
          id="to"
          name="to"
          type="email"
          required
          placeholder="correo@copayapunos.cl"
          className="mt-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>
      <button
        type="submit"
        disabled={pending}
        className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        {pending ? "Enviando…" : "Enviar Excel"}
      </button>
      {state?.error ? <p className="text-sm text-red-600">{state.error}</p> : null}
      {state?.success ? <p className="text-sm text-emerald-700">Enviado correctamente.</p> : null}
    </form>
  );
}
