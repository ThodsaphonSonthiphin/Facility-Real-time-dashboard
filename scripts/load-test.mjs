#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { performance } from 'node:perf_hooks';
import { fileURLToPath } from 'node:url';

const address = process.env.LOAD_BASE_URL ?? process.env.E2E_BASE_URL;
if (!address || !['127.0.0.1', 'localhost', '[::1]'].includes(new URL(address).hostname)) {
  throw new Error('Run npm run test:load or set LOAD_BASE_URL to an isolated loopback API seeded with the large dataset.');
}
const baseUrl = new URL(address).origin;
const adminUsername = process.env.LOAD_ADMIN_USER ?? 'admin';
const initPath = fileURLToPath(
  new URL('../backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/DbInitializer.cs', import.meta.url),
);
const initSource = readFileSync(initPath, 'utf8');
const seedAdminPassword = /Username = "admin"[^\n]*?Hash\("([^"]+)"\)/.exec(initSource)?.[1];
const adminPassword = process.env.LOAD_ADMIN_PASS ?? seedAdminPassword;
if (!adminPassword) {
  throw new Error('Could not resolve admin password via LOAD_ADMIN_PASS or DbInitializer.cs');
}
const cleanerId = process.env.LOAD_CLEANER_ID ?? '1000001';
const cleanerPhone = process.env.LOAD_CLEANER_PHONE ?? '0810000001';

async function login(path, body) {
  const res = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    signal: AbortSignal.timeout(10_000),
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(`Login ${path} failed: ${res.status} ${await res.text()}`);
  }
  const data = await res.json();
  return data.accessToken;
}

function percentile(sorted, p) {
  const idx = Math.min(sorted.length - 1, Math.ceil((p / 100) * sorted.length) - 1);
  return sorted[idx];
}

async function runScenario({ name, url, token, concurrency = 10, total = 50, p95BudgetMs }) {
  // Prime .NET JIT and EF Core query plan compilation once before measuring concurrent steady-state latency.
  const warmup = await fetch(`${baseUrl}${url}`, {
    signal: AbortSignal.timeout(10_000),
    headers: { authorization: `Bearer ${token}` },
  });
  await warmup.arrayBuffer();
  if (!warmup.ok) {
    throw new Error(`Warmup ${name} failed: ${warmup.status}`);
  }

  const latencies = [];
  let errors = 0;
  let next = 0;

  async function worker() {
    while (true) {
      const i = next++;
      if (i >= total) return;
      const start = performance.now();
      try {
        const res = await fetch(`${baseUrl}${url}`, {
          signal: AbortSignal.timeout(10_000),
          headers: { authorization: `Bearer ${token}` },
        });
        await res.arrayBuffer();
        if (!res.ok) {
          errors += 1;
        } else {
          latencies.push(performance.now() - start);
        }
      } catch {
        errors += 1;
      }
    }
  }

  await Promise.all(Array.from({ length: concurrency }, worker));
  latencies.sort((a, b) => a - b);
  const p50 = latencies.length ? percentile(latencies, 50) : Number.NaN;
  const p95 = latencies.length ? percentile(latencies, 95) : Number.NaN;
  const p99 = latencies.length ? percentile(latencies, 99) : Number.NaN;
  const passed = errors === 0 && p95 <= p95BudgetMs;

  console.log(
    `${passed ? 'PASS' : 'FAIL'} ${name}: total=${total} conc=${concurrency} errors=${errors} p50=${p50.toFixed(1)}ms p95=${p95.toFixed(1)}ms (budget <=${p95BudgetMs}ms) p99=${p99.toFixed(1)}ms`,
  );
  return passed;
}

const adminToken = await login('/api/auth/login', { username: adminUsername, password: adminPassword });
const cleanerToken = await login('/api/auth/login', { employeeId: cleanerId, phone: cleanerPhone });

const results = [
  await runScenario({ name: 'Admin Overview (/api/admin/overview/areas)', url: '/api/admin/overview/areas?group=notEntered', token: adminToken, p95BudgetMs: 500 }),
  await runScenario({ name: 'Admin Service Points (/api/service-points)', url: '/api/service-points', token: adminToken, p95BudgetMs: 200 }),
  await runScenario({ name: 'Cleaner My Work (/api/my-work)', url: '/api/my-work', token: cleanerToken, p95BudgetMs: 200 }),
];

if (!results.every(Boolean)) {
  process.exit(1);
}
