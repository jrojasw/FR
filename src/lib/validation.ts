import { z } from "zod";

export const requestOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
});

export const verifyOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
  code: z.string().trim().regex(/^\d{4}$/, "El código debe tener 4 dígitos"),
});

export const createReportSchema = z.object({
  nombre: z.string().trim().min(2, "Nombre muy corto"),
  cargo: z.string().trim().min(2, "Cargo muy corto"),
  fecha: z.string().min(1, "Fecha requerida"),
  fondoPorRendir: z.coerce.number().positive("Debe ser mayor a 0"),
  glosa: z.string().trim().min(2, "Ingresa una glosa (ej: materiales, alimentación)"),
});

export const itemRowSchema = z.object({
  proveedor: z.string().trim().min(2, "Proveedor muy corto"),
  tipoDocumento: z.enum(["BOLETA", "FACTURA", "RECIBO"]),
  numeroDocumento: z.string().trim().min(1, "N° documento requerido"),
  montoTotal: z.coerce.number().positive("Debe ser mayor a 0"),
});

export const saveItemsSchema = z.object({
  items: z.array(itemRowSchema).min(1, "Agrega al menos un documento"),
});

export const finalizeReportSchema = z.object({
  rut: z.string().trim().min(3, "RUT requerido"),
  signatureData: z.string().min(1, "Falta la firma"),
});

export const reviewReportSchema = z.object({
  decision: z.enum(["APPROVED", "REJECTED"]),
  reviewComment: z.string().trim().max(2000).optional(),
});
