"use client";

import { useActionState, useState } from "react";
import { createEntryAction, updateEntryAction, type EntryFormState } from "@/app/horas/actions";

const initialState: EntryFormState = {};

type Props = {
  mode: "create" | "edit";
  entryId?: string;
  initial?: {
    tipo: "TURNO_DOMINGO" | "HORAS_EXTRA";
    fecha: string;
    horaInicio?: string;
    horaFin?: string;
    motivo?: string;
  };
};

export function OvertimeEntryForm({ mode, entryId, initial }: Props) {
  const action = mode === "edit" && entryId ? updateEntryAction.bind(null, entryId) : createEntryAction;
  const [state, formAction, pending] = useActionState(action, initialState);
  const [tipo, setTipo] = useState<"TURNO_DOMINGO" | "HORAS_EXTRA">(initial?.tipo ?? "HORAS_EXTRA");

  return (
    <form action={formAction} className="space-y-5 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <div>
        <span className="block text-sm font-medium text-slate-700">Tipo de registro</span>
        <div className="mt-2 flex flex-col gap-2 sm:flex-row">
          <label className="flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm has-checked:border-slate-900 has-checked:bg-slate-50">
            <input
              type="radio"
              name="tipo"
              value="HORAS_EXTRA"
              checked={tipo === "HORAS_EXTRA"}
              onChange={() => setTipo("HORAS_EXTRA")}
            />
            Horas extra semana
          </label>
          <label className="flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm has-checked:border-slate-900 has-checked:bg-slate-50">
            <input
              type="radio"
              name="tipo"
              value="TURNO_DOMINGO"
              checked={tipo === "TURNO_DOMINGO"}
              onChange={() => setTipo("TURNO_DOMINGO")}
            />
            Turno domingo (aseo)
          </label>
        </div>
      </div>

      <div>
        <label htmlFor="fecha" className="block text-sm font-medium text-slate-700">
          Fecha
        </label>
        <input
          id="fecha"
          name="fecha"
          type="date"
          required
          defaultValue={initial?.fecha}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none sm:w-auto"
        />
        {tipo === "TURNO_DOMINGO" && (
          <p className="mt-1 text-xs text-slate-500">Debe ser un día domingo.</p>
        )}
      </div>

      {tipo === "HORAS_EXTRA" && (
        <div className="flex flex-wrap gap-4">
          <div>
            <label htmlFor="horaInicio" className="block text-sm font-medium text-slate-700">
              Hora inicio
            </label>
            <input
              id="horaInicio"
              name="horaInicio"
              type="time"
              required={tipo === "HORAS_EXTRA"}
              defaultValue={initial?.horaInicio}
              className="mt-1 rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
            />
          </div>
          <div>
            <label htmlFor="horaFin" className="block text-sm font-medium text-slate-700">
              Hora término
            </label>
            <input
              id="horaFin"
              name="horaFin"
              type="time"
              required={tipo === "HORAS_EXTRA"}
              defaultValue={initial?.horaFin}
              className="mt-1 rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
            />
          </div>
        </div>
      )}

      <div>
        <label htmlFor="motivo" className="block text-sm font-medium text-slate-700">
          Motivo o tarea realizada {tipo === "TURNO_DOMINGO" ? "(opcional)" : ""}
        </label>
        <textarea
          id="motivo"
          name="motivo"
          rows={3}
          required={tipo === "HORAS_EXTRA"}
          defaultValue={initial?.motivo}
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
        />
      </div>

      <p className="text-xs text-slate-500">
        Recuerda marcar entrada y salida en el reloj biométrico: la administradora valida cada
        registro contra esa marcación antes de aprobarlo.
      </p>

      {state?.error ? (
        <p role="alert" className="text-sm text-red-600">
          {state.error}
        </p>
      ) : null}

      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-60"
      >
        {pending ? "Guardando…" : mode === "edit" ? "Guardar cambios" : "Registrar"}
      </button>
    </form>
  );
}
