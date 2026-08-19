// Detección de bordes de documento y corrección de perspectiva en el navegador,
// usando OpenCV.js (cargado en tiempo de ejecución desde /opencv/opencv.js).
// La lógica de detección está adaptada de jscanify (MIT License,
// https://github.com/puffinsoft/jscanify) para no depender del paquete npm
// completo (que arrastra "canvas"/"jsdom" nativos que no necesitamos en el navegador).
//
// OpenCV.js no distribuye tipos oficiales y se carga dinámicamente como
// global en tiempo de ejecución, así que su API se trata como `any` aquí.
/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

export type Point = { x: number; y: number };

export type CornerPoints = {
  topLeftCorner: Point;
  topRightCorner: Point;
  bottomLeftCorner: Point;
  bottomRightCorner: Point;
};

let loadPromise: Promise<void> | null = null;

export function loadOpenCv(): Promise<void> {
  if (typeof window === "undefined") return Promise.reject(new Error("client only"));
  const w = window as unknown as { cv?: { onRuntimeInitialized?: () => void } };
  if (w.cv && (w as unknown as { cv: { Mat?: unknown } }).cv.Mat) return Promise.resolve();

  if (!loadPromise) {
    loadPromise = new Promise((resolve, reject) => {
      const existing = document.querySelector<HTMLScriptElement>('script[data-opencv="true"]');
      if (existing) {
        existing.addEventListener("load", () => waitForRuntime(resolve));
        existing.addEventListener("error", () => reject(new Error("No se pudo cargar OpenCV")));
        return;
      }
      const script = document.createElement("script");
      script.src = "/opencv/opencv.js";
      script.async = true;
      script.dataset.opencv = "true";
      script.onload = () => waitForRuntime(resolve);
      script.onerror = () => reject(new Error("No se pudo cargar OpenCV"));
      document.head.appendChild(script);
    });
  }
  return loadPromise;
}

function waitForRuntime(resolve: () => void) {
  const w = window as unknown as { cv: { onRuntimeInitialized?: () => void; Mat?: unknown } };
  if (w.cv.Mat) {
    resolve();
    return;
  }
  w.cv.onRuntimeInitialized = () => resolve();
}

function getCv(): any {
  return (window as unknown as { cv: any }).cv;
}

function distance(p1: Point, p2: Point): number {
  return Math.hypot(p1.x - p2.x, p1.y - p2.y);
}

// cv.imread(imgElement) sizes its internal Mat from the element's *rendered*
// CSS box (image.width/height), not its natural pixel dimensions — since the
// preview <img> is scaled down to fit the screen, that mismatches every
// natural-pixel coordinate used elsewhere (the SVG overlay, the corner drag
// handles, the final crop). Drawing to an offscreen canvas at
// naturalWidth/naturalHeight first pins OpenCV to the same coordinate space.
function toNaturalCanvas(image: HTMLImageElement): HTMLCanvasElement {
  const canvas = document.createElement("canvas");
  canvas.width = image.naturalWidth;
  canvas.height = image.naturalHeight;
  const ctx = canvas.getContext("2d");
  ctx?.drawImage(image, 0, 0, image.naturalWidth, image.naturalHeight);
  return canvas;
}

function getCornerPoints(contour: any): CornerPoints | null {
  const cv = getCv();
  const rect = cv.minAreaRect(contour);
  const center = rect.center;

  let topLeftCorner: Point | undefined;
  let topLeftCornerDist = 0;
  let topRightCorner: Point | undefined;
  let topRightCornerDist = 0;
  let bottomLeftCorner: Point | undefined;
  let bottomLeftCornerDist = 0;
  let bottomRightCorner: Point | undefined;
  let bottomRightCornerDist = 0;

  for (let i = 0; i < contour.data32S.length; i += 2) {
    const point = { x: contour.data32S[i], y: contour.data32S[i + 1] };
    const dist = distance(point, center);
    if (point.x < center.x && point.y < center.y) {
      if (dist > topLeftCornerDist) {
        topLeftCorner = point;
        topLeftCornerDist = dist;
      }
    } else if (point.x > center.x && point.y < center.y) {
      if (dist > topRightCornerDist) {
        topRightCorner = point;
        topRightCornerDist = dist;
      }
    } else if (point.x < center.x && point.y > center.y) {
      if (dist > bottomLeftCornerDist) {
        bottomLeftCorner = point;
        bottomLeftCornerDist = dist;
      }
    } else if (point.x > center.x && point.y > center.y) {
      if (dist > bottomRightCornerDist) {
        bottomRightCorner = point;
        bottomRightCornerDist = dist;
      }
    }
  }

  if (!topLeftCorner || !topRightCorner || !bottomLeftCorner || !bottomRightCorner) return null;
  return { topLeftCorner, topRightCorner, bottomLeftCorner, bottomRightCorner };
}

/**
 * Intenta detectar las 4 esquinas del documento en la imagen. Devuelve null
 * si no se encontró un contorno razonable (el usuario ajusta manualmente).
 */
export function detectDocumentCorners(image: HTMLImageElement): CornerPoints | null {
  const cv = getCv();
  const img = cv.imread(toNaturalCanvas(image));

  try {
    const imgGray = new cv.Mat();
    cv.Canny(img, imgGray, 50, 200);

    const imgBlur = new cv.Mat();
    cv.GaussianBlur(imgGray, imgBlur, new cv.Size(3, 3), 0, 0, cv.BORDER_DEFAULT);

    const imgThresh = new cv.Mat();
    cv.threshold(imgBlur, imgThresh, 0, 255, cv.THRESH_OTSU);

    const contours = new cv.MatVector();
    const hierarchy = new cv.Mat();
    cv.findContours(imgThresh, contours, hierarchy, cv.RETR_CCOMP, cv.CHAIN_APPROX_SIMPLE);

    let maxArea = 0;
    let maxContourIndex = -1;
    for (let i = 0; i < contours.size(); i++) {
      const area = cv.contourArea(contours.get(i));
      if (area > maxArea) {
        maxArea = area;
        maxContourIndex = i;
      }
    }

    // Ignora contornos muy pequeños (probablemente ruido, no el documento).
    const minArea = (img.rows * img.cols) * 0.1;
    let result: CornerPoints | null = null;
    if (maxContourIndex >= 0 && maxArea >= minArea) {
      result = getCornerPoints(contours.get(maxContourIndex));
    }

    imgGray.delete();
    imgBlur.delete();
    imgThresh.delete();
    contours.delete();
    hierarchy.delete();
    return result;
  } finally {
    img.delete();
  }
}

/**
 * Recorta y endereza la imagen según las 4 esquinas dadas (corrección de
 * perspectiva), y devuelve un canvas con el resultado.
 */
export function extractDocument(
  image: HTMLImageElement,
  corners: CornerPoints,
  resultWidth: number,
  resultHeight: number
): HTMLCanvasElement {
  const cv = getCv();
  const canvas = document.createElement("canvas");
  const img = cv.imread(toNaturalCanvas(image));

  try {
    const dsize = new cv.Size(resultWidth, resultHeight);
    const srcTri = cv.matFromArray(4, 1, cv.CV_32FC2, [
      corners.topLeftCorner.x,
      corners.topLeftCorner.y,
      corners.topRightCorner.x,
      corners.topRightCorner.y,
      corners.bottomLeftCorner.x,
      corners.bottomLeftCorner.y,
      corners.bottomRightCorner.x,
      corners.bottomRightCorner.y,
    ]);
    const dstTri = cv.matFromArray(4, 1, cv.CV_32FC2, [
      0, 0, resultWidth, 0, 0, resultHeight, resultWidth, resultHeight,
    ]);

    const m = cv.getPerspectiveTransform(srcTri, dstTri);
    const warped = new cv.Mat();
    cv.warpPerspective(img, warped, m, dsize, cv.INTER_LINEAR, cv.BORDER_CONSTANT, new cv.Scalar());
    cv.imshow(canvas, warped);

    srcTri.delete();
    dstTri.delete();
    m.delete();
    warped.delete();
    return canvas;
  } finally {
    img.delete();
  }
}

/** Aumenta contraste y desatura levemente para dar look de "escaneado". */
export function applyScanLook(canvas: HTMLCanvasElement): void {
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
  const data = imageData.data;
  const contrast = 1.35;
  const brightness = 12;
  const intercept = 128 * (1 - contrast) + brightness;

  for (let i = 0; i < data.length; i += 4) {
    const gray = data[i] * 0.34 + data[i + 1] * 0.5 + data[i + 2] * 0.16;
    const mix = 0.55; // 0 = color original, 1 = blanco y negro total
    for (let c = 0; c < 3; c++) {
      const original = data[i + c];
      const value = original * (1 - mix) + gray * mix;
      data[i + c] = Math.min(255, Math.max(0, value * contrast + intercept));
    }
  }
  ctx.putImageData(imageData, 0, 0);
}
