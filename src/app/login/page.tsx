import { Chewy } from "next/font/google";
import { RequestOtpForm } from "@/components/RequestOtpForm";

const chewy = Chewy({ weight: "400", subsets: ["latin"] });

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string }>;
}) {
  const { next } = await searchParams;

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <div className="flex items-baseline gap-2">
          <span className={`${chewy.className} text-4xl text-slate-900`}>Elon</span>
          <span className="text-xs text-slate-500">
            By <span className="font-bold" style={{ color: "#2CA8DE" }}>CPY</span>{" "}
            <span className="font-bold" style={{ color: "#E87033" }}>NOS</span>
          </span>
        </div>
        <h1 className="mt-3 text-lg font-semibold text-slate-900">Sistema de rendición de fondos</h1>
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
