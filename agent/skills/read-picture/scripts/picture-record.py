"""picture-record.py — read/write the per-project picture-record.jsonl file.

picture-record.jsonl records what has already been read out of a picture, so a
second run (or a second skill) does not re-open the same image to reach the same
sentence. It is append-only: one JSON object per line, never rewritten. The
canonical schema and question set live in
plugins/dev-workflows/references/picture-record-contract.md.

This script owns ALL read/write so the row format is never corrupted by freehand
edits. Judgment stays in the read-picture SKILL: what the picture shows, which
kind applies, and whether a stored row actually covers the request. This script
never touches git and never writes anything but appended lines.

Subcommands:
  resolve-path [--path P]
      Print the resolved record path (override order: --path > PICTURE_RECORD_FILE
      env > git-root). Exits non-zero with a message when not in a git repo and no
      override is given.
  kinds
      Print the named question set, one per line.
  hash <file>
      Print the SHA-256 of a file's bytes.
  lookup --kind K --detail D (--file F | --source S) [--path P] [--json]
      Report hit / candidates / no-answer plus bytes_verified. Refuses with a
      clear message, before any read, when neither --file nor --source is given.
  append --kind K --detail D --answer A --asked-by S
         (--file F [--source S] | --sha H --source S) [--source-kind SK] [--path P]
      Validate and append exactly one line. Refuses with a clear message, before
      any write, unless the identity given actually names a picture: --file alone,
      --file with a --source that supersedes it as the durable identity (a
      downloaded attachment's real URL, say), or --sha and --source together.

Importable as a module: resolve_path, hash_file, normalize_detail, make_row,
validate_row, append_row, iter_rows and lookup are pure functions whose only side
effects are the reads and writes named. The CLI lives under
`if __name__ == "__main__":` so importing never auto-runs.

Dependencies: none beyond the standard library.
"""

import argparse
import hashlib
import io
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone

try:
    sys.stdout.reconfigure(encoding="utf-8")  # Python 3.7+; avoids cp1252 crashes on Windows
except Exception:
    pass

FILE_NAME = "picture-record.jsonl"
ENV_VAR = "PICTURE_RECORD_FILE"
SCHEMA_VERSION = 1

# The named set (ADR 0138). `other` obliges the run to add its new kind to the
# contract document in the same change. test_picture_record.py asserts this tuple
# still matches that document's table.
QUESTION_KINDS = ("on-screen-text", "requirement", "other")

SOURCE_KINDS = ("ado-attachment", "local-path", "url")

# Emit order for a row, key before answer, so a human reading a line sees what was
# asked before what was answered.
ROW_FIELDS = (
    "schema_version",
    "image_sha256",
    "source",
    "source_kind",
    "question_kind",
    "question_detail",
    "answer",
    "read_on",
    "asked_by",
)

_UNSET = object()

_URL_SCHEME = re.compile(r"^[a-zA-Z][a-zA-Z0-9+.-]*://")

# Partial guard, not the whitelist itself. "No credential, ever" (the contract's
# safety section) is enforced by the read-picture SKILL reading the picture and
# choosing what to write down; this only catches the shapes measured in practice -
# a signed query string, or a bearer token pasted into an answer - so an obvious
# slip does not sail past validate_row. It is not a substitute for the SKILL's
# own judgment.
_CREDENTIAL_MARKERS = ("sig=", "sv=", "token=", "Bearer ")


def _looks_like_a_credential(answer):
    if not answer:
        return False
    return any(marker in answer for marker in _CREDENTIAL_MARKERS)


def _non_empty_string(value):
    return isinstance(value, str) and value.strip() != ""


def _git_root(cwd=None):
    """Repo root, or None when the cwd is not inside a git repo."""
    try:
        out = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            cwd=cwd, capture_output=True, text=True, check=False,
        )
    except OSError:
        return None
    root = out.stdout.strip()
    return root or None


def resolve_path(path=None, env_value=None, cwd=None, git_root=_UNSET):
    """--path > PICTURE_RECORD_FILE > git root. None when there is no repo and no
    override, so the caller can ask the user instead of failing (ADR 0142)."""
    if path:
        return path
    if env_value:
        return env_value
    root = _git_root(cwd) if git_root is _UNSET else git_root
    if not root:
        return None
    return os.path.join(root, FILE_NAME)


def hash_file(file_path):
    """SHA-256 of a file's bytes, streamed so a large screenshot is not loaded whole."""
    digest = hashlib.sha256()
    with open(file_path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


_WHITESPACE = re.compile(r"\s+")


def normalize_detail(detail):
    """Fold case and collapse whitespace, so two callers wording the same request
    slightly differently still match. Anything beyond this is judgment and belongs
    in the SKILL, not here."""
    return _WHITESPACE.sub(" ", (detail or "").strip().lower())


def validate_row(row):
    """Raise ValueError unless the row is exactly the contract's nine fields with
    known enum values, both key fields actually hold a value, and the answer does
    not carry an obvious credential shape. Returns the row so callers can wrap it."""
    missing = [name for name in ROW_FIELDS if name not in row]
    if missing:
        raise ValueError("row is missing required field(s): %s" % ", ".join(missing))
    unknown = [name for name in row if name not in ROW_FIELDS]
    if unknown:
        raise ValueError("row carries unknown field(s): %s" % ", ".join(sorted(unknown)))
    if row["schema_version"] != SCHEMA_VERSION:
        raise ValueError(
            "schema_version must be %d, got %r" % (SCHEMA_VERSION, row["schema_version"])
        )
    # Both halves of the key must actually hold a value - a row with a null hash
    # AND a null source writes successfully and can never be found again, because
    # the file is append-only and there is no way to edit it back out.
    if not _non_empty_string(row.get("image_sha256")):
        raise ValueError(
            "image_sha256 must be a non-empty string - a keyless row can never be "
            "found again"
        )
    if not _non_empty_string(row.get("source")):
        raise ValueError(
            "source must be a non-empty string - a keyless row can never be found again"
        )
    if row["question_kind"] not in QUESTION_KINDS:
        raise ValueError(
            "question_kind %r is not in the named set (%s)"
            % (row["question_kind"], ", ".join(QUESTION_KINDS))
        )
    if row["source_kind"] not in SOURCE_KINDS:
        raise ValueError(
            "source_kind %r is not one of (%s)"
            % (row["source_kind"], ", ".join(SOURCE_KINDS))
        )
    if _looks_like_a_credential(row.get("answer")):
        raise ValueError(
            "answer looks like it carries a credential (a signed query string, or a "
            "Bearer token) - no credential is ever recorded, for any reader"
        )
    return row


def make_row(image_sha256, source, source_kind, question_kind, question_detail,
             answer, asked_by, read_on=None):
    """Build one validated row in contract field order."""
    row = {
        "schema_version": SCHEMA_VERSION,
        "image_sha256": image_sha256,
        "source": source,
        "source_kind": source_kind,
        "question_kind": question_kind,
        "question_detail": question_detail,
        "answer": answer,
        "read_on": read_on or datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds"),
        "asked_by": asked_by,
    }
    return validate_row(row)


def append_row(record_path, row):
    """Append exactly one line. Never rewrites, so no earlier line can be corrupted
    by a bad round-trip (ADR 0143). Returns the line written."""
    validate_row(row)
    line = json.dumps(row, ensure_ascii=False, sort_keys=False) + "\n"
    with io.open(record_path, "a", encoding="utf-8", newline="\n") as handle:
        handle.write(line)
    return line


def _warn_bad_line(number):
    print("picture record line %d is not valid JSON - skipped" % number, file=sys.stderr)


def iter_rows(record_path, on_bad_line=None):
    """Yield each row in file order — which is chronological, because the file is
    append-only. A blank line is skipped. A line that is not valid JSON is skipped
    too, never silently: on_bad_line(line_number) is called for it (default: a
    stderr warning naming the line). One torn line - a hand edit, or the tail of an
    interrupted write - must not brick every lookup against every other line in the
    file, and the contract designates a human who greps and edits by hand as a
    first-class reader of this file."""
    if not record_path or not os.path.exists(record_path):
        return
    if on_bad_line is None:
        on_bad_line = _warn_bad_line
    with io.open(record_path, encoding="utf-8", newline="") as handle:
        for number, raw in enumerate(handle, 1):
            text = raw.strip()
            if not text:
                continue
            try:
                yield json.loads(text)
            except ValueError:
                on_bad_line(number)
                continue


def lookup(record_path, question_kind, question_detail, image_sha256=None, source=None,
           file_path=None):
    """Find the row that answers this question about this image.

    Keyed on hash + kind + detail (ADRs 0136, 0138). Matching on the detail is
    deliberately literal — normalised string equality, nothing cleverer — so the
    script stays deterministic and the judgment of whether a near-miss row is good
    enough stays in the SKILL, which must default to re-reading.

    Pass file_path when the bytes are present and you want THIS call to prove it:
    lookup hashes them itself, and the returned bytes_verified is true because of
    work this call actually did. Pass image_sha256 directly when you already know
    the hash (for example, one read out of a stored row) — that gets
    bytes_verified=False, because no bytes were checked in this call; it says
    nothing about whether the hash is correct, only that this lookup did not verify
    it. Pass source when the bytes are gone. bytes_verified is the ADR 0139 flag:
    it lives on the result, never on a stored line.
    """
    if file_path:
        image_sha256 = hash_file(file_path)
        bytes_verified = True
    else:
        bytes_verified = False
    if not image_sha256 and not source:
        raise ValueError("lookup needs image_sha256 or source (or file_path)")
    wanted = normalize_detail(question_detail)
    exact = None
    candidates = []
    for row in iter_rows(record_path):
        if row.get("question_kind") != question_kind:
            continue
        if image_sha256:
            if row.get("image_sha256") != image_sha256:
                continue
        elif row.get("source") != source:
            continue
        if normalize_detail(row.get("question_detail")) == wanted:
            exact = row  # last match wins: the file is append-only, so file order is time order
        else:
            candidates.append(row)
    result = {
        "outcome": "no-answer",
        "row": None,
        "candidates": candidates,
        "bytes_verified": bytes_verified,
    }
    if exact is not None:
        result["outcome"] = "hit"
        result["row"] = exact
    elif candidates:
        result["outcome"] = "candidates"
    return result


def _resolved_or_die(args):
    path = resolve_path(path=args.path, env_value=os.environ.get(ENV_VAR))
    if not path:
        raise SystemExit(
            "not inside a git repo and no --path or %s given - ask where the record "
            "should live rather than guessing" % ENV_VAR
        )
    return path


def _require_identity(args, command):
    """Raise SystemExit unless the arguments actually name a picture - checked
    before any read or write, so a call that cannot be found again is refused
    instead of silently succeeding.

    append's valid forms: --file alone, --file with --source (the durable identity
    supersedes the temp path --file supplies bytes from), or --sha and --source
    together. lookup's valid forms: --file alone, or --source alone (the bytes may
    be gone)."""
    if args.file:
        return
    if command == "append":
        if not (args.sha and args.source):
            raise SystemExit(
                "append needs --file, or --sha and --source together - refusing to "
                "write a row with no identity (both halves of the key must hold a "
                "value, or the row can never be found again)"
            )
        return
    if not args.source:
        raise SystemExit(
            "lookup needs --file or --source - refusing to guess what picture is meant"
        )


def _default_source_kind(source):
    """local-path unless the source looks like a URL. Used only when --source-kind
    is not given explicitly."""
    return "url" if _URL_SCHEME.match(source or "") else "local-path"


def _source_of(args):
    """Return (image_sha256, source, source_kind) for append. Caller must have
    already validated the identity with _require_identity.

    --file supplies the bytes to hash. --source, when also given, supplies the
    durable identity - a downloaded attachment's real URL rather than the temp path
    it was saved to - and wins over --file as the stored `source`. Without --file,
    --sha and --source together are the explicit trio."""
    if args.file:
        try:
            sha = hash_file(args.file)
        except OSError as err:
            raise SystemExit(
                "cannot read --file %r (%s) - the bytes are gone; supply --sha and "
                "--source instead if you still know where they came from" % (args.file, err)
            )
        source = args.source or args.file
        source_kind = args.source_kind or _default_source_kind(source)
        return sha, source, source_kind
    source_kind = args.source_kind or _default_source_kind(args.source)
    return args.sha, args.source, source_kind


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read/write the per-project picture record.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    p_resolve = subparsers.add_parser("resolve-path")
    p_resolve.add_argument("--path")

    subparsers.add_parser("kinds")

    p_hash = subparsers.add_parser("hash")
    p_hash.add_argument("file")

    for name in ("lookup", "append"):
        sub = subparsers.add_parser(name)
        sub.add_argument("--path")
        sub.add_argument("--kind", required=True, choices=list(QUESTION_KINDS))
        sub.add_argument("--detail", required=True)
        sub.add_argument("--file")
        sub.add_argument("--sha")
        sub.add_argument("--source")
        sub.add_argument("--source-kind", choices=list(SOURCE_KINDS))
        if name == "lookup":
            sub.add_argument("--json", action="store_true")
        else:
            sub.add_argument("--answer", required=True)
            sub.add_argument("--asked-by", required=True)

    args = parser.parse_args(argv)

    if args.command == "kinds":
        for kind in QUESTION_KINDS:
            print(kind)
        return 0

    if args.command == "hash":
        print(hash_file(args.file))
        return 0

    if args.command == "resolve-path":
        print(_resolved_or_die(args))
        return 0

    record_path = _resolved_or_die(args)

    if args.command == "lookup":
        _require_identity(args, "lookup")
        try:
            result = lookup(
                record_path, args.kind, args.detail,
                file_path=args.file or None,
                source=None if args.file else args.source,
            )
        except OSError as err:
            raise SystemExit(
                "cannot read --file %r (%s) - the bytes are gone; look up by "
                "--source instead if you still know where they came from"
                % (args.file, err)
            )
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            print("%s | bytes_verified=%s" % (result["outcome"], result["bytes_verified"]))
            if result["row"]:
                print(result["row"]["answer"])
            for candidate in result["candidates"]:
                print("candidate: %s -> %s"
                      % (candidate["question_detail"], candidate["answer"]))
        return 0

    _require_identity(args, "append")
    sha, source, source_kind = _source_of(args)

    try:
        row = make_row(
            image_sha256=sha, source=source, source_kind=source_kind,
            question_kind=args.kind, question_detail=args.detail,
            answer=args.answer, asked_by=args.asked_by,
        )
    except ValueError as err:
        raise SystemExit(str(err))
    append_row(record_path, row)
    print("appended to %s" % record_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
