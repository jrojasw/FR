"use client";

import { useActionState, useRef, useState } from "react";
import type { DragEvent } from "react";
import {
  sendPaymentCertificateAction,
  type SendPaymentCertificateState,
} from "@/app/aprobaciones/actions";

const initialState: SendPaymentCertificateState = {};

type AdditionalCertificate = { id: string; fileName: string };

export function PaymentCertificateForm({
  reportId,
  initialFileName,
  alreadyPaid,
  initialAdditionalCertificates = [],
}: {
  reportId: string;
  initialFileName: string | null;
  alreadyPaid: boolean;
  initialAdditionalCertificates?: AdditionalCertificate[];
}) {
  const [fileName, setFileName] = useState(initialFileName);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  const dragCounter = useRef(0);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Comprobantes adicionales: para cuando el certificado ya enviado tenía un
  // error y ya se hizo otra transferencia para corregirlo. Se agregan, no
  // reemplazan al original.
  const [additionalCertificates, setAdditionalCertificates] = useState<AdditionalCertificate[]>(
    initialAdditionalCertificates
  );
  const [pendingAdditionalFile, setPendingAdditionalFile] = useState<File | null>(null);
  const [addingAdditional, setAddingAdditional] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [dragActiveAdd, setDragActiveAdd] = useState(false);
  const dragCounterAdd = useRef(0);
  const additionalFileInputRef = useRef<HTMLInputElement>(null);

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

  function handleDragEnter(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    if (e.dataTransfer.types.includes("Files")) {
      dragCounter.current += 1;
      setDragActive(true);
    }
  }

  function handleDragOver(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
  }

  function handleDragLeave(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    dragCounter.current = Math.max(0, dragCounter.current - 1);
    if (dragCounter.current === 0) setDragActive(false);
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    dragCounter.current = 0;
    setDragActive(false);
    handleFile(e.dataTransfer.files[0]);
  }

  // Al reemplazar un certificado ya enviado, el archivo se deja pendiente
  // en vez de subirlo de inmediato: hay que confirmar con "Aceptar
  // reemplazo" para no corregir un documento ya entregado por error.
  function stageFile(file: File | undefined) {
    if (!file) return;
    setUploadError(null);
    setPendingFile(file);
  }

  function handleDropStage(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    dragCounter.current = 0;
    setDragActive(false);
    stageFile(e.dataTransfer.files[0]);
  }

  function cancelReplace() {
    setPendingFile(null);
    setUploadError(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  async function confirmReplace() {
    if (!pendingFile) return;
    await handleFile(pendingFile);
    setPendingFile(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  function stageAdditionalFile(file: File | undefined) {
    if (!file) return;
    setAddError(null);
    setPendingAdditionalFile(file);
  }

  function handleDragEnterAdd(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    if (e.dataTransfer.types.includes("Files")) {
      dragCounterAdd.current += 1;
      setDragActiveAdd(true);
    }
  }

  function handleDragLeaveAdd(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    dragCounterAdd.current = Math.max(0, dragCounterAdd.current - 1);
    if (dragCounterAdd.current === 0) setDragActiveAdd(false);
  }

  function handleDropAdd(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    dragCounterAdd.current = 0;
    setDragActiveAdd(false);
    stageAdditionalFile(e.dataTransfer.files[0]);
  }

  function cancelAddAdditional() {
    setPendingAdditionalFile(null);
    setAddError(null);
    if (additionalFileInputRef.current) additionalFileInputRef.current.value = "";
  }

  async function confirmAddAdditional() {
    if (!pendingAdditionalFile) return;
    setAddError(null);
    setAddingAdditional(true);
    const formData = new FormData();
    formData.set("file", pendingAdditionalFile);
    try {
      const res = await fetch(`/api/rendiciones/${reportId}/certificados-pago`, {
        method: "POST",
        body: formData,
      });
      const data = await res.json();
      if (!res.ok) {
        setAddError(data.error ?? "No se pudo subir el comprobante.");
        return;
      }
      setAdditionalCertificates((prev) => [...prev, { id: data.id, fileName: data.fileName }]);
      setPendingAdditionalFile(null);
      if (additionalFileInputRef.current) additionalFileInputRef.current.value = "";
    } catch {
      setAddError("No se pudo subir el comprobante. Revisa tu conexión.");
    } finally {
      setAddingAdditional(false);
    }
  }

  async function handleDeleteAdditional(id: string) {
    setAddError(null);
    const res = await fetch(`/api/rendiciones/${reportId}/certificados-pago/${id}`, { method: "DELETE" });
    if (res.ok) {
      setAdditionalCertificates((prev) => prev.filter((c) => c.id !== id));
    } else {
      setAddError("No se pudo eliminar el comprobante.");
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

        <div
          onDragEnter={handleDragEnter}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDropStage}
          className={`mt-3 rounded-md border-2 border-dashed p-3 transition-colors ${
            dragActive ? "border-[#004b93] bg-blue-50" : "border-transparent"
          }`}
        >
          <input
            ref={fileInputRef}
            type="file"
            accept="application/pdf,image/*"
            className="hidden"
            onChange={(e) => stageFile(e.target.files?.[0])}
          />
          <button
            type="button"
            disabled={uploading}
            onClick={() => fileInputRef.current?.click()}
            className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
          >
            Reemplazar certificado
          </button>
          <p className="mt-1 hidden text-xs text-slate-400 sm:block">
            {dragActive ? "Suelta aquí para subir" : "…o arrastra y suelta el archivo aquí"}
          </p>
        </div>

        {pendingFile ? (
          <div className="mt-3 flex flex-wrap items-center gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2">
            <span className="text-sm text-amber-800">Nuevo archivo: {pendingFile.name}</span>
            <button
              type="button"
              disabled={uploading}
              onClick={confirmReplace}
              className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-60"
            >
              {uploading ? "Subiendo…" : "Aceptar reemplazo"}
            </button>
            <button
              type="button"
              disabled={uploading}
              onClick={cancelReplace}
              className="text-sm text-slate-500 hover:text-slate-700 disabled:opacity-60"
            >
              Cancelar
            </button>
          </div>
        ) : null}

        <p className="mt-1 text-xs text-slate-500">
          Solo corrige el archivo guardado (se usará en el próximo PDF descargado); no reenvía
          correos automáticamente.
        </p>

        {uploadError ? <p className="mt-2 text-sm text-red-600">{uploadError}</p> : null}

        <div className="mt-5 border-t border-slate-100 pt-4">
          <h3 className="text-sm font-semibold text-slate-900">Comprobantes de pago adicionales</h3>
          <p className="mt-1 text-xs text-slate-500">
            Para cuando el certificado de arriba tenía un error y ya se hizo otra transferencia
            para corregirlo: agrega el comprobante nuevo sin perder el registro del original.
          </p>

          {additionalCertificates.length > 0 ? (
            <ul className="mt-2 space-y-1">
              {additionalCertificates.map((cert) => (
                <li key={cert.id} className="flex items-center gap-2 text-sm">
                  <a
                    href={`/api/rendiciones/${reportId}/certificados-pago/${cert.id}`}
                    target="_blank"
                    rel="noreferrer"
                    className="text-slate-700 underline"
                  >
                    {cert.fileName}
                  </a>
                  <button
                    type="button"
                    onClick={() => handleDeleteAdditional(cert.id)}
                    className="text-xs text-red-600 hover:text-red-800"
                  >
                    Eliminar
                  </button>
                </li>
              ))}
            </ul>
          ) : null}

          <div
            onDragEnter={handleDragEnterAdd}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeaveAdd}
            onDrop={handleDropAdd}
            className={`mt-3 rounded-md border-2 border-dashed p-3 transition-colors ${
              dragActiveAdd ? "border-[#004b93] bg-blue-50" : "border-transparent"
            }`}
          >
            <input
              ref={additionalFileInputRef}
              type="file"
              accept="application/pdf,image/*"
              className="hidden"
              onChange={(e) => stageAdditionalFile(e.target.files?.[0])}
            />
            <button
              type="button"
              disabled={addingAdditional}
              onClick={() => additionalFileInputRef.current?.click()}
              className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
            >
              + Agregar otro comprobante
            </button>
            <p className="mt-1 hidden text-xs text-slate-400 sm:block">
              {dragActiveAdd ? "Suelta aquí para subir" : "…o arrastra y suelta el archivo aquí"}
            </p>
          </div>

          {pendingAdditionalFile ? (
            <div className="mt-3 flex flex-wrap items-center gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2">
              <span className="text-sm text-amber-800">Nuevo archivo: {pendingAdditionalFile.name}</span>
              <button
                type="button"
                disabled={addingAdditional}
                onClick={confirmAddAdditional}
                className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-60"
              >
                {addingAdditional ? "Subiendo…" : "Agregar comprobante"}
              </button>
              <button
                type="button"
                disabled={addingAdditional}
                onClick={cancelAddAdditional}
                className="text-sm text-slate-500 hover:text-slate-700 disabled:opacity-60"
              >
                Cancelar
              </button>
            </div>
          ) : null}

          {addError ? <p className="mt-2 text-sm text-red-600">{addError}</p> : null}
        </div>
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

      <div
        onDragEnter={handleDragEnter}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`mt-4 rounded-md border-2 border-dashed p-3 transition-colors ${
          dragActive ? "border-[#004b93] bg-blue-50" : "border-transparent"
        }`}
      >
        <div className="flex flex-wrap items-center gap-3">
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
        <p className="mt-1 hidden text-xs text-slate-400 sm:block">
          {dragActive ? "Suelta aquí para subir" : "…o arrastra y suelta el archivo aquí"}
        </p>
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
