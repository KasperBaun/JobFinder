// Regenerates the committed app icons from build/icon-source.svg.
//   build/icon.png       — 1024x1024 master raster
//   build/icon.ico       — Windows multi-size (16..256)
//   build/icons/NxN.png  — Linux hicolor set (electron-builder installs each into
//                          /usr/share/icons/hicolor/NxN/apps). A single 1024 png is NOT
//                          enough: GNOME's icon theme ignores the non-standard 1024 size.
// Run when the source SVG changes: `npm run icons`. The outputs are committed so CI needs no sharp.
import sharp from 'sharp'
import pngToIco from 'png-to-ico'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const buildDir = resolve(dirname(fileURLToPath(import.meta.url)), '..', 'build')
const svg = readFileSync(resolve(buildDir, 'icon-source.svg'))

const pngPath = resolve(buildDir, 'icon.png')
const icoPath = resolve(buildDir, 'icon.ico')

await sharp(svg).resize(1024, 1024).png().toFile(pngPath)

const sizes = [16, 24, 32, 48, 64, 128, 256]
const frames = await Promise.all(sizes.map((s) => sharp(svg).resize(s, s).png().toBuffer()))
writeFileSync(icoPath, await pngToIco(frames))

const iconsDir = resolve(buildDir, 'icons')
mkdirSync(iconsDir, { recursive: true })
const linuxSizes = [16, 32, 48, 64, 128, 256, 512]
await Promise.all(
  linuxSizes.map((s) => sharp(svg).resize(s, s).png().toFile(resolve(iconsDir, `${s}x${s}.png`))),
)

console.log(`wrote ${pngPath}`)
console.log(`wrote ${icoPath}`)
console.log(`wrote ${iconsDir}/{${linuxSizes.map((s) => `${s}x${s}`).join(',')}}.png`)
