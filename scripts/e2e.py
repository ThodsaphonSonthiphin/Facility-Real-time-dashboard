#!/usr/bin/env python3
"""Real API + MySQL + headed Chromium. Never adopts or resets an existing database."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import subprocess
import sys
import time
import urllib.request
from e2e_report import generate

ROOT = Path(__file__).resolve().parents[1]
RUNS = ROOT / '.run' / 'e2e'
BROWSERS = ('chromium', 'firefox', 'webkit')  # E2E_BROWSER; one engine per run, each on its own database
SAFE_DB = re.compile(r'facility_e2e_[0-9]{14}_[0-9a-f]{8}\Z')


def sql(statement):
    env = os.environ.copy()
    env['MYSQL_PWD'] = env.get('E2E_MYSQL_PASSWORD', '')
    result = subprocess.run(['mysql', '--no-defaults', '-u', 'root', '--batch',
                             '--skip-column-names'], input=statement, text=True,
                            capture_output=True, env=env, check=True)
    return result.stdout.strip()


def execute(args, *, cwd=ROOT, env=None):
    print('+', ' '.join(map(str, args)), flush=True)
    command = list(map(str, args))
    if env and env.get('E2E_RUN_DIR'):
        log_path = Path(env['E2E_RUN_DIR']) / 'steps.log'
        with log_path.open('a') as log:
            log.write('+ ' + ' '.join(command) + '\n')
            log.flush()
            result = subprocess.run(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT)
        if result.returncode:
            print('\n'.join(log_path.read_text().splitlines()[-35:]), file=sys.stderr)
            result.check_returncode()
    else:
        subprocess.run(command, cwd=cwd, env=env, check=True)


def clean_run(run_dir):
    # No legacy database cleanup, pid-based process killing, or shared wwwroot removal.
    run_dir = run_dir.resolve()
    if run_dir.parent != RUNS.resolve():
        raise ValueError('Cleanup requires a direct child of .run/e2e')
    manifest_path = run_dir / 'ownership.json'
    state = json.loads(manifest_path.read_text())
    database, user = state['database'], state['user']
    if not SAFE_DB.fullmatch(database) or user != 'e2e_' + database.rsplit('_', 1)[1]:
        raise ValueError('Invalid E2E ownership manifest')
    if state.get('cleaned'):
        return
    # Both the private manifest and database marker must agree before any DROP.
    if sql(f"SELECT token FROM `{database}`.__e2e_owner") != state['token']:
        raise ValueError('Database ownership marker does not match; refusing cleanup')
    # Preserve the marker until user deletion succeeds, so either DROP can be retried.
    if state.get('userCreated'):
        sql(f"DROP USER IF EXISTS '{user}'@'localhost';")
    sql(f"DROP DATABASE `{database}`;")
    state['cleaned'] = True
    manifest_path.write_text(json.dumps(state, indent=2) + '\n')
    print(f'Cleaned owned synthetic database {database}; evidence retained at {run_dir}', flush=True)


def ensure_port_free(port):
    """Refuse a port another process listens on. SO_REUSEADDR lets the previous run's TIME_WAIT pass, as Kestrel does."""
    with socket.socket() as probe:
        probe.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        probe.bind(('127.0.0.1', port))
        probe.listen()


def run(args, playwright_args):
    for program in ['mysql', 'dotnet', 'node', 'npm', 'npx']:
        if not shutil.which(program):
            raise RuntimeError(f'Missing prerequisite: {program}')
    sql('SELECT 1')
    browser = os.environ.get('E2E_BROWSER', 'chromium')
    if browser not in BROWSERS:
        raise RuntimeError(f'E2E_BROWSER must be one of {", ".join(BROWSERS)}')
    port = int(os.environ.get('E2E_PORT', '5101'))
    ensure_port_free(port)
    stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%d%H%M%S')
    suffix = secrets.token_hex(4)
    database = f'facility_e2e_{stamp}_{suffix}'
    user, password, token = f'e2e_{suffix}', secrets.token_hex(24), secrets.token_hex(24)
    run_dir = RUNS / f'{stamp}_{suffix}'
    run_dir.mkdir(parents=True, mode=0o700)
    app_dir = run_dir / 'app'
    env = os.environ.copy()
    env.update({
        'ASPNETCORE_ENVIRONMENT': 'Development',
        'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
        'ASPNETCORE_CONTENTROOT': str(app_dir),
        'ASPNETCORE_WEBROOT': str(app_dir / 'wwwroot'),
        'ConnectionStrings__DefaultConnection':
            f'Server=localhost;Port=3306;Database={database};Uid={user};Pwd={password};CharSet=utf8mb4;',
        'Jwt__SigningKey': secrets.token_urlsafe(64),
        'E2E_BASE_URL': f'http://127.0.0.1:{port}',
        'E2E_RUN_DIR': str(run_dir), 'E2E_DATASET': args.dataset, 'E2E_SEED_TOKEN': token,
    })
    process, owned, exit_code = None, False, 1
    started = time.monotonic()
    try:
        execute(['dotnet', 'publish', 'backend/src/FacilityRealtime.Api', '-c', 'Release',
                 '-o', app_dir, '--nologo', '-v', 'quiet'], env=env)
        execute(['npx', 'tsc', '-b'], cwd=ROOT / 'frontend', env=env)
        execute(['npx', 'vite', 'build', '--outDir', app_dir / 'wwwroot'], cwd=ROOT / 'frontend', env=env)
        # CREATE without IF NOT EXISTS refuses to adopt an existing database.
        sql(f"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;")
        owned = True
        sql(f"CREATE TABLE `{database}`.__e2e_owner (token VARCHAR(48) NOT NULL); "
            f"INSERT INTO `{database}`.__e2e_owner VALUES ('{token}');")
        state = {'database': database, 'user': user, 'token': token, 'cleaned': False, 'userCreated': False}
        (run_dir / 'ownership.json').write_text(json.dumps(state, indent=2) + '\n')
        sql(f"CREATE USER '{user}'@'localhost' IDENTIFIED BY '{password}';")
        state['userCreated'] = True
        (run_dir / 'ownership.json').write_text(json.dumps(state, indent=2) + '\n')
        sql(f"GRANT ALL PRIVILEGES ON `{database}`.* TO '{user}'@'localhost';")
        with (run_dir / 'api.log').open('w') as log:
            process = subprocess.Popen(['dotnet', str(app_dir / 'FacilityRealtime.Api.dll')],
                                       cwd=app_dir, env=env, stdout=log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError(f'API exited; inspect {run_dir / "api.log"}')
            try:
                with urllib.request.urlopen(env['E2E_BASE_URL'] + '/api/health', timeout=1) as response:
                    if response.status == 200:
                        break
            except (OSError, TimeoutError):
                time.sleep(0.25)
        else:
            raise RuntimeError(f'API did not become ready; inspect {run_dir / "api.log"}')
        if not int(sql(f'SELECT COUNT(*) FROM `{database}`.users')):
            raise RuntimeError('Development seed missing from owned database')
        if args.dataset != 'baseline':
            execute(['dotnet', 'run', '--project', 'backend/tools/FacilityRealtime.E2ESeed',
                     '-c', 'Release', '--', args.dataset], env=env)
            execute(['dotnet', 'run', '--no-build', '--project', 'backend/tools/FacilityRealtime.E2ESeed',
                     '-c', 'Release', '--', args.dataset], env=env)
            first = json.loads((run_dir / 'dataset.json').read_text())
            repeated = json.loads((run_dir / 'dataset-repeat.json').read_text())
            if first['counts'] != repeated['counts'] or not repeated['reused']:
                raise RuntimeError('Repeated seed changed dataset counts')
        execute(['npx', 'playwright', 'install', browser], cwd=ROOT / 'frontend', env=env)
        result = subprocess.run(['npx', 'playwright', 'test', *playwright_args],
                                cwd=ROOT / 'frontend', env=env)
        exit_code = result.returncode
        return exit_code
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        (run_dir / 'run.json').write_text(json.dumps({
            'database': database, 'dataset': args.dataset, 'baseURL': env['E2E_BASE_URL'],
            'elapsedSeconds': round(time.monotonic() - started, 2), 'exitCode': exit_code,
            'browser': browser, 'headed': os.environ.get('E2E_HEADLESS') != '1', 'playwrightArgs': playwright_args,
        }, indent=2) + '\n')
        if owned and (run_dir / 'ownership.json').exists():
            clean_run(run_dir)
        generate(run_dir)
        print(f'E2E evidence: {run_dir}', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    run_parser = sub.add_parser('run')
    run_parser.add_argument('--dataset', choices=['baseline', 'small', 'medium', 'large'], default='baseline')
    cleanup = sub.add_parser('clean')
    cleanup.add_argument('run_directory', type=Path)
    args, extra = parser.parse_known_args()
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(143))
    if args.command == 'clean':
        if extra:
            parser.error('Unexpected cleanup arguments')
        clean_run(args.run_directory)
        return 0
    return run(args, extra)


if __name__ == '__main__':
    sys.exit(main())
