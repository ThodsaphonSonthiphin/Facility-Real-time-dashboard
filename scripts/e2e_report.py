#!/usr/bin/env python3
"""Produce an evidence index and measured timings from one Playwright JSON report."""
import base64
from collections import defaultdict
import json
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile


def specifications(suites):
    for suite in suites:
        yield from suite.get('specs', [])
        yield from specifications(suite.get('suites', []))


def generate(directory):
    directory = Path(directory).resolve()
    results_path = directory / 'results.json'
    if not results_path.exists():
        (directory / 'report.md').write_text('# E2E execution did not reach Playwright\n\nInspect api.log and the command output. No browser results are claimed.\n')
        return
    report = json.loads(results_path.read_text())
    stats = report['stats']
    metrics = defaultdict(list)
    lines = ['# E2E execution evidence', '',
             f"Passed: {stats['expected']}. Failed: {stats['unexpected']}. Skipped: {stats['skipped']}. Flaky: {stats['flaky']}.",
             f"Browser suite elapsed: {stats['duration'] / 1000:.2f} seconds.", '',
             'Real loopback API and MySQL; Chromium is headed by default at 350 ms slow motion. '
             'These durations include browser/trace overhead and are not concurrent load-test results.', '',
             '[HTML report](html/index.html) · [Machine-readable results](results.json) · [API log](api.log)', '']
    dataset_path = directory / 'dataset.json'
    if dataset_path.exists():
        dataset = json.loads(dataset_path.read_text())
        lines += [f"Dataset: {dataset['profile']}; anchor: {dataset['anchorDate']}; seed: {dataset['seedSeconds']} s.", '',
                  '| Entity | Actual rows before browser tests |', '| --- | ---: |']
        lines += [f'| {key} | {value} |' for key, value in dataset['counts'].items()]
        lines += ['', '[Dataset validation](dataset.json) · [Repeated seed validation](dataset-repeat.json)', '']
    lines += ['## Cases and artifacts', '', '| Project | Case | Status | Seconds |', '| --- | --- | --- | ---: |']
    failures = []
    for spec in specifications(report['suites']):
        for test in spec['tests']:
            result = test['results'][-1] if test['results'] else {}
            title = spec['title'].replace('|', '\\|')
            lines += [f"| {test['projectName']} | {title} | {result.get('status', test['status'])} | {result.get('duration', 0) / 1000:.2f} |"]
            if test['status'] == 'unexpected':
                failures.append((spec, result))
            for attachment in result.get('attachments', []):
                if attachment['name'] == 'browser-evidence' and 'body' in attachment:
                    evidence = json.loads(base64.b64decode(attachment['body']))
                    for response in evidence['responses']:
                        if response['milliseconds'] >= 0:
                            metrics[(response['method'], response['path'])].append(response['milliseconds'])
    if failures:
        lines += ['', '## Failures requiring investigation', '']
        for spec, result in failures:
            lines += [f"### {spec['title']}", '', f"Reproduce: run the same dataset with `--grep {json.dumps(spec['title'])}`.",
                      f"Source: {spec['file']}:{spec['line']}. Classification requires reviewing the error and trace; a failing test alone does not prove an application defect.", '', '```text',
                      '\n'.join(error.get('message', '') for error in result.get('errors', [])), '```', '']
    lines += ['', '## Measured API response durations', '',
              'Browser request start through response end, milliseconds, successful and rejected requests included. '
              'Different endpoint paths are reported separately. Small samples are observations, not service-level guarantees.', '',
              '| Method | Path | Samples | Median ms | p95 ms | Max ms |', '| --- | --- | ---: | ---: | ---: | ---: |']
    for (method, path), values in sorted(metrics.items()):
        ordered = sorted(values)
        lines += [f'| {method} | {path} | {len(values)} | {ordered[len(values) // 2]} | {ordered[math.ceil(len(values) * .95) - 1]} | {max(values)} |']
    lines += ['', '## Screenshots and traces', '']
    for artifact in sorted((directory / 'test-results').rglob('*')):
        if artifact.suffix in ('.png', '.zip'):
            path = artifact.relative_to(directory)
            lines.append(f'- [{path}]({path})')
    lines += ['', '## Downloaded workbook contents', '']
    for workbook in sorted((directory / 'test-results').rglob('*.xlsx')):
        with zipfile.ZipFile(workbook) as archive:
            if archive.testzip() is not None:
                raise ValueError(f'Corrupt XLSX archive: {workbook}')
            sheets = {}
            for name in archive.namelist():
                if name.startswith('xl/worksheets/sheet') and name.endswith('.xml'):
                    root = ET.fromstring(archive.read(name))
                    sheets[name] = len(root.findall('.//{http://schemas.openxmlformats.org/spreadsheetml/2006/main}row'))
            if not sheets or max(sheets.values()) <= 1:
                raise ValueError(f'Workbook contains no data rows: {workbook}')
            lines += [f'- {workbook.name}: {json.dumps(sheets)} (row counts include headers).']
    lines += ['', '## Coverage boundaries', '',
              'Only passed cases above are verified. No claims about Safari, real GPS hardware, paper printing, '
              'network loss/recovery, concurrent-user capacity, or every possible business rule. '
              'Account/Area pages use client-side lists; history has server cursor pagination. '
              'No user-selectable table sorting exists; tests check the implemented default ordering.', '']
    (directory / 'report.md').write_text('\n'.join(lines))


if __name__ == '__main__':
    generate(sys.argv[1])
