"use client";

import { useActionState } from "react";
import { verifyOtpAction, type VerifyOtpState } from "@/app/login/verificar/actions";

const initialState: VerifyOtpState = {};

export function VerifyOtpForm({ email, next }: { email: string; next?: string }) {
  const [state, formAction, pending] = useActionState(verifyOtpAction, initialState);

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="email" value={email} />
      {next ? <input type="hidden" name="next" value={next} /> : null}
      <div>
        <label htmlFor="code" className="block text-sm font-medium text-slate-700">
          Código de 4 dígitos
        </label>
        <input
          id="code"
          name="code"
          type="text"
          inputMode="numeric"
          pattern="\d{4}"
          maxLength={4}
          required
          autoFocus
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-center text-2xl tracking-[0.5em] shadow-sm focus:border-slate-500 focus:outline-none"
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
        {pending ? "Verificando…" : "Ingresar"}
      </button>
    </form>
  );
}
