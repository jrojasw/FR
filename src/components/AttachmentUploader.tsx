"use client";

import { useRef, useState } from "react";
import { MAX_ATTACHMENTS } from "@/lib/reports";

type Attachment = {
  id: string;
  fileName: string;
  mimeType: string;
  kind: "PHOTO" | "DOCUMENT";
};

export function AttachmentUploader({
  reportId,
  initialAttachments,
}: {
  reportId: string;
  initialAttachments: Attachment[];
}) {
  const [attachments, setAttachments] = useState<Attachment[]>(initialAttachments);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [preview, setPreview] = useState<Attachment | null>(null);

  const cameraInputRef = useRef<HTMLInputElement>(null);
  const galleryInputRef = useRef<HTMLInputElement>(null);
  const docInputRef = useRef<HTMLInputElement>(null);

  async function handleFiles(files: FileList | null) {
    if (!files || files.length === 0) return;
    setError(null);

    if (attachments.length + files.length > MAX_ATTACHMENTS) {
      setError(`Máximo ${MAX_ATTACHMENTS} adjuntos por rendición.`);
      return;
    }

    const formData = new FormData();
    Array.from(files).forEach((file) => formData.append("files", file));

    setUploading(true);
    try {
      const res = await fetch(`/api/rendiciones/${reportId}/adjuntos`, {
        method: "POST",
        body: formData,
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error ?? "No se pudo subir el archivo.");
        return;
      }
      setAttachments((prev) => [...prev, ...data.attachments]);
    } catch {
      setError("No se pudo subir el archivo. Revisa tu conexión.");
    } finally {
      setUploading(false);
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    const res = await fetch(`/api/rendiciones/${reportId}/adjuntos/${id}`, { method: "DELETE" });
    if (res.ok) {
      setAttachments((prev) => prev.filter((a) => a.id !== id));
    } else {
      setError("No se pudo eliminar el adjunto.");
    }
  }

  return (
    <div>
      <div className="flex flex-wrap gap-2">
        <input
          ref={cameraInputRef}
          type="file"
          accept="image/*"
          capture="environment"
          multiple
          className="hidden"
          onChange={(e) => handleFiles(e.target.files)}
        />
        <input
          ref={galleryInputRef}
          type="file"
          accept="image/*"
          multiple
          className="hidden"
          onChange={(e) => handleFiles(e.target.files)}
        />
        <input
          ref={docInputRef}
          type="file"
          accept="application/pdf,.doc,.docx"
          multiple
          className="hidden"
          onChange={(e) => handleFiles(e.target.files)}
        />

        <button
          type="button"
          disabled={uploading}
          onClick={() => cameraInputRef.current?.click()}
          className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          📷 Tomar foto
        </button>
        <button
          type="button"
          disabled={uploading}
          onClick={() => galleryInputRef.current?.click()}
          className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          🖼️ Elegir de galería
        </button>
        <button
          type="button"
          disabled={uploading}
          onClick={() => docInputRef.current?.click()}
          className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          📄 Subir documento
        </button>
      </div>

      <p className="mt-2 text-xs text-slate-500">
        {attachments.length}/{MAX_ATTACHMENTS} adjuntos {uploading ? "· subiendo…" : ""}
      </p>

      {error ? <p className="mt-1 text-sm text-red-600">{error}</p> : null}

      {attachments.length > 0 && (
        <ul className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {attachments.map((a) => (
            <li key={a.id} className="relative overflow-hidden rounded-md border border-slate-200 bg-white">
              <button
                type="button"
                onClick={() => setPreview(a)}
                className="block w-full"
                aria-label={`Ver ${a.fileName}`}
              >
                {a.kind === "PHOTO" ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={`/api/adjuntos/${a.id}`}
                    alt={a.fileName}
                    className="h-24 w-full object-cover"
                  />
                ) : (
                  <div className="flex h-24 w-full flex-col items-center justify-center gap-1 bg-slate-50 px-2 text-center">
                    <span className="text-2xl">📄</span>
                    <span className="line-clamp-1 text-xs text-slate-600">{a.fileName}</span>
                  </div>
                )}
              </button>
              <button
                type="button"
                onClick={() => handleDelete(a.id)}
                className="absolute right-1 top-1 flex h-6 w-6 items-center justify-center rounded-full bg-black/60 text-xs text-white hover:bg-black/80"
                aria-label={`Eliminar ${a.fileName}`}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      )}

      {preview && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4"
          onClick={() => setPreview(null)}
        >
          <div
            className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-lg bg-white"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-slate-200 px-4 py-2">
              <span className="truncate text-sm font-medium text-slate-700">{preview.fileName}</span>
              <button
                type="button"
                onClick={() => setPreview(null)}
                className="ml-2 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-slate-500 hover:bg-slate-100"
                aria-label="Cerrar"
              >
                ×
              </button>
            </div>
            <div className="flex-1 overflow-auto bg-slate-50">
              {preview.kind === "PHOTO" ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={`/api/adjuntos/${preview.id}`}
                  alt={preview.fileName}
                  className="mx-auto max-h-[80vh] w-auto object-contain"
                />
              ) : preview.mimeType === "application/pdf" ? (
                <iframe src={`/api/adjuntos/${preview.id}`} title={preview.fileName} className="h-[80vh] w-full" />
              ) : (
                <div className="flex h-64 flex-col items-center justify-center gap-3 px-4 text-center">
                  <p className="text-sm text-slate-600">
                    Este tipo de archivo no se puede previsualizar aquí.
                  </p>
                  <a
                    href={`/api/adjuntos/${preview.id}`}
                    target="_blank"
                    rel="noreferrer"
                    className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
                  >
                    Abrir en una pestaña nueva
                  </a>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
