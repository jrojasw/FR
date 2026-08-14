"use client";

import { useMemo, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { finalizeReportAction } from "@/app/rendiciones/actions";
import { AttachmentUploader } from "@/components/AttachmentUploader";
import { SignaturePad } from "@/components/SignaturePad";
import { computeTotals } from "@/lib/reports";
import { formatCurrency, documentTypeLabels } from "@/lib/format";
import { formatRut } from "@/lib/rut";

type ItemRow = {
  glosa: string;
  proveedor: string;
  tipoDocumento: "BOLETA" | "FACTURA" | "RECIBO";
  numeroDocumento: string;
  montoTotal: string;
};

type Attachment = { id: string; fileName: string; mimeType: string; kind: "PHOTO" | "DOCUMENT" };

function emptyRow(): ItemRow {
  return { glosa: "", proveedor: "", tipoDocumento: "BOLETA", numeroDocumento: "", montoTotal: "" };
}

export function ReportEditor({
  reportId,
  correlativo,
  initial,
  initialAttachments,
}: {
  reportId: string;
  correlativo: number;
  initial: {
    nombre: string;
    cargo: string;
    fecha: string;
  };
  initialAttachments: Attachment[];
}) {
  const router = useRouter();
  const [nombre, setNombre] = useState(initial.nombre);
  const [cargo, setCargo] = useState(initial.cargo);
  const [fecha, setFecha] = useState(initial.fecha);
  const [items, setItems] = useState<ItemRow[]>([emptyRow()]);
  const [rut, setRut] = useState("");
  const [signatureData, setSignatureData] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const encabezadoRef = useRef<HTMLDivElement>(null);
  const detalleRef = useRef<HTMLDivElement>(null);
  const adjuntosRef = useRef<HTMLDivElement>(null);
  const firmaRef = useRef<HTMLDivElement>(null);

  function failWith(message: string, ref?: React.RefObject<HTMLDivElement | null>) {
    setError(message);
    ref?.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  const totals = useMemo(
    () => computeTotals(items.map((i) => ({ montoTotal: Number(i.montoTotal) || 0 }))),
    [items]
  );

  function updateItem(index: number, patch: Partial<ItemRow>) {
    setItems((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function addItem() {
    setItems((prev) => [...prev, emptyRow()]);
  }

  function removeItem(index: number) {
    setItems((prev) => prev.filter((_, i) => i !== index));
  }

  function handleSubmit() {
    setError(null);

    if (!nombre.trim() || !cargo.trim() || !fecha) {
      failWith("Completa todos los datos del encabezado.", encabezadoRef);
      return;
    }
    const validItems = items.filter(
      (i) => i.glosa.trim() && i.proveedor.trim() && i.numeroDocumento.trim() && i.montoTotal
    );
    if (validItems.length === 0) {
      failWith("Agrega al menos un documento en el detalle (glosa, proveedor, N° documento y monto).", detalleRef);
      return;
    }
    if (!signatureData) {
      failWith("Falta tu firma.", firmaRef);
      return;
    }
    if (!rut.trim()) {
      failWith("Ingresa tu RUT.", firmaRef);
      return;
    }

    const formData = new FormData();
    formData.set("nombre", nombre.trim());
    formData.set("cargo", cargo.trim());
    formData.set("fecha", fecha);
    formData.set("items", JSON.stringify(validItems.map((i) => ({ ...i, montoTotal: Number(i.montoTotal) }))));
    formData.set("rut", rut.trim());
    formData.set("signatureData", signatureData);

    startTransition(async () => {
      const result = await finalizeReportAction(reportId, formData);
      if (result?.error) {
        const ref = /adjunt|comprobante/i.test(result.error) ? adjuntosRef : undefined;
        failWith(result.error, ref);
      } else {
        router.refresh();
      }
    });
  }

  return (
    <div className="space-y-8">
      {error ? (
        <div
          role="alert"
          className="sticky top-2 z-10 rounded-md border border-red-300 bg-red-50 px-4 py-3 text-sm font-medium text-red-800 shadow-md"
        >
          {error}
        </div>
      ) : null}

      <section ref={encabezadoRef} className="scroll-mt-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900">Encabezado</h2>
          <span className="rounded-full bg-slate-100 px-3 py-1 text-sm font-medium text-slate-700">
            N° {correlativo}
          </span>
        </div>
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="block text-sm font-medium text-slate-700">Nombre</label>
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Cargo</label>
            <input
              value={cargo}
              onChange={(e) => setCargo(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Fecha</label>
            <input
              type="date"
              value={fecha}
              onChange={(e) => setFecha(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
            />
          </div>
        </div>
      </section>

      <section ref={detalleRef} className="scroll-mt-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Detalle de documentos</h2>

        {/* Vista móvil: tarjetas con los campos de a dos, sin scroll horizontal */}
        <div className="mt-4 flex flex-col gap-3 sm:hidden">
          {items.map((row, index) => (
            <div key={index} className="rounded-lg border border-slate-200 p-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="mb-1 block text-xs uppercase text-slate-500">Glosa</label>
                  <input
                    value={row.glosa}
                    onChange={(e) => updateItem(index, { glosa: e.target.value })}
                    placeholder="Ej: materiales"
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs uppercase text-slate-500">Proveedor</label>
                  <input
                    value={row.proveedor}
                    onChange={(e) => updateItem(index, { proveedor: e.target.value })}
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs uppercase text-slate-500">Tipo documento</label>
                  <select
                    value={row.tipoDocumento}
                    onChange={(e) => updateItem(index, { tipoDocumento: e.target.value as ItemRow["tipoDocumento"] })}
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  >
                    {Object.entries(documentTypeLabels).map(([value, label]) => (
                      <option key={value} value={value}>
                        {label}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs uppercase text-slate-500">N° documento</label>
                  <input
                    value={row.numeroDocumento}
                    onChange={(e) => updateItem(index, { numeroDocumento: e.target.value })}
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs uppercase text-slate-500">Monto total</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    value={row.montoTotal}
                    onChange={(e) => updateItem(index, { montoTotal: e.target.value })}
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                  />
                </div>
                {items.length > 1 && (
                  <div className="flex items-end justify-end">
                    <button
                      type="button"
                      onClick={() => removeItem(index)}
                      className="text-sm text-red-600 hover:text-red-800"
                    >
                      Eliminar
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* Vista escritorio/tablet: tabla */}
        <div className="mt-4 hidden overflow-x-auto sm:block">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="text-xs uppercase text-slate-500">
              <tr>
                <th className="py-2 pr-2">Glosa</th>
                <th className="py-2 pr-2">Proveedor</th>
                <th className="py-2 pr-2">Tipo documento</th>
                <th className="py-2 pr-2">N° documento</th>
                <th className="py-2 pr-2">Monto total</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {items.map((row, index) => (
                <tr key={index} className="border-t border-slate-100">
                  <td className="py-2 pr-2">
                    <input
                      value={row.glosa}
                      onChange={(e) => updateItem(index, { glosa: e.target.value })}
                      placeholder="Ej: materiales"
                      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="py-2 pr-2">
                    <input
                      value={row.proveedor}
                      onChange={(e) => updateItem(index, { proveedor: e.target.value })}
                      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="py-2 pr-2">
                    <select
                      value={row.tipoDocumento}
                      onChange={(e) => updateItem(index, { tipoDocumento: e.target.value as ItemRow["tipoDocumento"] })}
                      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    >
                      {Object.entries(documentTypeLabels).map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="py-2 pr-2">
                    <input
                      value={row.numeroDocumento}
                      onChange={(e) => updateItem(index, { numeroDocumento: e.target.value })}
                      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="py-2 pr-2">
                    <input
                      type="number"
                      min="0"
                      step="1"
                      value={row.montoTotal}
                      onChange={(e) => updateItem(index, { montoTotal: e.target.value })}
                      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                    />
                  </td>
                  <td className="py-2 text-right">
                    {items.length > 1 && (
                      <button
                        type="button"
                        onClick={() => removeItem(index)}
                        className="text-sm text-red-600 hover:text-red-800"
                      >
                        Eliminar
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <button
          type="button"
          onClick={addItem}
          className="mt-3 rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          + Agregar línea
        </button>

        <div className="mt-5 grid grid-cols-2 gap-4 border-t border-slate-100 pt-4">
          <div>
            <p className="text-xs uppercase text-slate-500">Total rendido</p>
            <p className="mt-1 text-lg font-semibold text-slate-900">{formatCurrency(totals.totalRendido)}</p>
          </div>
          <div>
            <p className="text-xs uppercase text-slate-500">Reembolso correspondiente</p>
            <p className="mt-1 text-lg font-semibold text-[#004b93]">{formatCurrency(totals.montoReembolso)}</p>
          </div>
        </div>
      </section>

      <section ref={adjuntosRef} className="scroll-mt-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Fotos y documentos</h2>
        <p className="mt-1 text-sm text-slate-500">
          Toma fotos de tus boletas/facturas, súbelas desde tu galería, o adjunta un documento digital.
        </p>
        <div className="mt-4">
          <AttachmentUploader reportId={reportId} initialAttachments={initialAttachments} />
        </div>
      </section>

      <section ref={firmaRef} className="scroll-mt-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Firma</h2>
        <div className="mt-4 max-w-xl">
          <SignaturePad onChange={setSignatureData} />
        </div>
        <div className="mt-4 max-w-xs">
          <label className="block text-sm font-medium text-slate-700">RUT</label>
          <input
            value={rut}
            onChange={(e) => setRut(formatRut(e.target.value))}
            inputMode="numeric"
            placeholder="12.345.678-9"
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none"
          />
        </div>
      </section>

      <button
        type="button"
        onClick={handleSubmit}
        disabled={isPending}
        className="w-full rounded-md bg-slate-900 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-slate-700 disabled:opacity-60 sm:w-auto"
      >
        {isPending ? "Enviando…" : "Enviar rendición"}
      </button>
    </div>
  );
}
