import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In `npm run dev`, the orchestrator picks free ports and hands them in via env.
// When running vite standalone (`npm --prefix .../Client run dev`), both fall
// back to vite defaults — useful only for client-only debugging without dotnet.
const vitePort = parseInt(process.env.JOBFINDER_VITE_PORT ?? '', 10)
const apiTarget = process.env.JOBFINDER_API_TARGET

export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    port: Number.isFinite(vitePort) && vitePort > 0 ? vitePort : undefined,
    strictPort: Number.isFinite(vitePort) && vitePort > 0,
    proxy: apiTarget
      ? {
          // /api/search streams SSE — http-proxy passes the response through
          // unbuffered, so EventSource works through the proxy unmodified.
          '/api': { target: apiTarget, changeOrigin: false },
        }
      : undefined,
  },
})
