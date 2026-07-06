import { BrowserWindow } from 'electron'
import { loadWindowState, trackWindowState } from './window-state'
import { appIconPath } from './icon'

export function createMainWindow(port: number): BrowserWindow {
  const state = loadWindowState()

  const win = new BrowserWindow({
    x: state.x,
    y: state.y,
    width: state.width,
    height: state.height,
    minWidth: state.minWidth,
    minHeight: state.minHeight,
    center: state.x === undefined,
    show: false, // avoid a first-paint flash; reveal once the SPA is ready
    title: 'Jobfinder',
    backgroundColor: '#F4F6FB', // matches the SPA surface so there's no white flash
    icon: appIconPath(),
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  if (state.isMaximized) win.maximize()
  win.setMenuBarVisibility(false)
  win.once('ready-to-show', () => win.show())
  trackWindowState(win)

  void win.loadURL(`http://127.0.0.1:${port}`)
  return win
}
