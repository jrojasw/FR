import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // pdfkit lee sus archivos de fuentes (.afm) con rutas relativas a su propio
  // módulo en tiempo de ejecución; si Next lo empaqueta, esas rutas se rompen
  // (ENOENT). Se deja fuera del bundle para que se cargue tal cual desde
  // node_modules.
  serverExternalPackages: ["pdfkit"],
  experimental: {
    serverActions: {
      bodySizeLimit: "8mb",
    },
  },
};

export default nextConfig;
