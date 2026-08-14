import { RequestOtpForm } from "@/components/RequestOtpForm";

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string }>;
}) {
  const { next } = await searchParams;

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mx-auto flex w-fit flex-col items-end">
          <span
            className="font-normal"
            style={{ fontSize: "7rem", lineHeight: 1, color: "#555555", fontFamily: "'Times New Roman', Times, serif" }}
          >
            Elon
          </span>
          <span className="-mt-1 text-xs text-slate-500">
            By <span style={{ color: "#2CA8DE" }}>CPY</span>
            <span style={{ color: "#E87033" }}>NOS</span>
          </span>
        </div>
        <h1 className="mt-3 text-center text-lg font-semibold text-slate-900">Sistema de rendición de fondos</h1>
        <p className="mt-1 text-sm text-slate-500">
          Ingresa tu correo y te enviaremos un código de acceso de 4 dígitos.
        </p>
        <div className="mt-6">
          <RequestOtpForm next={next} />
        </div>
      </div>
    </div>
  );
}
