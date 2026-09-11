# picture-record.jsonl — the cross-skill picture-reading contract

One file per project, holding what has already been read out of pictures. It has
**two readers by design**: a **human** (greps a sentence and lands on the line that
produced it) and **another skill** (looks up whether a question has already been
answered for an image). `plugins/dev-workflows/scripts/picture-record.py` owns all
read and write; nothing else parses or emits these lines.

This file is the single source of truth for both the row schema and the question set.
`scripts/test_picture_record.py` asserts the script's constants still match the two
tables below, so an edit here that the script does not follow fails the suite.

## File location

Resolved at **runtime**, never hardcoded:

1. `--path <file>` flag (highest precedence), then
2. `PICTURE_RECORD_FILE` env var, then
3. `picture-record.jsonl` at the **git repository root** (`git rev-parse --show-toplevel`).

If the cwd is not inside a git repo, the skill asks the user where to put it rather
than failing. It lives in the repo you run in — **never** in the plugin marketplace
repo, which installs onto other people's machines (ADR 0142).

## Shape

**Append-only JSONL**: one JSON object per line, one line per answer, never rewritten
(ADR 0143). Re-reading a picture appends a new line rather than editing the old one, so
the file keeps the history of when each answer was confirmed.

## Row fields

| field | type | meaning |
|---|---|---|
| `schema_version` | int | On every line, so a format change never needs the file rewritten. `1` today. |
| `image_sha256` | string | SHA-256 of the bytes that were actually read. Half of the key (ADR 0136). |
| `source` | string | The URL or path the bytes came from. How a row is found when the file is gone. |
| `source_kind` | enum | `ado-attachment` \| `local-path` \| `url`. Says whether the source is expected to be re-fetchable. |
| `question_kind` | enum | A name from the set below. The other half of the key (ADR 0138). |
| `question_detail` | string | The caller's own wording under that name — what specifically it asked. |
| `answer` | string | Only what the question asked for (ADR 0137). |
| `read_on` | string | ISO-8601 with offset, when the bytes were read. |
| `asked_by` | string | The calling skill's name. Makes cross-skill reuse observable rather than assumed. |

Field order is the order above, and `append_row` emits it unsorted, so a human reading a line sees what was
asked before what was answered.

## Question kinds

| kind | the question it stands for |
|---|---|
| `on-screen-text` | The exact words the product shows — control labels, page titles, status names. |
| `requirement` | What an annotated picture asks to be done. |
| `other` | Anything else. Using it **obliges the run to add the new kind to this table** in the same change, the way `document-what-shipped` writes back a spine its five did not cover. |

Two kinds, not five: a kind invented for a caller nobody has wired is a guess, and this
repo does not ship guessed recipes (ADR 0140).

## What a lookup returns

| outcome | means |
|---|---|
| `hit` | A row exists for this image, kind, and an equal (whitespace- and case-normalised) detail. |
| `candidates` | Rows exist for this image and kind but none matches the detail. **The skill must judge them and default to re-reading** — a near-miss is a miss (ADR 0136). |
| `no-answer` | Nothing recorded for this image and kind. |

Every result also carries **`bytes_verified`**: `true` only when *this* lookup hashed
bytes itself (a `file_path` was given and read), `false` otherwise - including when the
caller already knew the hash and passed `image_sha256` directly, because no bytes were
checked in this call. A hash a caller happens to hold is not proof; the flag answers
"did this call verify the bytes", not "is a hash known". A `false` result is the **not
re-checked against current bytes** flag of ADR 0139, and it lives on the returned result
— never on a stored line, because a line written last month cannot know about a lookup
happening today.

## Safety

- **Only what the question asked goes into a line** (ADR 0137). A data value is recorded
  only when the caller names the field it wants. This is what makes the file committable.
- **No credential, ever.** A signed link is a credential; it stays out of the record and
  out of the commit, for any reader.
- The script reads image bytes and appends lines. It writes nothing else and touches git
  never.

## See also

- ADRs `0135`–`0143` — the decisions behind every rule on this page.
- `plugins/dev-workflows/skills/read-picture/SKILL.md` — the judgment half.
- `plugins/dev-workflows/references/daily-state-contract.md` — the sibling contract this
  one is modelled on.
