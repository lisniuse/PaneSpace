import { Resvg } from '@resvg/resvg-js';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import opentype from 'opentype.js';

const assets = new URL('../../assets/branding/', import.meta.url);
const iconSvg = readFileSync(new URL('icon.svg', assets), 'utf8');
const fontPath = weight => fileURLToPath(new URL(`node_modules/@fontsource/inter/files/inter-latin-${weight}-normal.woff`, import.meta.url));
function font(weight) {
  const data = readFileSync(fontPath(weight));
  return opentype.parse(data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength));
}
function textPath(face, text, x, y, size) {
  let previous;
  const scale = size / face.unitsPerEm;
  // These ASCII labels need only cmap glyphs and kerning, not text shaping.
  return Array.from(text, character => {
    const glyph = face.charToGlyph(character);
    if (previous) x += face.getKerningValue(previous, glyph) * scale;
    const path = glyph.getPath(x, y, size).toPathData(2);
    x += glyph.advanceWidth * scale;
    previous = glyph;
    return path;
  }).join(' ');
}
const wordmark = textPath(font(700), 'PaneSpace', 132, 64, 48);
const tagline = textPath(font(400), 'A canvas for your windows.', 134, 93, 17);
// Keep the vector mark in one source file. Wordmarks embed that exact geometry.
const mark = iconSvg.slice(iconSvg.indexOf('<defs>'), iconSvg.lastIndexOf('</svg>'));
for (const [name, ink, secondary] of [
  ['logo.svg', '#0b1220', '#52647a'],
  ['logo-dark.svg', '#f1f5f9', '#94a3b8'],
]) {
  const logo = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 420 120" width="420" height="120" role="img" aria-labelledby="title desc">
  <title id="title">PaneSpace</title>
  <desc id="desc">PaneSpace — a canvas for your windows.</desc>
  <g transform="translate(12 12) scale(1.5)">${mark}</g>
  <path d="${wordmark}" fill="${ink}"/>
  <path d="${tagline}" fill="${secondary}"/>
</svg>
`;
  writeFileSync(new URL(name, assets), logo);
}

function png(svg, width) {
  return new Resvg(svg, { fitTo: { mode: 'width', value: width } }).render().asPng();
}

// Windows ICO supports PNG frames; each size is rendered directly from the SVG.
const sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
const frames = sizes.map(size => png(iconSvg, size));
const directory = Buffer.alloc(6 + 16 * frames.length);
directory.writeUInt16LE(1, 2);
directory.writeUInt16LE(frames.length, 4);
let offset = directory.length;
frames.forEach((frame, index) => {
  const entry = 6 + index * 16;
  directory.writeUInt8(sizes[index] === 256 ? 0 : sizes[index], entry);
  directory.writeUInt8(sizes[index] === 256 ? 0 : sizes[index], entry + 1);
  directory.writeUInt16LE(1, entry + 4);
  directory.writeUInt16LE(32, entry + 6);
  directory.writeUInt32LE(frame.length, entry + 8);
  directory.writeUInt32LE(offset, entry + 12);
  offset += frame.length;
});
writeFileSync(new URL('icon.ico', assets), Buffer.concat([directory, ...frames]));
writeFileSync(new URL('icon.png', assets), png(iconSvg, 256));
console.log(`Brand assets generated in ${fileURLToPath(assets)}`);
