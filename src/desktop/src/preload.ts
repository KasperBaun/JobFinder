import { contextBridge, ipcRenderer } from 'electron'

// The only bridge the SPA needs: a way to ask the shell to quit. Its mere presence also lets the
// SPA tell it's running inside the desktop app (vs. the browser web-shell) and behave accordingly.
contextBridge.exposeInMainWorld('jobfinderDesktop', {
  quit: () => ipcRenderer.send('jobfinder:quit'),
})
