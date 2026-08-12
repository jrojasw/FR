"use client";

import { useActionState, useRef, useState } from "react";
import {
  sendPaymentCertificateAction,
  type SendPaymentCertificateState,
} from "@/app/aprobaciones/actions";

const initialState: SendPaymentCertificateState = {};

export function PaymentCertificateForm({
  reportId,
  initialFileName,
  alreadyPaid,
}: {
  reportId: string;
  initialFileName: string | null;
  alreadyPaid: boolean;
}) {
  const [fileName, setFileName] = useState(initialFileName);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const boundAction = sendPaymentCertificateAction.bind(null, reportId);
  const [state, formAction, pending] = useActionState(boundAction, initialState);

  async function handleFile(file: File | undefined) {
    if (!file) return;
    setUploadError(null);
    setUploading(true);
    const formData = new FormData();
    formData.set("file", file);
    try {
      const res = await fetch(`/api/rendiciones/${reportId}/certificado-pago`, {
        method: "POST",
        body: formData,
      });
      const data = await res.json();
      if (!res.ok) {
        setUploadError(data.error ?? "No se pudo subir el certificado.");
        return;
      }
      setFileName(data.fileName);
    } catch {
      setUploadError("No se pudo subir el certificado. Revisa tu conexión.");
    } finally {
      setUploading(false);
    }
  }

  if (alreadyPaid) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Certificado de pago</h2>
        <p className="mt-2 text-sm text-emerald-700">
          Certificado enviado a contabilidad.{" "}
          {fileName ? (
            <a
              href={`/api/rendiciones/${reportId}/certificado-pago`}
              target="_blank"
              rel="noreferrer"
              className="underline"
            >
              Ver certificado
            </a>
          ) : null}
        </p>
      </section>
    );
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">Certificado de pago del banco</h2>
      <p className="mt-1 text-sm text-slate-500">
        Sube el certificado de pago (PDF o imagen). Cuando esté cargado podrás enviarlo a
        contabilidad y cerrar la rendición.
      </p>

      <input
        ref={fileInputRef}
        type="file"
        accept="application/pdf,image/*"
        className="hidden"
        onChange={(e) => handleFile(e.target.files?.[0])}
      />

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="button"
          disabled={uploading}
          onClick={() => fileInputRef.current?.click()}
          className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          {fileName ? "Reemplazar archivo" : "Subir certificado"}
        </button>
        {fileName ? (
          <a
            href={`/api/rendiciones/${reportId}/certificado-pago`}
            target="_blank"
            rel="noreferrer"
            className="text-sm text-slate-600 underline"
          >
            {fileName}
          </a>
        ) : (
          <span className="text-sm text-slate-400">Sin certificado cargado</span>
        )}
        {uploading ? <span className="text-sm text-slate-400">Subiendo…</span> : null}
      </div>

      {uploadError ? <p className="mt-2 text-sm text-red-600">{uploadError}</p> : null}

      <form action={formAction} className="mt-4">
        {state?.error ? <p className="mb-2 text-sm text-red-600">{state.error}</p> : null}
        <button
          type="submit"
          disabled={!fileName || pending}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-40"
        >
          {pending ? "Enviando…" : "Enviar certificado y marcar como pagada"}
        </button>
      </form>
    </section>
  );
}
