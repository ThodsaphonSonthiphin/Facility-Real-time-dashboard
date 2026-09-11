# Destinations

A **destination** is where a published page lands. Two families, keyed on how a page is written:

| family | destinations | how a page is written | what refuses a stale write |
|---|---|---|---|
| **API page store** | Azure DevOps wiki (measured); Confluence, Notion, SharePoint (not measured) | one HTTP call per page | a per-page version token - `ETag`, replayed as `If-Match` |
| **Git file store** | GitHub wiki, a repo `docs/` folder, a plain local folder | write the file, commit, push | the commit - a stale push is refused as non-fast-forward |

A plain local folder is the git family with the push left out.

**An adapter is measured, or it does not exist.** A recipe written from vendor documentation is a
guess, and it fails at publish time - after the draft is finished and in front of the page owner.
The slug trap in the Azure DevOps entry below was invisible in the documentation and cost the run
its only user-visible failure.

---

## The eight questions

Answer all eight against the live destination **before the first write**. Ten minutes once per
destination; then write the answers into this file as a new entry so the next run pays nothing.

1. **Addressing** - how is a page identified? An id, a path, a file name? Which one do the write
   calls want? What does the destination do to a title to make the address (spaces, punctuation,
   case)?
2. **Read** - how do you fetch a page's current content? Is there a separate call for an
   unpublished draft? (An Azure DevOps *web resource* has one; a plain read returns the published
   copy and a saved-but-unpublished edit reads as "no change".)
3. **Version token** - what does a read hand back that a write can replay to refuse a stale
   overwrite? If the answer is "nothing", say so in the entry: the destination cannot detect a
   concurrent edit and the run must re-read immediately before writing.
4. **Create and update** - the call for each, and how they differ. What status code means created
   rather than updated?
5. **Children and siblings** - how do you list what sits under a parent? Which fields do the
   returned objects carry, and which are missing? (Azure DevOps child objects carry **no id**.)
6. **Attachments** - how does an image get in, what encoding does the body want, and what exactly
   does the page write to reference it?
7. **Links** - how is an internal link spelled, how does the destination convert that spelling back
   into an address, and **what does a broken link look like**? If a broken link is
   indistinguishable from a page that never existed, every link must be resolved after publishing.
8. **Rename and move** - the call, and how to verify it. Never verify from the response body: some
   APIs echo the old address.

Each entry also records **the Mermaid fence** and any markup the destination refuses. The same
page text is correct on one destination and unreadable on another.

---

## Azure DevOps wiki - MEASURED 2026-08-20

Against `GlassHull.wiki` in `Cartagena365/GlassHull`, branch `wikiMaster`. Every row below was run.

**Token** - the resource GUID is fixed for Azure DevOps, on every tenant:

```bash
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --query accessToken -o tsv
```

**Base** - `https://dev.azure.com/{org}/{project}/_apis/wiki/wikis/{wiki}`

| operation | call | the trap |
|---|---|---|
| list wikis | `GET /_apis/wiki/wikis` | `az devops wiki list` also works |
| read by id | `GET /pages/{id}?includeContent=true` | the id is what the wiki URL shows; every other call wants the **path** |
| read by path | `GET /pages?path={path}&includeContent=true` | the path uses **spaces**, url-encoded |
| list children | `GET /pages?path={parent}&recursionLevel=oneLevel` | child objects carry **no `id`** - only `path` and `gitItemPath`. Reading `["id"]` raises `KeyError` |
| create or update | `PUT /pages?path={path}`, body `{"content": "..."}` | create answers **201**; an update without `If-Match: {etag}` is **refused** |
| rename or move | `POST /pagemoves?api-version=7.0-preview`, body `{path, newPath}` | answers **201 echoing the OLD path with a null id** - it reads like a no-op. Verify with a fresh `GET /pages?path=` |
| upload an attachment | `PUT /attachments?name={name}` | the body must be **base64**, not raw bytes. A raw PUT answers **HTTP 500** complaining the input is not a valid Base-64 string, which reads like a corrupt file |
| **verify** an attachment exists | `GET /_apis/git/repositories/{wiki}/items?scopePath=/.attachments&recursionLevel=oneLevel&versionDescriptor.version=wikiMaster&versionDescriptor.versionType=branch` | a wiki **is** a git repo, so its attachments are blobs and are listable - there is no wiki-API call for this. Passing `path` instead of **`scopePath`** answers **HTTP 400**, and the error text is the only documentation of the fix. Measured 2026-08-21 |

**A broken image is silent, and it was unchecked until 2026-08-21.** The page renders, the alt text does not appear, and nothing 404s - the same failure shape as a dead link. The git call above is now in `scripts/check_links.py`, so an ADO run reports `0 unchecked` rather than listing attachments it declined to verify. The first run to use it found all 11 attachments present, including six that had been referenced for a day with nobody able to say whether they existed.

**Addressing, and the trap that cost the run.** A space in a page title is stored as `-` in the
filename, so a **literal hyphen must be escaped as `%2D`**. The page *"Customer quote self-service
flow and diagrams"* was stored as `Customer-quote-self%2Dservice-flow-and-diagrams.md`, and a link
spelt the obvious way resolved to a path with a **space** where the hyphen was - answering *Page
does not exist*, which is what a page that was never created also answers. The owner found it by
clicking the link inside the page just published.

- **Rule: never put a literal hyphen in a page path.** Rename the page instead of escaping the
  links, because nobody hand-types `%2D`. The H1 inside the page may keep its hyphen; only the path
  must lose it.
- Slug back to path: `%2D` to `-`, then `-` to a space. `scripts/check_links.py` implements exactly
  this.

**Attachments** are referenced from the page as `/.attachments/{name}`. Upload first, then the
image links resolve with no edit.

**Mermaid fence**: `::: mermaid` ... `:::` - **not** a triple-backtick fence. A block that does
not parse renders as an **error box on the live page**, and no publish probe or link check sees it -
run `scripts/check_mermaid.py` before the write. Two characters are measured killers inside a
block: `;` terminates a statement, and `#` opens an entity code. The ADO container also
reports the failing line **one lower** than the raw markdown, so count from the block, not the
file. Avoid `<br/>` inside
node labels (the existing pages use none, and a mechanical strip of them produced unreadable
labels). A composite `state "X" as Y { }` was avoided in favour of two plain `stateDiagram-v2`
blocks.

---

## Git file store

Mechanically one family. What differs is the repo and whether a review gate exists.

| destination | address | write | review before it is visible |
|---|---|---|---|
| **repo `docs/`** *(default when the destination is a repo the team develops in)* | the file path | commit on a branch, push, open a PR | yes - the PR |
| **GitHub wiki** | the wiki repo's file name | clone the wiki remote, write, commit, push | **none. The push is publication** |
| **plain local folder** | the file path | write the file | not applicable |

Because a wiki push is live the instant it lands, the GitHub wiki inherits the API-family protocol
unchanged: re-fetch, write, read back, resolve every link.

**Not yet measured, and must be answered on the first run against it:** how GitHub converts a wiki
page title into its file name, and whether a literal hyphen has the same collision as Azure DevOps.
Answer question 1 and question 7 live, then write the answer here.

**Mermaid fence**: a triple-backtick `mermaid` fence renders on GitHub, in a repo file and in the
wiki. A plain local folder renders nothing on its own - if the page will be read in a viewer that
does not draw Mermaid, say so before drafting rather than after.

**Version token**: the commit. There is no per-page token, so re-read the file immediately before
writing, and let the push refusal do the rest. Never `push --force` a destination.

---

## Adding a new destination

1. answer the eight questions live, against a scratch page you are allowed to break;
2. publish through it once, reading the page back;
3. resolve every internal link with `scripts/check_links.py`, adding a resolver for the new family
   if it needs one;
4. write the entry here, in the same run, including every trap you hit and the date measured.

An entry that says *not measured* is more useful than an entry that guesses, because it tells the
next run where the ten minutes must be spent.

---

## Plain local folder - MEASURED 2026-08-20

Against a `wiki/` folder holding `Portal-Roles-and-Permissions.md`, published to it once. The eight
questions, answered live:

1. **Addressing** - the file path. The title is not converted by anything: **the writer chooses the
   file name**, and here the owner supplied it. Spaces became `-` by convention only, so the Azure
   DevOps `%2D` collision cannot happen unless somebody writes a literal hyphen on purpose. If the
   folder is ever pushed to an Azure DevOps wiki, that rule starts applying retroactively - so keep
   literal hyphens out of file names anyway.
2. **Read** - `cat` the file. There is no draft copy and no published copy: what is on disk is what
   a reader sees.
3. **Version token** - **none.** Checked: `git status` in the folder's tree answered *not a git
   repository*, so there is no commit either, and nothing can refuse a stale write. Compensate by
   hashing the file into the record and re-reading it immediately before writing. A `sha256` plus
   the byte count is the closest thing to an `ETag` this family has.
4. **Create and update** - the same write. "Created" is only knowable by testing for the file
   first; do that, because it is the one signal that says whether this run is publishing or
   overwriting somebody else's page.
5. **Children and siblings** - `ls` the folder. There is no parent-child structure at all: the
   hierarchy exists only as links inside the pages, which is exactly why the parent edit is not
   optional here. A page nothing links to is unreachable and invisible.
6. **Attachments** - copy the image into the folder (or a subfolder) and reference it relatively.
   Nothing validates that the target exists; `check_links.py files` does not verify images.
7. **Links** - a relative file name, `[text](Some-Page.md)`. `check_links.py files` resolves it on
   disk, trying the name, `+ .md`, and spaces replaced by `-`. **A broken link is silent**: nothing
   renders it, nothing 404s, and it simply does not open. Indistinguishable from a page that never
   existed, so resolve every link after every write.
8. **Rename and move** - `git mv` if the folder is in a repo, otherwise `mv`. Verify by listing the
   folder, and then re-run the link check: renaming a page here breaks every link to it with no
   warning of any kind.

**Generated pages are the real trap in this family, not the transport.** The page published against
here carried `<!-- generated by build_parent.py - do not edit this file by hand -->` on line 1. Its
generator reproduced the published bytes exactly (verified by re-running it and diffing against the
snapshot before any edit), which is what proves the comment is true and the output must not be
touched. Look at line 1 of every page in a plain folder before editing it.

**Mermaid fence**: still unmeasured, and it cannot be measured from the folder - it depends on the
viewer, which is not part of the destination. Ask before drafting a diagram, and if the owner is
unavailable, leave the diagram out and say so on the record rather than shipping a fence that may
render as raw text on a management page.
