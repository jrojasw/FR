"use client";

import { useActionState } from "react";
import {
  resendBeneficiaryNoticeAction,
  type ResendBeneficiaryNoticeState,
} from "@/app/aprobaciones/actions";

const initialState: ResendBeneficiaryNoticeState = {};

export function ResendBeneficiaryNoticeButton({
  reportId,
  beneficiarioEmail,
}: {
  reportId: string;
  beneficiarioEmail: string;
}) {
  const boundAction = resendBeneficiaryNoticeAction.bind(null, reportId);
  const [state, formAction, pending] = useActionState(boundAction, initialState);

  return (
    <div className="flex items-center justify-between rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <div>
        <h2 className="text-lg font-semibold text-slate-900">Reenviar aviso al beneficiario</h2>
        <p className="mt-1 text-sm text-slate-500">
          Reenvía el correo de estado de esta rendición a {beneficiarioEmail}.
        </p>
        {state?.error ? <p className="mt-1 text-sm text-red-600">{state.error}</p> : null}
        {state?.success ? <p className="mt-1 text-sm text-emerald-700">Correo reenviado.</p> : null}
      </div>
      <form action={formAction}>
        <button
          type="submit"
          disabled={pending}
          className="shrink-0 rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          {pending ? "Enviando…" : "📧 Reenviar"}
        </button>
      </form>
    </div>
  );
}
