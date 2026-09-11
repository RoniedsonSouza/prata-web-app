#!/usr/bin/env node
/**
 * Gate RN-FRT-001 para Next 16 / Turbopack: nenhum chunk em
 * .next/static/chunks pode passar de 120 kB gzip.
 * (size-limit com glob soma todos os arquivos e invalida o orçamento.)
 */
import { gzipSync } from "node:zlib";
import { readdirSync, readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const LIMIT = 120 * 1024;
const dir = join(process.cwd(), ".next/static/chunks");

if (!existsSync(dir)) {
  console.error("Rode npm run build antes de npm run size.");
  process.exit(1);
}

const offenders = [];
for (const name of readdirSync(dir)) {
  if (!name.endsWith(".js")) continue;
  const gz = gzipSync(readFileSync(join(dir, name)), { level: 9 }).byteLength;
  if (gz > LIMIT) {
    offenders.push({ name, gz });
  }
}

if (offenders.length) {
  for (const o of offenders) {
    console.error(`EXCEDE ${(o.gz / 1024).toFixed(1)} kB gzip: ${o.name}`);
  }
  process.exit(1);
}

console.log("size OK: nenhum chunk JS > 120 kB gzip (RN-FRT-001).");
