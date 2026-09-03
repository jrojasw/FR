"use client";

import { useActionState } from "react";
import { reviewEntryAction, type ReviewState } from "@/app/aprobaciones/actions";

const initialState: ReviewState = {};

export function ReviewEntryForm({ entryId }: { entryId: string }) {
  const boundAction = reviewEntryAction.bind(null, entryId);
  const [state, formAction, pending] = useActionState(boundAction, initialState);

  return (
    <form action={formAction} className="space-y-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">Decisión</h2>

      <label className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
        <input type="checkbox" name="validadoReloj" value="true" className="mt-0.5" />
        Confirmo que revisé el reloj biométrico y la marcación coincide con este registro.
      </label>

      <div>
        <label htmlFor="reviewComment" className="block text-sm font-medium text-slate-700">
          Comentario (opcional)
        </label>
        <textarea
          id="reviewComment"
          name="reviewComment"
          rows={3}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>
      {state?.error ? (
        <p role="alert" className="text-sm text-red-600">
          {state.error}
        </p>
      ) : null}
      <div className="flex gap-3">
        <button
          type="submit"
          name="decision"
          value="APROBADA"
          disabled={pending}
          className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
        >
          Aprobar
        </button>
        <button
          type="submit"
          name="decision"
          value="RECHAZADA"
          disabled={pending}
          className="rounded-md bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-60"
        >
          Rechazar
        </button>
      </div>
    </form>
  );
}
