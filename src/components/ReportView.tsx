import type { ReactNode } from "react";
import {
  formatCurrency,
  formatDate,
  documentTypeLabels,
  reportStatusLabels,
  reportStatusStyles,
} from "@/lib/format";

type Decimalish = { toString(): string };

export type ReportViewData = {
  id: string;
  correlativo: number;
  nombre: string;
  apellido: string;
  cargo: string;
  fecha: Date;
  totalRendido: Decimalish;
  montoReembolso: Decimalish;
  rut: string | null;
  esParaOtraPersona: boolean;
  beneficiarioNombre: string | null;
  beneficiarioApellido: string | null;
  beneficiarioRut: string | null;
  signatureData: string | null;
  status: string;
  submittedAt: Date | null;
  reviewedAt: Date | null;
  reviewComment: string | null;
  reviewer?: { name: string | null; email: string } | null;
  paidAt: Date | null;
  paymentCertificateName: string | null;
  paidBy?: { name: string | null; email: string } | null;
  items: {
    id: string;
    glosa: string;
    proveedor: string;
    tipoDocumento: string;
    numeroDocumento: string;
    montoTotal: Decimalish;
  }[];
  attachments: { id: string; fileName: string; mimeType: string; kind: string }[];
};

export function ReportView({ report, actions }: { report: ReportViewData; actions?: ReactNode }) {
  return (
    <div className="space-y-8">
      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-semibold text-slate-900">Encabezado</h2>
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-slate-100 px-3 py-1 text-sm font-medium text-slate-700">
              N° {report.correlativo}
            </span>
            <span className={`rounded-full px-3 py-1 text-sm font-medium ${reportStatusStyles[report.status]}`}>
              {reportStatusLabels[report.status]}
            </span>
          </div>
        </div>
        <dl className="mt-4 grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-slate-500">Nombre</dt>
            <dd className="font-medium text-slate-900">
              {report.nombre} {report.apellido}
            </dd>
          </div>
          <div>
            <dt className="text-slate-500">Cargo</dt>
            <dd className="font-medium text-slate-900">{report.cargo}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Fecha</dt>
            <dd className="font-medium text-slate-900">{formatDate(report.fecha)}</dd>
          </div>
        </dl>

        {report.esParaOtraPersona && (
          <div className="mt-4 rounded-md border border-slate-200 bg-slate-50 p-3">
            <p className="text-xs uppercase text-slate-500">A nombre de</p>
            <p className="mt-1 text-sm font-medium text-slate-900">
              {report.beneficiarioNombre} {report.beneficiarioApellido}
            </p>
          </div>
        )}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Detalle de documentos</h2>
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead className="text-xs uppercase text-slate-500">
              <tr>
                <th className="py-2 pr-2">Glosa</th>
                <th className="py-2 pr-2">Proveedor</th>
                <th className="py-2 pr-2">Tipo documento</th>
                <th className="py-2 pr-2">N° documento</th>
                <th className="py-2 pr-2">Monto total</th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((item) => (
                <tr key={item.id} className="border-t border-slate-100">
                  <td className="py-2 pr-2">{item.glosa}</td>
                  <td className="py-2 pr-2">{item.proveedor}</td>
                  <td className="py-2 pr-2">{documentTypeLabels[item.tipoDocumento] ?? item.tipoDocumento}</td>
                  <td className="py-2 pr-2">{item.numeroDocumento}</td>
                  <td className="py-2 pr-2">{formatCurrency(item.montoTotal.toString())}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="mt-5 grid grid-cols-2 gap-4 border-t border-slate-100 pt-4">
          <div>
            <p className="text-xs uppercase text-slate-500">Total rendido</p>
            <p className="mt-1 text-lg font-semibold text-slate-900">
              {formatCurrency(report.totalRendido.toString())}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase text-slate-500">Reembolso correspondiente</p>
            <p className="mt-1 text-lg font-semibold text-[#004b93]">
              {formatCurrency(report.montoReembolso.toString())}
            </p>
          </div>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Fotos y documentos</h2>
        {report.attachments.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">Sin adjuntos.</p>
        ) : (
          <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {report.attachments.map((a) => (
              <li key={a.id} className="overflow-hidden rounded-md border border-slate-200 bg-white">
                <a href={`/api/adjuntos/${a.id}`} target="_blank" rel="noreferrer">
                  {a.kind === "PHOTO" ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={`/api/adjuntos/${a.id}`} alt={a.fileName} className="h-24 w-full object-cover" />
                  ) : (
                    <div className="flex h-24 w-full flex-col items-center justify-center gap-1 bg-slate-50 px-2 text-center">
                      <span className="text-2xl">📄</span>
                      <span className="line-clamp-1 text-xs text-slate-600">{a.fileName}</span>
                    </div>
                  )}
                </a>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Firma</h2>
        <div className="mt-4 flex flex-wrap items-end gap-6">
          {report.signatureData ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={report.signatureData}
              alt="Firma"
              className="h-32 w-64 rounded-md border border-slate-200 bg-white object-contain"
            />
          ) : (
            <p className="text-sm text-slate-500">Sin firma.</p>
          )}
          <div className="text-sm">
            <p className="text-slate-500">{report.esParaOtraPersona ? "RUT (quien rinde)" : "RUT"}</p>
            <p className="font-medium text-slate-900">{report.rut ?? "—"}</p>
          </div>
          {report.esParaOtraPersona && (
            <div className="text-sm">
              <p className="text-slate-500">RUT de {report.beneficiarioNombre}</p>
              <p className="font-medium text-slate-900">{report.beneficiarioRut ?? "—"}</p>
            </div>
          )}
        </div>
      </section>

      {(report.status === "APPROVED" || report.status === "REJECTED" || report.status === "PAID") && (
        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">Revisión</h2>
          <dl className="mt-3 space-y-2 text-sm">
            <div>
              <dt className="text-slate-500">Revisado por</dt>
              <dd className="font-medium text-slate-900">
                {report.reviewer?.name ?? report.reviewer?.email ?? "—"}
              </dd>
            </div>
            <div>
              <dt className="text-slate-500">Fecha</dt>
              <dd className="font-medium text-slate-900">
                {report.reviewedAt ? formatDate(report.reviewedAt) : "—"}
              </dd>
            </div>
            {report.reviewComment ? (
              <div>
                <dt className="text-slate-500">Comentario</dt>
                <dd className="font-medium text-slate-900">{report.reviewComment}</dd>
              </div>
            ) : null}
          </dl>
        </section>
      )}

      {report.status === "PAID" && (
        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">Pago</h2>
          <dl className="mt-3 space-y-2 text-sm">
            <div>
              <dt className="text-slate-500">Pagado por</dt>
              <dd className="font-medium text-slate-900">
                {report.paidBy?.name ?? report.paidBy?.email ?? "—"}
              </dd>
            </div>
            <div>
              <dt className="text-slate-500">Fecha</dt>
              <dd className="font-medium text-slate-900">
                {report.paidAt ? formatDate(report.paidAt) : "—"}
              </dd>
            </div>
            {report.paymentCertificateName ? (
              <div>
                <dt className="text-slate-500">Certificado de pago</dt>
                <dd className="font-medium text-slate-900">
                  <a
                    href={`/api/rendiciones/${report.id}/certificado-pago`}
                    target="_blank"
                    rel="noreferrer"
                    className="text-slate-900 underline"
                  >
                    {report.paymentCertificateName}
                  </a>
                </dd>
              </div>
            ) : null}
          </dl>
        </section>
      )}

      {actions}
    </div>
  );
}
