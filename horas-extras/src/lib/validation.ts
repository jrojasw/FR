import { z } from "zod";

export const adminLoginSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
  password: z.string().min(1, "Ingresa tu clave"),
});

export const pinLoginSchema = z.object({
  userId: z.string().min(1),
  pin: z.string().trim().regex(/^\d{4,6}$/, "El PIN debe tener entre 4 y 6 dígitos"),
});

const TIME_REGEX = /^([01]\d|2[0-3]):([0-5]\d)$/;

export const turnoDomingoSchema = z.object({
  tipo: z.literal("TURNO_DOMINGO"),
  fecha: z.string().min(1, "Fecha requerida"),
  motivo: z.string().trim().max(500).optional().default(""),
});

export const horasExtraSchema = z.object({
  tipo: z.literal("HORAS_EXTRA"),
  fecha: z.string().min(1, "Fecha requerida"),
  horaInicio: z.string().regex(TIME_REGEX, "Hora de inicio inválida"),
  horaFin: z.string().regex(TIME_REGEX, "Hora de término inválida"),
  motivo: z.string().trim().min(5, "Describe brevemente el motivo o la tarea realizada"),
});

export const overtimeEntrySchema = z.discriminatedUnion("tipo", [turnoDomingoSchema, horasExtraSchema]);
export type OvertimeEntryInput = z.infer<typeof overtimeEntrySchema>;

export const reviewEntrySchema = z.object({
  decision: z.enum(["APROBADA", "RECHAZADA"]),
  reviewComment: z.string().trim().max(2000).optional(),
  validadoReloj: z
    .enum(["true", "false"])
    .optional()
    .transform((v) => v === "true"),
});

export const createColaboradoraSchema = z.object({
  name: z.string().trim().min(2, "Nombre muy corto"),
  pin: z.string().trim().regex(/^\d{4,6}$/, "El PIN debe tener entre 4 y 6 dígitos"),
});

export const resetPinSchema = z.object({
  userId: z.string().min(1),
  pin: z.string().trim().regex(/^\d{4,6}$/, "El PIN debe tener entre 4 y 6 dígitos"),
});
