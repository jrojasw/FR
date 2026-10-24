"use client";

import { documentTypeLabels } from "@/lib/format";
import type { DuplicateDocumentMatch } from "@/lib/duplicate-documents";

export function DuplicateDocumentModal({
  match,
  numeroDocumento,
  acknowledging,
  onCorrect,
  onAcceptAndContinue,
}: {
  match: Extract<DuplicateDocumentMatch, { kind: "exact" | "number" }>;
  numeroDocumento: string;
  acknowledging: boolean;
  onCorrect: () => void;
  onAcceptAndContinue: () => void;
}) {
  const isExact = match.kind === "exact";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" onClick={onCorrect}>
      <div className="w-full max-w-sm rounded-lg bg-white p-6" onClick={(e) => e.stopPropagation()}>
        <div
          className={`mx-auto flex h-12 w-12 items-center justify-center rounded-full text-2xl ${
            isExact ? "bg-red-100" : "bg-amber-100"
          }`}
        >
          {isExact ? "⛔" : "⚠️"}
        </div>
        <h3 className="mt-4 text-center text-base font-semibold text-slate-900">
          {isExact ? "Este documento ya fue rendido" : "Posible documento repetido"}
        </h3>
        <p className="mt-2 text-center text-sm text-slate-600">
          {isExact ? (
            <>
              El N° <strong>{numeroDocumento}</strong> ya está registrado con el mismo tipo de documento en la{" "}
              <a
                href={`/aprobaciones/${match.matchedReportId}`}
                target="_blank"
                rel="noreferrer"
                className="font-medium text-slate-900 underline"
              >
                Rendición N° {match.matchedCorrelativo}
              </a>
              . No se puede rendir el mismo documento dos veces.
            </>
          ) : (
            <>
              Ya existe un documento con el N° <strong>{numeroDocumento}</strong> en el sistema, registrado como{" "}
              {documentTypeLabels[match.matchedTipoDocumento] ?? match.matchedTipoDocumento} en la{" "}
              <a
                href={`/aprobaciones/${match.matchedReportId}`}
                target="_blank"
                rel="noreferrer"
                className="font-medium text-slate-900 underline"
              >
                Rendición N° {match.matchedCorrelativo}
              </a>
              . Revisa si es el mismo documento antes de continuar.
            </>
          )}
        </p>

        <div className="mt-5 flex flex-col gap-2">
          <button
            type="button"
            onClick={onCorrect}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Corregir el número
          </button>
          {!isExact && (
            <button
              type="button"
              disabled={acknowledging}
              onClick={onAcceptAndContinue}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-60"
            >
              {acknowledging ? "Guardando…" : "Sí, es correcto, continuar"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
