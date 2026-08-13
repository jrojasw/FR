import "server-only";
import { mkdir, writeFile, unlink, readFile } from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { S3Client, PutObjectCommand, GetObjectCommand, DeleteObjectCommand } from "@aws-sdk/client-s3";

const STORAGE_ROOT = process.env.ATTACHMENTS_DIR || path.join(process.cwd(), "storage", "attachments");

function sanitizeFileName(name: string): string {
  return name.replace(/[^a-zA-Z0-9._-]/g, "_").slice(-100);
}

function buildKey(reportId: string, fileName: string): string {
  return `${reportId}/${randomUUID()}-${sanitizeFileName(fileName)}`;
}

let r2Client: S3Client | null = null;

/**
 * Cuando las variables R2_* están configuradas, los adjuntos se guardan en
 * Cloudflare R2 (compatible con S3). Si no, caen a disco local — útil para
 * desarrollo sin credenciales.
 */
function getR2Client(): { client: S3Client; bucket: string } | null {
  const accountId = process.env.R2_ACCOUNT_ID;
  const accessKeyId = process.env.R2_ACCESS_KEY_ID;
  const secretAccessKey = process.env.R2_SECRET_ACCESS_KEY;
  const bucket = process.env.R2_BUCKET_NAME;
  if (!accountId || !accessKeyId || !secretAccessKey || !bucket) return null;

  if (!r2Client) {
    r2Client = new S3Client({
      region: "auto",
      endpoint: `https://${accountId}.r2.cloudflarestorage.com`,
      credentials: { accessKeyId, secretAccessKey },
    });
  }
  return { client: r2Client, bucket };
}

export async function saveAttachmentFile(
  reportId: string,
  fileName: string,
  buffer: Buffer
): Promise<string> {
  const key = buildKey(reportId, fileName);
  const r2 = getR2Client();

  if (r2) {
    await r2.client.send(new PutObjectCommand({ Bucket: r2.bucket, Key: key, Body: buffer }));
    return key;
  }

  const fullPath = path.join(/* turbopackIgnore: true */ STORAGE_ROOT, key);
  await mkdir(path.dirname(fullPath), { recursive: true });
  await writeFile(fullPath, buffer);
  return key;
}

export async function readAttachmentFile(relativePath: string): Promise<Buffer> {
  const r2 = getR2Client();
  if (r2) {
    const result = await r2.client.send(new GetObjectCommand({ Bucket: r2.bucket, Key: relativePath }));
    const bytes = await result.Body!.transformToByteArray();
    return Buffer.from(bytes);
  }

  return readFile(path.join(/* turbopackIgnore: true */ STORAGE_ROOT, relativePath));
}

export async function deleteAttachmentFile(relativePath: string): Promise<void> {
  const r2 = getR2Client();
  if (r2) {
    await r2.client.send(new DeleteObjectCommand({ Bucket: r2.bucket, Key: relativePath })).catch(() => {});
    return;
  }

  await unlink(path.join(/* turbopackIgnore: true */ STORAGE_ROOT, relativePath)).catch(() => {});
}
