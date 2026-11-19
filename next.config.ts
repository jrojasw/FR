import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // pdfkit lee sus archivos de fuentes (.afm) con rutas relativas a su propio
  // módulo en tiempo de ejecución; si Next lo empaqueta, esas rutas se rompen
  // (ENOENT). tesseract.js resuelve la ruta de su worker-script de la misma
  // forma (se rompe empaquetado: "Cannot find module .../worker-script/node").
  // pdf-parse usa pdfjs-dist, que tiene el mismo problema con sus workers.
  // Los tres se dejan fuera del bundle para que se carguen tal cual desde
  // node_modules.
  serverExternalPackages: ["pdfkit", "tesseract.js", "pdf-parse"],
  experimental: {
    serverActions: {
      bodySizeLimit: "8mb",
    },
  },
};

export default nextConfig;
