"use client";

import { useRef, useState } from "react";
import type { PointerEvent } from "react";

export function SignaturePad({
  onChange,
  initialDataUrl,
}: {
  onChange: (dataUrl: string) => void;
  initialDataUrl?: string;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const [hasSignature, setHasSignature] = useState(false);
  // Si ya hay una firma guardada (ej. al editar una rendición), se muestra
  // como imagen en vez del lienzo en blanco, para no obligar a re-firmar
  // solo por corregir otro dato.
  const [showExisting, setShowExisting] = useState(Boolean(initialDataUrl));

  function pointerPos(e: PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    return {
      x: (e.clientX - rect.left) * (canvas.width / rect.width),
      y: (e.clientY - rect.top) * (canvas.height / rect.height),
    };
  }

  function handlePointerDown(e: PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    drawing.current = true;
    const { x, y } = pointerPos(e);
    ctx.beginPath();
    ctx.moveTo(x, y);
    canvas.setPointerCapture(e.pointerId);
  }

  function handlePointerMove(e: PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    const ctx = canvasRef.current?.getContext("2d");
    if (!ctx) return;
    const { x, y } = pointerPos(e);
    ctx.lineWidth = 2.5;
    ctx.lineCap = "round";
    ctx.strokeStyle = "#0f172a";
    ctx.lineTo(x, y);
    ctx.stroke();
    setHasSignature(true);
  }

  function handlePointerUp() {
    if (!drawing.current) return;
    drawing.current = false;
    const canvas = canvasRef.current;
    if (canvas) onChange(canvas.toDataURL("image/png"));
  }

  function clear() {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    setHasSignature(false);
    onChange("");
  }

  function changeSignature() {
    setShowExisting(false);
    clear();
  }

  if (showExisting && initialDataUrl) {
    return (
      <div>
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={initialDataUrl}
          alt="Firma actual"
          className="h-64 w-full rounded-md border border-slate-300 bg-white object-contain"
        />
        <div className="mt-2 flex items-center justify-between">
          <p className="text-xs text-slate-500">Firma actual</p>
          <button
            type="button"
            onClick={changeSignature}
            className="text-xs font-medium text-slate-600 hover:text-slate-900"
          >
            Cambiar firma
          </button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <canvas
        ref={canvasRef}
        width={600}
        height={320}
        className="h-64 w-full touch-none rounded-md border border-slate-300 bg-white"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerUp}
      />
      <div className="mt-2 flex items-center justify-between">
        <p className="text-xs text-slate-500">{hasSignature ? "Firma capturada" : "Firma con el dedo o el mouse"}</p>
        <button
          type="button"
          onClick={clear}
          className="text-xs font-medium text-slate-600 hover:text-slate-900"
        >
          Borrar firma
        </button>
      </div>
    </div>
  );
}
