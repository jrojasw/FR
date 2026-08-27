import { z } from "zod";

export const requestOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
});

export const verifyOtpSchema = z.object({
  email: z.string().trim().toLowerCase().email("Correo inválido"),
  code: z.string().trim().regex(/^\d{4}$/, "El código debe tener 4 dígitos"),
});

const NO_SPACES_MESSAGE = "No puede tener espacios: usa el campo Apellido para el resto del nombre";

export const createReportSchema = z
  .object({
    nombre: z.string().trim().min(2, "Nombre muy corto").regex(/^\S+$/, NO_SPACES_MESSAGE),
    apellido: z.string().trim().min(2, "Apellido muy corto").regex(/^\S+$/, NO_SPACES_MESSAGE),
    segundoApellido: z.string().trim().min(2, "Segundo apellido muy corto").regex(/^\S+$/, NO_SPACES_MESSAGE),
    cargo: z.string().trim().min(2, "Cargo muy corto"),
    fecha: z.string().min(1, "Fecha requerida"),
    esParaOtraPersona: z
      .enum(["true", "false"])
      .transform((v) => v === "true"),
    beneficiarioNombre: z.string().trim().optional().default(""),
    beneficiarioApellido: z.string().trim().optional().default(""),
    beneficiarioSegundoApellido: z.string().trim().optional().default(""),
    beneficiarioEmail: z.string().trim().optional().default(""),
  })
  .superRefine((data, ctx) => {
    if (!data.esParaOtraPersona) return;
    if (data.beneficiarioNombre.length < 2) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioNombre"], message: "Nombre de la persona muy corto" });
    } else if (!/^\S+$/.test(data.beneficiarioNombre)) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioNombre"], message: NO_SPACES_MESSAGE });
    }
    if (data.beneficiarioApellido.length < 2) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioApellido"], message: "Apellido de la persona muy corto" });
    } else if (!/^\S+$/.test(data.beneficiarioApellido)) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioApellido"], message: NO_SPACES_MESSAGE });
    }
    if (data.beneficiarioSegundoApellido.length < 2) {
      ctx.addIssue({
        code: "custom",
        path: ["beneficiarioSegundoApellido"],
        message: "Segundo apellido de la persona muy corto",
      });
    } else if (!/^\S+$/.test(data.beneficiarioSegundoApellido)) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioSegundoApellido"], message: NO_SPACES_MESSAGE });
    }
    if (data.beneficiarioEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(data.beneficiarioEmail)) {
      ctx.addIssue({ code: "custom", path: ["beneficiarioEmail"], message: "El correo de la persona no es válido" });
    }
  });

export const itemRowSchema = z.object({
  glosa: z.string().trim().min(2, "Ingresa una glosa (ej: materiales, alimentación)"),
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
  beneficiarioRut: z.string().trim().optional().default(""),
});

export const reviewReportSchema = z.object({
  decision: z.enum(["APPROVED", "REJECTED"]),
  reviewComment: z.string().trim().max(2000).optional(),
});
