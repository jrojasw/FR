import { redirect } from "next/navigation";
import { VerifyOtpForm } from "@/components/VerifyOtpForm";
import { BrandHeader } from "@/components/BrandHeader";

export default async function VerifyOtpPage({
  searchParams,
}: {
  searchParams: Promise<{ email?: string; next?: string; dev?: string }>;
}) {
  const { email, next, dev } = await searchParams;

  if (!email) {
    redirect("/login");
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <BrandHeader />
        <h1 className="mt-3 text-xl font-semibold text-slate-900">Ingresa tu código</h1>
        <p className="mt-1 text-sm text-slate-500">
          Enviamos un código de 4 dígitos a <span className="font-medium">{email}</span>.
        </p>
        {dev ? (
          <p className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
            Correo no configurado todavía (modo desarrollo). Tu código es{" "}
            <span className="font-semibold">{dev}</span>.
          </p>
        ) : null}
        <div className="mt-6">
          <VerifyOtpForm email={email} next={next} />
        </div>
      </div>
    </div>
  );
}
