import { app } from 'electron'
import * as path from 'node:path'

// Dev + Linux load the icon file directly; packaged Windows uses the icon embedded in the exe by
// electron-builder, so undefined is correct there (letting the exe icon apply to the window/taskbar).
export function appIconPath(): string | undefined {
  return app.isPackaged ? undefined : path.join(app.getAppPath(), 'build', 'icon.ico')
}
