import { app, BrowserWindow } from 'electron'
import * as path from 'node:path'

export function createMainWindow(port: number): BrowserWindow {
  const win = new BrowserWindow({
    width: 1280,
    height: 860,
    minWidth: 940,
    minHeight: 600,
    title: 'Jobfinder',
    backgroundColor: '#F4F6FB', // matches the SPA surface so there's no white flash
    // Packaged Windows uses the icon embedded in the exe by electron-builder; this covers dev + Linux.
    icon: app.isPackaged ? undefined : path.join(app.getAppPath(), 'build', 'icon.ico'),
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  win.setMenuBarVisibility(false)
  void win.loadURL(`http://127.0.0.1:${port}`)
  return win
}
