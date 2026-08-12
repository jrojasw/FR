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
    </div>
  );
}
