import { z } from "zod";

export const requestOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
});

export const verifyOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
  code: z.string().trim().regex(/^\d{4}$/, "El código debe tener 4 dígitos"),
});

const TIME_REGEX = /^([01]\d|2[0-3]):([0-5]\d)$/;

export const overtimeEntrySchema = z.object({
  fecha: z.string().min(1, "Fecha requerida"),
  horaInicio: z.string().regex(TIME_REGEX, "Hora de inicio inválida"),
  horaFin: z.string().regex(TIME_REGEX, "Hora de término inválida"),
  motivo: z.string().trim().min(5, "Describe brevemente el motivo o la tarea realizada"),
});

export const reviewEntrySchema = z.object({
  decision: z.enum(["APROBADA", "RECHAZADA"]),
  reviewComment: z.string().trim().max(2000).optional(),
});
