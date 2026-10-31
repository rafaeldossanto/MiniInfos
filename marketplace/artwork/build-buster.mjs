import { createRequire } from "node:module";
import { readFile, writeFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// Asset packing only: the new poses are generated artwork, not duplicated or interpolated stills.
// `sharp` can be supplied by a local art toolchain without adding it to the app or the site.
const require = createRequire(import.meta.url);
const sharp = require(process.env.MINIINFOS_SHARP_MODULE || "sharp");
const here = dirname(fileURLToPath(import.meta.url));
const atlas = resolve(here, "../packages/community.buster-beaver/sprites.png");
const source = resolve(here, "community.buster-beaver/head-turns.png");
const original = await readFile(process.argv[2] || atlas);
const generated = await readFile(source);
const metadata = await sharp(generated).metadata();
const cell = 320;
const originalRaw = await sharp(original)
  .ensureAlpha()
  .raw()
  .toBuffer({ resolveWithObject: true });
const atlasWidth = 16 * cell;
const pixels = Buffer.alloc(atlasWidth * 10 * cell * 4);
function place(data, width, height, left, top) {
  for (let y = 0; y < height; y++)
    data.copy(
      pixels,
      ((top + y) * atlasWidth + left) * 4,
      y * width * 4,
      (y + 1) * width * 4,
    );
}
for (let row = 0; row < 10; row++) {
  if (row === 0 || row === 2) continue;
  // Copy raw RGBA rows, including RGB under alpha zero. Alpha compositing would change edge pixels.
  for (let y = 0; y < cell; y++) {
    const source = (row * cell + y) * originalRaw.info.width * 4;
    originalRaw.data.copy(
      pixels,
      (row * cell + y) * atlasWidth * 4,
      source,
      source + 8 * cell * 4,
    );
  }
}
for (let frame = 0; frame < 16; frame++) {
  const x = frame % 4,
    y = Math.floor(frame / 4);
  const left = Math.floor((x * metadata.width) / 4),
    top = Math.floor((y * metadata.height) / 4);
  const width = Math.floor(((x + 1) * metadata.width) / 4) - left,
    height = Math.floor(((y + 1) * metadata.height) / 4) - top;
  // One scale and one offset for every pose: the character keeps its original 76..299px footprint.
  const input = await sharp(generated)
    .extract({ left, top, width, height })
    .resize(264, 264, { fit: "fill" })
    .ensureAlpha()
    .raw()
    .toBuffer();
  for (const row of [0, 2])
    place(input, 264, 264, frame * cell + 15, row * cell + 47);
}
const output = await sharp(pixels, {
  raw: { width: atlasWidth, height: 10 * cell, channels: 4 },
})
  .png({ compressionLevel: 9, adaptiveFiltering: true })
  .toBuffer();
await writeFile(atlas, output);
console.log(
  `Buster: 16 columns, 10 rows, ${output.length} PNG bytes. Other 8 animation rows preserved.`,
);
