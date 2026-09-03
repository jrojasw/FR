"use client";

import { useActionState, useRef, useEffect } from "react";
import {
  createColaboradoraAction,
  type ColaboradoraFormState,
} from "@/app/admin/usuarios/actions";

const initialState: ColaboradoraFormState = {};

export function CreateColaboradoraForm() {
  const [state, formAction, pending] = useActionState(createColaboradoraAction, initialState);
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    if (!pending && !state?.error) formRef.current?.reset();
  }, [pending, state]);

  return (
    <form ref={formRef} action={formAction} className="flex flex-wrap items-end gap-3">
      <div>
        <label htmlFor="name" className="block text-xs font-medium text-slate-700">
          Nombre
        </label>
        <input
          id="name"
          name="name"
          type="text"
          required
          className="mt-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>
      <div>
        <label htmlFor="pin" className="block text-xs font-medium text-slate-700">
          PIN (4-6 dígitos)
        </label>
        <input
          id="pin"
          name="pin"
          type="text"
          inputMode="numeric"
          pattern="\d{4,6}"
          maxLength={6}
          required
          className="mt-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>
      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-slate-900 px-4 py-1.5 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-60"
      >
        {pending ? "Creando…" : "Agregar colaboradora"}
      </button>
      {state?.error ? <p className="w-full text-sm text-red-600">{state.error}</p> : null}
    </form>
  );
}
