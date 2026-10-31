import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import {
  catalogApi,
  MAX_INDEX_BYTES,
  releaseAssets,
  publishedCatalog,
  readCatalog,
} from '../src/community/model.ts';

const root = fileURLToPath(new URL('../', import.meta.url));
const target = join(root, 'public/community/catalog.json');

async function readBytes(response, maxBytes) {
  if (!response.ok || Number(response.headers.get('content-length')) > maxBytes)
    throw new Error('Catalog response unavailable or too large');
  const reader = response.body.getReader();
  const chunks = [];
  let size = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maxBytes) throw new Error('Catalog response too large');
      chunks.push(value);
    }
  } finally {
    await reader.cancel();
  }
  return Buffer.concat(chunks);
}

/** Fetch only metadata and the index during build. No package archives or preview images are bundled. */
export async function fetchCatalog(request = fetch) {
  const options = {
    signal: AbortSignal.timeout(10000),
    headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'MiniInfos-site' },
  };
  const release = JSON.parse(
    (await readBytes(await request(catalogApi, options), 4 * MAX_INDEX_BYTES)).toString('utf8'),
  );
  const assets = releaseAssets(release);
  const indexes = assets.filter((asset) => asset.name === 'index.json');
  if (indexes.length !== 1 || indexes[0].size > MAX_INDEX_BYTES)
    throw new Error('Catalog index unavailable');
  const bytes = await readBytes(
    await request(indexes[0].browser_download_url, { signal: AbortSignal.timeout(10000) }),
    MAX_INDEX_BYTES,
  );
  if (bytes.byteLength !== indexes[0].size) throw new Error('Incomplete catalog index');
  const digest = release.assets.find((asset) => asset.name === 'index.json')?.digest;
  if (digest && digest !== 'sha256:' + createHash('sha256').update(bytes).digest('hex'))
    throw new Error('Catalog index checksum mismatch');
  return publishedCatalog(JSON.parse(bytes.toString('utf8')), assets);
}

export async function syncCatalog() {
  try {
    const catalog = await fetchCatalog();
    await mkdir(join(root, 'public/community'), { recursive: true });
    await writeFile(target, JSON.stringify(catalog, null, 2) + '\n');
    console.log(`Catalog: ${catalog.packages.length} published packages, fixed tag catalog.`);
  } catch (error) {
    // The last validated snapshot keeps an offline build usable without inventing packages.
    try {
      readCatalog(JSON.parse(await readFile(target, 'utf8')));
    } catch {
      throw new Error('No valid catalog snapshot is available.', { cause: error });
    }
    console.warn('Catalog refresh unavailable; keeping the last validated snapshot.');
  }
}
if (process.argv[1] === fileURLToPath(import.meta.url)) await syncCatalog();
