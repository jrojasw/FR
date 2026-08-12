import "server-only";
import { Resend } from "resend";

type SendEmailInput = {
  to: string | string[];
  subject: string;
  html: string;
  attachments?: { filename: string; content: Buffer }[];
};

const FROM_ADDRESS = process.env.EMAIL_FROM || "Fondos a Rendir <onboarding@resend.dev>";

let client: Resend | null = null;
function getClient(): Resend | null {
  const apiKey = process.env.RESEND_API_KEY;
  if (!apiKey) return null;
  if (!client) client = new Resend(apiKey);
  return client;
}

/**
 * Envía un correo con Resend cuando RESEND_API_KEY está configurado.
 * Si no hay proveedor configurado, deja el contenido en el log del servidor
 * para poder probar el flujo completo sin credenciales reales.
 */
export async function sendEmail(input: SendEmailInput): Promise<{ delivered: boolean }> {
  const resend = getClient();

  const to = Array.isArray(input.to) ? input.to.join(", ") : input.to;

  if (!resend) {
    console.log(
      `[email:no-configurado] Para: ${to} | Asunto: ${input.subject}\n${input.html.replace(/<[^>]+>/g, " ")}`
    );
    return { delivered: false };
  }

  await resend.emails.send({
    from: FROM_ADDRESS,
    to: input.to,
    subject: input.subject,
    html: input.html,
    attachments: input.attachments?.map((a) => ({
      filename: a.filename,
      content: a.content,
    })),
  });

  return { delivered: true };
}

export function isEmailConfigured(): boolean {
  return Boolean(process.env.RESEND_API_KEY);
}
