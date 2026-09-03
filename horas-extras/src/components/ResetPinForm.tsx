"use client";

import { useActionState, useRef, useEffect } from "react";
import { resetPinAction, type ColaboradoraFormState } from "@/app/admin/usuarios/actions";

const initialState: ColaboradoraFormState = {};

export function ResetPinForm({ userId }: { userId: string }) {
  const [state, formAction, pending] = useActionState(resetPinAction, initialState);
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    if (!pending && !state?.error) formRef.current?.reset();
  }, [pending, state]);

  return (
    <form ref={formRef} action={formAction} className="flex items-center gap-2">
      <input type="hidden" name="userId" value={userId} />
      <input
        name="pin"
        type="text"
        inputMode="numeric"
        pattern="\d{4,6}"
        maxLength={6}
        required
        placeholder="Nuevo PIN"
        className="w-28 rounded-md border border-slate-300 px-2 py-1 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
      />
      <button
        type="submit"
        disabled={pending}
        className="rounded-md border border-slate-300 bg-white px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        Restablecer
      </button>
      {state?.error ? <p className="text-xs text-red-600">{state.error}</p> : null}
    </form>
  );
}
