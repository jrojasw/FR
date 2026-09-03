"use client";

import { useActionState } from "react";
import { pinLoginAction, type PinLoginState } from "@/app/login/actions";

const initialState: PinLoginState = {};

export function PinLoginForm({ userId }: { userId: string }) {
  const [state, formAction, pending] = useActionState(pinLoginAction, initialState);

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="userId" value={userId} />
      <div>
        <label htmlFor="pin" className="block text-sm font-medium text-slate-700">
          PIN
        </label>
        <input
          id="pin"
          name="pin"
          type="password"
          inputMode="numeric"
          pattern="\d{4,6}"
          maxLength={6}
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
        {pending ? "Ingresando…" : "Ingresar"}
      </button>
    </form>
  );
}
