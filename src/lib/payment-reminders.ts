import "server-only";
import { prisma } from "@/lib/prisma";
import { sendEmail } from "@/lib/email";
import { getAdminEmail } from "@/lib/roles";
import { formatCurrency } from "@/lib/format";

// Cada cuánto se reenvía el aviso de "falta subir comprobante de pago"
// mientras una rendición sigue Aprobada sin certificado.
const REMINDER_INTERVAL_MS = 4 * 60 * 60 * 1000;
// Cada cuánto se revisa si hay recordatorios pendientes por enviar.
const CHECK_INTERVAL_MS = 15 * 60 * 1000;

export async function sendPendingPaymentReminders() {
  const cutoff = new Date(Date.now() - REMINDER_INTERVAL_MS);
  const baseUrl = process.env.APP_URL || "";

  const pending = await prisma.expenseReport.findMany({
    where: {
      status: "APPROVED",
      paymentCertificatePath: null,
      reviewedAt: { lte: cutoff },
      OR: [{ lastPaymentReminderAt: null }, { lastPaymentReminderAt: { lte: cutoff } }],
    },
  });

  for (const report of pending) {
    try {
      const porTercero = report.esParaOtraPersona
        ? ` a nombre de ${report.beneficiarioNombre} ${report.beneficiarioApellido} ${report.beneficiarioSegundoApellido}`
        : "";
      await sendEmail({
        to: getAdminEmail(),
        subject: `Recordatorio: falta subir comprobante de pago - Rendición N° ${report.correlativo}`,
        html: `
          <p>Sigue pendiente subir el comprobante de la transferencia del banco para la rendición N° ${report.correlativo} de ${report.nombre} ${report.apellido} ${report.segundoApellido} (${formatCurrency(report.totalRendido.toString())})${porTercero}.</p>
          <p>Este recordatorio se repetirá cada 4 horas hasta que se suba el comprobante.</p>
          <p><a href="${baseUrl}/aprobaciones/${report.id}">Subir comprobante de pago</a></p>
        `,
      });
      await prisma.expenseReport.update({
        where: { id: report.id },
        data: { lastPaymentReminderAt: new Date() },
      });
    } catch (error) {
      console.error(`Error enviando recordatorio de pago para la rendición ${report.id}:`, error);
    }
  }
}

// Se llama una vez al iniciar el servidor (ver instrumentation.ts). El
// proceso de Next queda corriendo de forma continua en Railway, así que un
// setInterval en memoria alcanza sin depender de un cron externo.
export function startPaymentReminderScheduler() {
  const run = () => {
    sendPendingPaymentReminders().catch((error) => {
      console.error("Error revisando recordatorios de pago pendientes:", error);
    });
  };
  run();
  setInterval(run, CHECK_INTERVAL_MS);
}
