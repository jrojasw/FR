import Link from "next/link";
import { AdminLoginForm } from "@/components/AdminLoginForm";

export default function AdminLoginPage() {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="text-center text-lg font-semibold text-slate-900">Administradora</h1>
        <p className="mt-1 text-center text-sm text-slate-500">Ingresa con tu correo y clave.</p>
        <div className="mt-6">
          <AdminLoginForm />
        </div>
        <div className="mt-6 text-center">
          <Link href="/login" className="text-sm text-slate-500 hover:text-slate-700">
            ← Volver a selección de colaboradora
          </Link>
        </div>
      </div>
    </div>
  );
}
