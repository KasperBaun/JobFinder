import { BrowserWindow, dialog, ipcMain } from 'electron'
import * as fs from 'node:fs'
import { t } from './strings'

const SOURCE_LOAD_TIMEOUT_MS = 30_000
// Career sites are routinely client-rendered; give their scripts a moment to paint after load.
const SOURCE_SETTLE_MS = 2_500

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

// The SPA's "Save as PDF" button routes here (see preload). Both channels resolve true on save
// or user cancel; false means the save genuinely failed.
export function registerPrintToPdf(): void {
  // Preferred: capture the posting page itself — the same page "Open job posting" leads to —
  // so the PDF is the ad as the site renders it, not jobfinder's summary of it.
  ipcMain.handle('jobfinder:printSourceToPdf', async (event, url: unknown, suggestedFileName: unknown) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (!win || typeof url !== 'string' || !/^https?:\/\//i.test(url)) return false
    try {
      const filePath = await askSavePath(win, suggestedFileName)
      if (filePath === null) return true
      const pdf = await renderSourcePdf(url)
      await fs.promises.writeFile(filePath, pdf)
      return true
    } catch {
      return false
    }
  })

  // Fallback kept for the SPA's portal-capture path: printToPDF of the app page, where the
  // @media print CSS shows only the listing summary.
  ipcMain.handle('jobfinder:printToPdf', async (event, suggestedFileName: unknown) => {
    const win = BrowserWindow.fromWebContents(event.sender)
    if (!win) return false
    try {
      const filePath = await askSavePath(win, suggestedFileName)
      if (filePath === null) return true
      const pdf = await win.webContents.printToPDF({ pageSize: 'A4', printBackground: true })
      await fs.promises.writeFile(filePath, pdf)
      return true
    } catch {
      return false
    }
  })
}

// Native save dialog; null means the user cancelled.
async function askSavePath(win: BrowserWindow, suggestedFileName: unknown): Promise<string | null> {
  const { canceled, filePath } = await dialog.showSaveDialog(win, {
    defaultPath: sanitizePdfFileName(typeof suggestedFileName === 'string' ? suggestedFileName : ''),
    filters: [{ name: t().pdfFilter, extensions: ['pdf'] }],
  })
  return canceled || !filePath ? null : filePath
}

async function renderSourcePdf(url: string): Promise<Buffer> {
  const page = new BrowserWindow({
    show: false,
    webPreferences: { sandbox: true, contextIsolation: true, nodeIntegration: false },
  })
  page.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
  try {
    await withTimeout(page.loadURL(url), SOURCE_LOAD_TIMEOUT_MS)
    await new Promise(resolve => setTimeout(resolve, SOURCE_SETTLE_MS))
    return await page.webContents.printToPDF({ pageSize: 'A4', printBackground: true })
  } finally {
    page.destroy()
  }
}

function withTimeout<T>(work: Promise<T>, ms: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timed out after ${ms}ms`)), ms)
    work.then(
      value => { clearTimeout(timer); resolve(value) },
      error => { clearTimeout(timer); reject(error) },
    )
  })
}
