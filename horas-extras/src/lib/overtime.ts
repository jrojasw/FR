/**
 * Combina la fecha (YYYY-MM-DD) con las horas de inicio/término (HH:mm) en
 * timestamps UTC (la hora se trata como hora de muro, sin zona horaria) y
 * calcula las horas trabajadas. Si la hora de término es igual o anterior a
 * la de inicio, se asume que el turno cruzó la medianoche.
 */
export function buildOvertimeRange(fecha: string, horaInicio: string, horaFin: string) {
  const inicio = new Date(`${fecha}T${horaInicio}:00.000Z`);
  let fin = new Date(`${fecha}T${horaFin}:00.000Z`);

  if (fin.getTime() <= inicio.getTime()) {
    fin = new Date(fin.getTime() + 24 * 60 * 60 * 1000);
  }

  const horas = Math.round(((fin.getTime() - inicio.getTime()) / (60 * 60 * 1000)) * 100) / 100;

  return { fechaInicio: inicio, fechaFin: fin, horas };
}

/** true si la fecha (YYYY-MM-DD) cae en domingo. */
export function isSunday(fecha: string): boolean {
  return new Date(`${fecha}T00:00:00.000Z`).getUTCDay() === 0;
}

export function toDateOnly(fecha: string): Date {
  return new Date(`${fecha}T00:00:00.000Z`);
}
