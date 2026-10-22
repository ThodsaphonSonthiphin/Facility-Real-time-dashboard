/// <reference types="vitest/config" />
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

/** facility-0068 item 3: the short commit shown on the login pages; "dev" outside a git checkout. */
function buildCommit(): string {
  try {
    return execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
  } catch {
    return 'dev';
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()], // facility-0086
  define: {
    __BUILD_COMMIT__: JSON.stringify(buildCommit()),
  },
  server: {
    host: '0.0.0.0', // Allow access from mobile devices on local WiFi
    port: 5173,
    https: process.env.FACILITY_DEV_CERT_DIR
      ? {
          key: readFileSync(join(process.env.FACILITY_DEV_CERT_DIR, 'dev-key.pem')),
          cert: readFileSync(join(process.env.FACILITY_DEV_CERT_DIR, 'dev-cert.pem')),
        }
      : undefined,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5001',
        ws: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
    // regression-net #32: tests fake the SignalR package the way fakeServer fakes fetch
    alias: { '@microsoft/signalr': fileURLToPath(new URL('./src/test/fakeSignalR.ts', import.meta.url)) },
  },
});
