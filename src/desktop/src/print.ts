import { BrowserWindow, dialog, ipcMain } from 'electron'
import * as fs from 'node:fs'
import { t } from './strings'

// Strips Windows-invalid and control characters so the renderer's `{company} - {title}.pdf`
// suggestion is always a usable default, whatever a job title contains.
export function sanitizePdfFileName(suggested: string): string {
  const base = suggested
    .replace(/\.pdf$/i, '')
    .replace(/[<>:"/\\|?*\u0000-\u001f]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/[. ]+$/, '')
    .slice(0, 150)
    .trim()
  return `${base || t().pdfDefaultName}.pdf`
}

// The SPA's "Save as PDF" button routes here (see preload): native save dialog, then a
// printToPDF capture of the page — the SPA's @media print CSS shows only the listing.
// Resolves true on save or user cancel; false means the save genuinely failed.
export function registerPrintToPdf(): void {
  ipcMain.handle('jobfinder:printToPdf', async (event, suggestedFileName: unknown) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (!win) return false
    try {
      const { canceled, filePath } = await dialog.showSaveDialog(win, {
        defaultPath: sanitizePdfFileName(typeof suggestedFileName === 'string' ? suggestedFileName : ''),
        filters: [{ name: t().pdfFilter, extensions: ['pdf'] }],
      })
      if (canceled || !filePath) return true
      const pdf = await win.webContents.printToPDF({ pageSize: 'A4', printBackground: true })
      await fs.promises.writeFile(filePath, pdf)
      return true
    } catch {
      return false
    }
  })
}
