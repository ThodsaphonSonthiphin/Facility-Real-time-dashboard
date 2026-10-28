import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { brotliCompressSync, constants, gzipSync } from 'node:zlib';
import type { Plugin } from 'vite';

/** Text files worth compressing; images like PNG are compressed already. */
const COMPRESSIBLE = /\.(js|css|html|svg|json|txt|webmanifest)$/;

/**
 * facility-0067 amendment (2026-10-28): writes `name.br` and `name.gz` beside every text file of the build, at the
 * highest levels (brotli 11, gzip 9). The API sends these as they are, so a phone gets the main JS about a third
 * smaller than the API's on-the-fly compression, which uses the fastest level, and the server does no work per request.
 * A copy is kept only when it is smaller than the file.
 */
export function precompress(): Plugin {
  let outDir = '';
  return {
    name: 'facility-precompress',
    apply: 'build',
    configResolved(config) {
      outDir = resolve(config.root, config.build.outDir);
    },
    closeBundle() {
      precompressDir(outDir);
    },
  };
}

export function precompressDir(dir: string): void {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) {
      precompressDir(path);
    } else if (COMPRESSIBLE.test(name)) {
      const file = readFileSync(path);
      const br = brotliCompressSync(file, { params: { [constants.BROTLI_PARAM_QUALITY]: 11 } });
      const gz = gzipSync(file, { level: 9 });
      if (br.length < file.length) {
        writeFileSync(`${path}.br`, br);
      }
      if (gz.length < file.length) {
        writeFileSync(`${path}.gz`, gz);
      }
    }
  }
}
