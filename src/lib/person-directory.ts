import "server-only";
import { prisma } from "@/lib/prisma";
import { cleanRut } from "@/lib/rut";

// Base de personas por RUT, armada sola con cada rendición "a nombre de
// otra persona" que se envía: la próxima vez que cualquier trabajador
// escriba ese mismo RUT, se le sugiere el nombre y el correo ya conocidos.
export async function upsertPersonDirectory(params: {
  rut: string;
  nombre?: string | null;
  apellido?: string | null;
  segundoApellido?: string | null;
  email?: string | null;
}): Promise<void> {
  const rut = cleanRut(params.rut);
  if (!rut) return;

  await prisma.personDirectory.upsert({
    where: { rut },
    create: {
      rut,
      nombre: params.nombre || null,
      apellido: params.apellido || null,
      segundoApellido: params.segundoApellido || null,
      email: params.email || null,
    },
    update: {
      // Solo pisa un dato si esta vez vino uno nuevo no vacío, para no
      // borrar lo que ya había si se dejó en blanco.
      ...(params.nombre ? { nombre: params.nombre } : {}),
      ...(params.apellido ? { apellido: params.apellido } : {}),
      ...(params.segundoApellido ? { segundoApellido: params.segundoApellido } : {}),
      ...(params.email ? { email: params.email } : {}),
    },
  });
}

export async function lookupPersonDirectory(rut: string) {
  const clean = cleanRut(rut);
  if (!clean) return null;
  return prisma.personDirectory.findUnique({ where: { rut: clean } });
}
