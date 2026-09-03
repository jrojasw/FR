import { notFound } from "next/navigation";
import Link from "next/link";
import { prisma } from "@/lib/prisma";
import { PinLoginForm } from "@/components/PinLoginForm";

export default async function PinLoginPage({
  searchParams,
}: {
  searchParams: Promise<{ userId?: string }>;
}) {
  const { userId } = await searchParams;
  if (!userId) notFound();

  const user = await prisma.user.findFirst({
    where: { id: userId, role: "SOLICITANTE", active: true },
    select: { name: true },
  });
  if (!user) notFound();

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-center text-lg font-semibold text-slate-900">Hola, {user.name}</h1>
        <p className="mt-1 text-center text-sm text-slate-500">Ingresa tu PIN para continuar.</p>
        <div className="mt-6">
          <PinLoginForm userId={userId} />
        </div>
        <div className="mt-6 text-center">
          <Link href="/login" className="text-sm text-slate-500 hover:text-slate-700">
            ← No soy yo
          </Link>
        </div>
      </div>
    </div>
  );
}
