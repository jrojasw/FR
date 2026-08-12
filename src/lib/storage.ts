import "server-only";
import { mkdir, writeFile, unlink, readFile } from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";

const STORAGE_ROOT = process.env.ATTACHMENTS_DIR || path.join(process.cwd(), "storage", "attachments");

function sanitizeFileName(name: string): string {
  return name.replace(/[^a-zA-Z0-9._-]/g, "_").slice(-100);
}

function attachmentDir(reportId: string): string {
  return path.join(/* turbopackIgnore: true */ STORAGE_ROOT, reportId);
}

export async function saveAttachmentFile(
  reportId: string,
  fileName: string,
  buffer: Buffer
): Promise<string> {
  const dir = attachmentDir(reportId);
  await mkdir(dir, { recursive: true });
  const storedName = `${randomUUID()}-${sanitizeFileName(fileName)}`;
  const fullPath = path.join(dir, storedName);
  await writeFile(fullPath, buffer);
  return path.relative(STORAGE_ROOT, fullPath);
}

export async function readAttachmentFile(relativePath: string): Promise<Buffer> {
  return readFile(path.join(/* turbopackIgnore: true */ STORAGE_ROOT, relativePath));
}

export async function deleteAttachmentFile(relativePath: string): Promise<void> {
  await unlink(path.join(/* turbopackIgnore: true */ STORAGE_ROOT, relativePath)).catch(() => {});
}
