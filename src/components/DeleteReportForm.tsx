"use client";

export function DeleteReportForm({
  reportId,
  correlativo,
  action,
}: {
  reportId: string;
  correlativo: number;
  action: (formData: FormData) => Promise<void>;
}) {
  return (
    <form
      action={action}
      onSubmit={(e) => {
        if (
          !confirm(
            `¿Eliminar la rendición N° ${correlativo} de forma permanente? Esta acción no se puede deshacer.`
          )
        ) {
          e.preventDefault();
        }
      }}
    >
      <input type="hidden" name="reportId" value={reportId} />
      <button type="submit" className="text-sm text-red-600 hover:text-red-800">
        Eliminar
      </button>
    </form>
  );
}
