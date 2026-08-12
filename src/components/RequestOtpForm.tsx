"use client";

import { useActionState } from "react";
import { requestOtpAction, type RequestOtpState } from "@/app/login/actions";

const initialState: RequestOtpState = {};

export function RequestOtpForm({ next }: { next?: string }) {
  const [state, formAction, pending] = useActionState(requestOtpAction, initialState);

  return (
    <form action={formAction} className="space-y-4">
      {next ? <input type="hidden" name="next" value={next} /> : null}
      <div>
        <label htmlFor="email" className="block text-sm font-medium text-slate-700">
          Correo
        </label>
        <input
          id="email"
          name="email"
          type="email"
          required
          autoComplete="email"
          inputMode="email"
          placeholder="nombre@copayapunos.cl"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>
      {state?.error ? (
        <p role="alert" className="text-sm text-red-600">
          {state.error}
        </p>
      ) : null}
      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-700 disabled:opacity-60"
      >
        {pending ? "Enviando código…" : "Enviar código de acceso"}
      </button>
    </form>
  );
}
