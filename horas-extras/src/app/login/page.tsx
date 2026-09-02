import { RequestOtpForm } from "@/components/RequestOtpForm";

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string }>;
}) {
  const { next } = await searchParams;

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-center text-lg font-semibold text-slate-900">Horas Extra</h1>
        <p className="mt-1 text-center text-sm text-slate-500">
          Ingresa tu correo y te enviaremos un código de acceso de 4 dígitos.
        </p>
        <div className="mt-6">
          <RequestOtpForm next={next} />
        </div>
      </div>
    </div>
  );
}
