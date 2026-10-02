"use client";

import { Suspense, useEffect, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

// Aviso que se muestra justo después de enviar una rendición. El envío
// redirige a la misma ruta (mismo reportId), así que Next.js no vuelve a
// montar este componente: hay que reaccionar al cambio de searchParams en
// vez de leerlo solo al montar, o el aviso nunca aparece.
function SubmittedNoticeInner() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (searchParams.get("enviado") === "1") {
      setOpen(true);
      router.replace(pathname, { scroll: false });
    }
  }, [searchParams, pathname, router]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4"
      onClick={() => setOpen(false)}
    >
      <div
        className="w-full max-w-sm rounded-lg bg-white p-6 text-center"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-2xl">
          ✅
        </div>
        <h3 className="mt-4 text-base font-semibold text-slate-900">Su rendición ha sido enviada</h3>
        <p className="mt-1 text-sm text-slate-500">
          Quedó registrada y en revisión. Te avisaremos por correo cuando haya una novedad.
        </p>
        <button
          type="button"
          onClick={() => setOpen(false)}
          className="mt-4 rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700"
        >
          Listo
        </button>
      </div>
    </div>
  );
}

export function SubmittedNotice() {
  return (
    <Suspense fallback={null}>
      <SubmittedNoticeInner />
    </Suspense>
  );
}
