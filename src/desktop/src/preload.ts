import { contextBridge, ipcRenderer } from 'electron'

// The bridge's mere presence also lets the SPA tell it's running inside the desktop app (vs. the
// browser web-shell) and behave accordingly.
contextBridge.exposeInMainWorld('jobfinderDesktop', {
  quit: () => ipcRenderer.send('jobfinder:quit'),
  // Native save dialog + PDF capture of the posting page itself (loaded hidden); resolves false
  // when the page couldn't be fetched or the save failed.
  printSourceToPdf: (url: string, suggestedFileName: string): Promise<boolean> =>
    ipcRenderer.invoke('jobfinder:printSourceToPdf', url, suggestedFileName),
  // Native save dialog + PDF capture of the current page; resolves false when the save failed.
  printToPdf: (suggestedFileName: string): Promise<boolean> =>
    ipcRenderer.invoke('jobfinder:printToPdf', suggestedFileName),
})
