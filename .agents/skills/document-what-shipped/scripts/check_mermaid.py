"""Parse every Mermaid block on a page before it is published.

Why this exists: a diagram that does not parse renders as an error box, and NOTHING else catches
it. The publish probes check sentences, the link check checks links, and both pass. Measured on
2026-08-20 a sequence diagram was published containing

    CRM-->>Portal: Quote closes; chosen schedule recorded

and it never rendered once. A semicolon TERMINATES a statement in mermaid, so the tail is parsed as
a new statement, the parser reads "chosen" as an actor id and demands an arrow. The page carried an
error box for a day and two runs went past it, because every assert those runs made passed.

Two modes, and the difference is stated in the output rather than hidden:

  PARSE     node + mermaid are available -> every block is handed to the real parser. Authoritative.
  SCAN      they are not -> a static scan for the characters known to break a block. A heuristic:
            it catches the measured killers and cannot catch a novel syntax error.

  python check_mermaid.py page.md [more.md ...]              # auto: parse if it can, else scan
  python check_mermaid.py --scan-only page.md                # force the heuristic
  python check_mermaid.py --setup                            # print the one-time node setup

Exit 1 if any block fails, or - in SCAN mode - if any suspect character is found.
"""
import argparse
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

# The fence belongs to the destination, not the writer: an Azure DevOps wiki uses a ::: container,
# GitHub and a repo docs/ folder use a triple-backtick fence. Both are recognised here so one
# command works before you have decided where the page lands.
ADO_OPEN = re.compile(r"^\s*:::\s*mermaid\s*$")
ADO_CLOSE = re.compile(r"^\s*:::\s*$")
GH_OPEN = re.compile(r"^\s*```+\s*mermaid\s*$")
GH_CLOSE = re.compile(r"^\s*```+\s*$")

# Measured killers. Each one is a character that ENDS a token the writer thinks is still open.
SUSPECT = {
    ";": "terminates a statement - the tail becomes a new statement and the parser demands an arrow",
    "#": "opens an entity code / comment - the rest of the label is swallowed",
}

PROBE = r"""
import {JSDOM} from 'jsdom';
import {readFileSync} from 'node:fs';
const dom = new JSDOM('<!doctype html><html><body></body></html>');
globalThis.window = dom.window;
globalThis.document = dom.window.document;
try { Object.defineProperty(globalThis, 'navigator',
      {value: dom.window.navigator, configurable: true}); } catch (e) {}
const mermaid = (await import('mermaid')).default;
mermaid.initialize({startOnLoad: false});
const blocks = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const out = [];
for (const b of blocks) {
  try { await mermaid.parse(b.src); out.push({...b, ok: true}); }
  catch (e) { out.push({...b, ok: false, error: String(e.message).split('\n').slice(0,3).join(' | ')}); }
}
console.log(JSON.stringify(out.map(({src, ...rest}) => rest)));
"""

SETUP = """One-time setup for PARSE mode (a few seconds, ~150 packages):

  mkdir mermaid-gate && cd mermaid-gate
  npm init -y && npm i mermaid jsdom

Then point the gate at it:

  MERMAID_GATE_DIR=/path/to/mermaid-gate python check_mermaid.py page.md

Without it the gate runs in SCAN mode and says so. jsdom is needed because mermaid reaches for a
document even to parse; setting globalThis.navigator needs defineProperty, since on modern node it
has only a getter."""


def blocks_in(path):
    """Every mermaid block in one file, as (start_line, kind, source)."""
    with io.open(path, encoding="utf-8", newline="") as fh:
        lines = fh.read().replace("\r\n", "\n").split("\n")
    found, i = [], 0
    while i < len(lines):
        opener = ADO_OPEN if ADO_OPEN.match(lines[i]) else (GH_OPEN if GH_OPEN.match(lines[i]) else None)
        if opener is None:
            i += 1
            continue
        closer = ADO_CLOSE if opener is ADO_OPEN else GH_CLOSE
        start = i
        i += 1
        body = []
        while i < len(lines) and not closer.match(lines[i]):
            body.append(lines[i])
            i += 1
        kind = next((b.strip() for b in body if b.strip()), "(empty)")
        found.append((start + 1, kind, "\n".join(body)))
        i += 1
    return found


def gate_dir():
    d = os.environ.get("MERMAID_GATE_DIR")
    for cand in ([d] if d else []) + [os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                   "mermaid-gate")]:
        if cand and os.path.isdir(os.path.join(cand, "node_modules", "mermaid")):
            return cand
    return None


def parse_mode(all_blocks, where):
    payload = [{"file": f, "line": ln, "kind": k, "src": s} for f, ln, k, s in all_blocks]
    tmp = tempfile.mkdtemp()
    try:
        blob = os.path.join(tmp, "blocks.json")
        with io.open(blob, "w", encoding="utf-8") as fh:
            json.dump(payload, fh)
        probe = os.path.join(where, "_check_mermaid_probe.mjs")
        with io.open(probe, "w", encoding="utf-8") as fh:
            fh.write(PROBE)
        r = subprocess.run(["node", probe, blob], capture_output=True, text=True,
                           cwd=where, shell=(os.name == "nt"))
        if r.returncode != 0 or not r.stdout.strip():
            return None, (r.stderr or "node produced no output")[-400:]
        return json.loads(r.stdout.strip().splitlines()[-1]), None
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pages", nargs="*")
    ap.add_argument("--scan-only", action="store_true",
                    help="force the heuristic even when the parser is available")
    ap.add_argument("--setup", action="store_true", help="print the one-time node setup and exit")
    a = ap.parse_args()
    if a.setup:
        print(SETUP)
        return 0
    if not a.pages:
        ap.error("give at least one page file, or --setup")

    all_blocks = []
    for p in a.pages:
        for ln, kind, src in blocks_in(p):
            all_blocks.append((p, ln, kind, src))
    if not all_blocks:
        print("no mermaid blocks found - nothing to check")
        return 0

    where = None if a.scan_only else gate_dir()
    results, err = (parse_mode(all_blocks, where) if where else (None, None))

    bad = 0
    if results is not None:
        print("mode: PARSE - every block handed to the real mermaid parser\n")
        for r in results:
            tag = "OK  " if r["ok"] else "FAIL"
            print("  %s  %s line %s  %s" % (tag, os.path.basename(r["file"]), r["line"], r["kind"]))
            if not r["ok"]:
                bad += 1
                print("        " + r["error"])
        print("\n%d blocks parsed, %d failed" % (len(results), bad))
    else:
        if err:
            print("PARSE mode failed, falling back to SCAN: " + err + "\n")
        print("mode: SCAN - a heuristic, NOT a parse. It catches the measured killers below and")
        print("      cannot catch a novel syntax error. Run --setup to get PARSE mode.\n")
        for f, ln, kind, src in all_blocks:
            for n, line in enumerate(src.split("\n")):
                for ch, why in SUSPECT.items():
                    if ch in line:
                        bad += 1
                        print("  FAIL  %s line %s  %s" % (os.path.basename(f), ln + 1 + n, kind))
                        print("        %r %s" % (ch, why))
                        print("        " + line.strip())
        print("\n%d blocks scanned, %d suspect line(s)" % (len(all_blocks), bad))

    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
