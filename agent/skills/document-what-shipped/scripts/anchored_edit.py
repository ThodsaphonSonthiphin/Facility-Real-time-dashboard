"""Anchored edits to a page, safe enough to publish.

Four habits, each of which was learned the hard way:

  1. find a place by its OWN TEXT, never by a line number - a line number is stale the
     moment anybody else edits the page;
  2. assert the anchor text appears EXACTLY ONCE - a non-unique anchor silently edits the
     wrong copy, and a page holding both a live summary and a frozen history section has
     the same sentence twice on purpose;
  3. resolve every anchor BEFORE the first write, then apply the edits from the bottom of
     the file upward, so an earlier edit cannot move a later anchor;
  4. probe the result - and then READ THE PROSE. An assert proves an anchor matched. It
     cannot prove a sentence reads. A patch that replaced one line of a two-line sentence
     passed every assert and published "the Agent clears it with Add Booking, and the
     quote-list marker and the Agent clears it with Add Booking".

Usage:

    from anchored_edit import Page

    p = Page.from_file("wiki-685-before.md")

    i = p.find("**Quote (Agent).** The Agent creates a quote")
    p.replace(i, ["1. **Quote (Agent, then Customer).** ..."])

    j = p.find("Customer->>CRM: Reply by email")
    p.replace(j, ["    alt Customer presses a link", "    ...", "    end"], span=4)

    p.apply()
    p.probe([("The customer has no accept/reject button", False),
             ("Booking approval requested", True),
             ("    end", 2)])
    p.balanced("    alt ", lambda ln: ln.strip() == "end")
    p.save("wiki-685-after-DRAFT.md", backup_dir=".")

Every step prints what it did, and any failure raises rather than writing a file. ASCII
source on purpose: this file is copied through shells that mangle anything else.
"""

import os
import shutil
import sys


class AnchorError(AssertionError):
    pass


class Page(object):
    def __init__(self, text, name="<memory>"):
        self.name = name
        self.eol = "\r\n" if "\r\n" in text else "\n"
        self.lines = text.replace("\r\n", "\n").split("\n")
        self._edits = []       # (index, new_lines, span)
        self._applied = False

    @classmethod
    def from_file(cls, path):
        with open(path, "rb") as f:
            raw = f.read().decode("utf-8")
        p = cls(raw, name=os.path.basename(path))
        print("read %s: %d lines, eol=%s" % (p.name, len(p.lines),
                                             "CRLF" if p.eol == "\r\n" else "LF"))
        return p

    # ---- locating ----

    def find_all(self, sub, start=0, end=None):
        end = len(self.lines) if end is None else end
        return [i for i in range(start, end) if sub in self.lines[i]]

    def find(self, sub, start=0, end=None):
        """Index of the ONE line containing sub. Raises unless it is unique."""
        hits = self.find_all(sub, start, end)
        if len(hits) != 1:
            raise AnchorError("anchor not unique (%d hits): %r -> %s" % (len(hits), sub, hits))
        return hits[0]

    def expect_near(self, index, offset, sub):
        """Guard a positional assumption: the line at index+offset must contain sub."""
        j = index + offset
        if j < 0 or j >= len(self.lines) or sub not in self.lines[j]:
            got = self.lines[j] if 0 <= j < len(self.lines) else "<out of range>"
            raise AnchorError("expected %r at %+d from line %d, found: %r" % (sub, offset, index, got))
        return j

    # ---- queueing edits (nothing is written until apply) ----

    def replace(self, index, new_lines, span=1):
        if span < 1:
            raise AnchorError("span must be >= 1")
        self._edits.append((index, list(new_lines), span))
        return self

    def insert_after(self, index, new_lines):
        self._edits.append((index + 1, list(new_lines), 0))
        return self

    def apply(self):
        if self._applied:
            raise AnchorError("apply() called twice")
        spans = []
        for idx, _new, span in self._edits:
            if span:
                spans.append((idx, idx + span - 1))
        spans.sort()
        for (a1, b1), (a2, _b2) in zip(spans, spans[1:]):
            if a2 <= b1:
                raise AnchorError("overlapping edits: lines %d-%d and %d-..." % (a1, b1, a2))
        for idx, new, span in sorted(self._edits, key=lambda e: -e[0]):
            self.lines[idx:idx + span] = new
        self._applied = True
        print("applied %d edits, now %d lines" % (len(self._edits), len(self.lines)))
        return self

    # ---- checking ----

    def text(self):
        return "\n".join(self.lines)

    def probe(self, checks):
        """checks: list of (substring, expected). expected True/False, or an exact count."""
        bad = []
        for sub, expected in checks:
            n = sum(1 for ln in self.lines if sub in ln)
            if expected is True:
                ok = n >= 1
            elif expected is False:
                ok = n == 0
            else:
                ok = n == expected
            print("  %s %r count=%d expected=%s" % ("OK  " if ok else "FAIL", sub, n, expected))
            if not ok:
                bad.append(sub)
        if bad:
            raise AnchorError("%d probe(s) failed: %s" % (len(bad), bad))
        return self

    def balanced(self, open_prefix, close_pred):
        """Fence/block balance, e.g. mermaid alt/end. close_pred takes a line."""
        n_open = sum(1 for ln in self.lines if ln.startswith(open_prefix))
        n_close = sum(1 for ln in self.lines if close_pred(ln))
        ok = n_open == n_close
        print("  %s balance %r: open=%d close=%d" % ("OK  " if ok else "FAIL", open_prefix,
                                                     n_open, n_close))
        if not ok:
            raise AnchorError("unbalanced %r: %d open, %d close" % (open_prefix, n_open, n_close))
        return self

    # ---- writing ----

    def save(self, path, backup_dir=None):
        """Write with the source's own line endings. Backs up an existing target first."""
        if backup_dir and os.path.exists(path):
            bak = os.path.join(backup_dir, os.path.basename(path) + ".BAK")
            shutil.copyfile(path, bak)
            print("backed up existing target to", bak)
        body = self.eol.join(self.lines)
        with open(path, "wb") as f:
            f.write(body.encode("utf-8"))
        print("wrote %s: %d lines, %d chars" % (path, len(self.lines), len(body)))
        return self


if __name__ == "__main__":
    # self-test: run `python anchored_edit.py`
    p = Page("a\nTARGET one\nb\nTARGET two\nc\n")
    try:
        p.find("TARGET")
        print("FAIL: non-unique anchor was accepted")
        sys.exit(1)
    except AnchorError:
        print("OK   non-unique anchor rejected")
    i = p.find("TARGET one")
    j = p.find("TARGET two")
    p.replace(i, ["ONE"]).replace(j, ["TWO", "TWO-EXTRA"]).apply()
    assert p.lines == ["a", "ONE", "b", "TWO", "TWO-EXTRA", "c", ""], p.lines
    print("OK   descending apply kept both anchors")
    p.probe([("ONE", 1), ("TARGET", False)])
    print("self-test passed")
