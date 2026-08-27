import { RequestOtpForm } from "@/components/RequestOtpForm";
import { BrandHeader } from "@/components/BrandHeader";

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string; error?: string }>;
}) {
  const { next, error } = await searchParams;

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <BrandHeader />
        <h1 className="mt-3 text-center text-lg font-semibold text-slate-900">Sistema de rendición de fondos</h1>
        <p className="mt-1 text-sm text-slate-500">
          Ingresa tu correo y te enviaremos un código de acceso de 4 dígitos.
        </p>
        {error === "enlace-vencido" && (
          <p className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            Ese enlace para usar el celular ya venció. Genera uno nuevo desde el computador e ingresa aquí con tu correo.
          </p>
        )}
        <div className="mt-6">
          <RequestOtpForm next={next} />
        </div>
      </div>
    </div>
  );
}
