"use client";

import { useRef, useState, useCallback } from "react";
import {
  loadOpenCv,
  detectDocumentCorners,
  extractDocument,
  applyScanLook,
  type CornerPoints,
  type Point,
} from "@/lib/document-scan";

type CornerKey = "topLeftCorner" | "topRightCorner" | "bottomLeftCorner" | "bottomRightCorner";

const CORNER_ORDER: CornerKey[] = ["topLeftCorner", "topRightCorner", "bottomRightCorner", "bottomLeftCorner"];

function defaultCorners(width: number, height: number): CornerPoints {
  const marginX = width * 0.08;
  const marginY = height * 0.08;
  return {
    topLeftCorner: { x: marginX, y: marginY },
    topRightCorner: { x: width - marginX, y: marginY },
    bottomLeftCorner: { x: marginX, y: height - marginY },
    bottomRightCorner: { x: width - marginX, y: height - marginY },
  };
}

export function DocumentScanner({ onScanned }: { onScanned: (file: File) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);
  const frameRef = useRef<HTMLDivElement>(null);

  const [status, setStatus] = useState<"idle" | "loading-engine" | "reviewing" | "processing">("idle");
  const [error, setError] = useState<string | null>(null);
  const [imageSrc, setImageSrc] = useState<string | null>(null);
  const [naturalSize, setNaturalSize] = useState({ width: 1, height: 1 });
  const [corners, setCorners] = useState<CornerPoints | null>(null);
  const draggingRef = useRef<CornerKey | null>(null);

  async function handleFileSelected(file: File) {
    setError(null);
    setStatus("loading-engine");
    try {
      await loadOpenCv();
    } catch {
      setError("No se pudo cargar el motor de escaneo. Revisa tu conexión e intenta de nuevo.");
      setStatus("idle");
      return;
    }

    const src = URL.createObjectURL(file);
    setImageSrc(src);
    setStatus("reviewing");
  }

  function handleImageLoaded() {
    const img = imgRef.current;
    if (!img) return;
    const width = img.naturalWidth;
    const height = img.naturalHeight;
    setNaturalSize({ width, height });

    let detected: CornerPoints | null = null;
    try {
      detected = detectDocumentCorners(img);
    } catch {
      detected = null;
    }
    setCorners(detected ?? defaultCorners(width, height));
  }

  const updateCornerFromEvent = useCallback(
    (key: CornerKey, clientX: number, clientY: number) => {
      const frame = frameRef.current;
      if (!frame) return;
      const rect = frame.getBoundingClientRect();
      const xFrac = Math.min(1, Math.max(0, (clientX - rect.left) / rect.width));
      const yFrac = Math.min(1, Math.max(0, (clientY - rect.top) / rect.height));
      setCorners((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          [key]: { x: xFrac * naturalSize.width, y: yFrac * naturalSize.height },
        };
      });
    },
    [naturalSize.width, naturalSize.height]
  );

  function startDrag(key: CornerKey) {
    draggingRef.current = key;
  }

  function handlePointerMove(e: React.PointerEvent) {
    if (!draggingRef.current) return;
    updateCornerFromEvent(draggingRef.current, e.clientX, e.clientY);
  }

  function handlePointerUp() {
    draggingRef.current = null;
  }

  async function handleConfirm() {
    const img = imgRef.current;
    if (!img || !corners) return;
    setStatus("processing");
    try {
      const width = Math.round(
        Math.max(
          distance(corners.topLeftCorner, corners.topRightCorner),
          distance(corners.bottomLeftCorner, corners.bottomRightCorner)
        )
      );
      const height = Math.round(
        Math.max(
          distance(corners.topLeftCorner, corners.bottomLeftCorner),
          distance(corners.topRightCorner, corners.bottomRightCorner)
        )
      );
      const canvas = extractDocument(img, corners, Math.max(width, 200), Math.max(height, 200));
      applyScanLook(canvas);

      const blob: Blob | null = await new Promise((resolve) => canvas.toBlob((b) => resolve(b), "image/jpeg", 0.9));
      if (!blob) throw new Error("No se pudo generar la imagen");

      const file = new File([blob], `escaneo-${Date.now()}.jpg`, { type: "image/jpeg" });
      onScanned(file);
      closeReview();
    } catch {
      setError("No se pudo procesar el escaneo. Intenta de nuevo.");
      setStatus("reviewing");
    }
  }

  function closeReview() {
    if (imageSrc) URL.revokeObjectURL(imageSrc);
    setImageSrc(null);
    setCorners(null);
    setStatus("idle");
    if (inputRef.current) inputRef.current.value = "";
  }

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) handleFileSelected(file);
        }}
      />
      <button
        type="button"
        disabled={status === "loading-engine"}
        onClick={() => inputRef.current?.click()}
        className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        {status === "loading-engine" ? "Cargando escáner…" : "📄 Escanear documento"}
      </button>

      {error && !imageSrc ? <p className="mt-2 text-sm text-red-600">{error}</p> : null}

      {imageSrc && (
        <div className="fixed inset-0 z-50 flex flex-col bg-black/90 p-4">
          <div className="flex items-center justify-between text-white">
            <span className="text-sm font-medium">Ajusta las esquinas del documento</span>
            <button type="button" onClick={closeReview} className="text-xl leading-none">
              ×
            </button>
          </div>

          <div className="mt-3 flex flex-1 items-center justify-center overflow-hidden">
            <div
              ref={frameRef}
              className="relative max-h-full max-w-full touch-none select-none"
              onPointerMove={handlePointerMove}
              onPointerUp={handlePointerUp}
              onPointerLeave={handlePointerUp}
            >
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                ref={imgRef}
                src={imageSrc}
                alt="Documento a escanear"
                onLoad={handleImageLoaded}
                className="block max-h-[70vh] max-w-full select-none"
                draggable={false}
              />

              {corners && naturalSize.width > 1 && (
                <svg
                  className="pointer-events-none absolute inset-0 h-full w-full"
                  viewBox={`0 0 ${naturalSize.width} ${naturalSize.height}`}
                  preserveAspectRatio="none"
                >
                  <polygon
                    points={CORNER_ORDER.map((k) => `${corners[k].x},${corners[k].y}`).join(" ")}
                    fill="rgba(44,168,222,0.25)"
                    stroke="#2CA8DE"
                    strokeWidth={naturalSize.width * 0.006}
                  />
                </svg>
              )}

              {corners &&
                CORNER_ORDER.map((key) => (
                  <div
                    key={key}
                    onPointerDown={(e) => {
                      e.currentTarget.setPointerCapture(e.pointerId);
                      startDrag(key);
                    }}
                    style={{
                      left: `${(corners[key].x / naturalSize.width) * 100}%`,
                      top: `${(corners[key].y / naturalSize.height) * 100}%`,
                    }}
                    className="absolute h-8 w-8 -translate-x-1/2 -translate-y-1/2 touch-none rounded-full border-4 border-white bg-[#2CA8DE] shadow-lg"
                  />
                ))}
            </div>
          </div>

          {error ? <p className="mt-2 text-center text-sm text-red-400">{error}</p> : null}

          <div className="mt-3 flex justify-center gap-3">
            <button
              type="button"
              onClick={closeReview}
              className="rounded-md border border-slate-500 px-4 py-2 text-sm font-medium text-white hover:bg-white/10"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={status === "processing"}
              onClick={handleConfirm}
              className="rounded-md bg-[#2CA8DE] px-4 py-2 text-sm font-semibold text-white hover:bg-[#1c86b8] disabled:opacity-60"
            >
              {status === "processing" ? "Procesando…" : "Confirmar escaneo"}
            </button>
          </div>
        </div>
      )}
    </>
  );
}

function distance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}
