/// <reference types="vite/client" />

// Bridge injected by the Electron shell's preload (src/desktop/src/preload.ts). Undefined in the
// browser web-shell — its presence is how the SPA knows it's running inside the desktop app.
interface Window {
  jobfinderDesktop?: {
    quit: () => void
  }
}
