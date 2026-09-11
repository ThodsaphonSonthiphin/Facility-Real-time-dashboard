#!/usr/bin/env python3
"""check_plugin_copies.py - find every copy of a plugin or skill, and grade it.

Reports where a plugin or bare skill exists on THIS machine and whether each
copy matches the source. It CHANGES NOTHING: a person reads the report and
makes the repair it names (ADR 0104).

Only `skills/<name>/SKILL.md` is hashed (ADR 0109). A copy whose
`references/` or `scripts/` drifted therefore grades IN SYNC, and the report
says so on its summary line rather than letting a clean answer stand for more
than it measured.

Nothing about any particular machine is hard-coded. Every location is derived
at run time from the marketplace registry, so the same code runs unchanged on
a machine it has never seen (ADR 0108).

Usage:
  python check_plugin_copies.py --plugin NAME [--marketplace NAME] [--strict]
  python check_plugin_copies.py --plugin NAME --root PATH [--root PATH ...]

Exit codes: 0 clean, 1 findings under --strict, 2 cannot run.
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys


def normalize(data):
    """CR-normalize a byte string (ADR 0086). CRLF -> LF, nothing else."""
    return data.replace(b"\r\n", b"\n")


def content_hash(data):
    return hashlib.sha256(data).hexdigest()


def read_normalized(path):
    with open(path, "rb") as f:
        return normalize(f.read())


def _die(message):
    sys.stderr.write("cannot run: %s\n" % message)
    sys.exit(2)


def _load_json_or_die(path, what):
    """Load a JSON file or die with exit code 2. Catches both JSON and OS errors."""
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (ValueError, OSError) as exc:
        _die("%s is not valid JSON or is unreadable (%s)" % (what, exc))


def load_registry(claude_home):
    """The marketplace registry. This is the only discovery root that is
    assumed to exist at a fixed place."""
    path = os.path.join(claude_home, "plugins", "known_marketplaces.json")
    if not os.path.isfile(path):
        _die("no marketplace registry at %s" % path)
    return _load_json_or_die(path, "the marketplace registry")


def marketplace_root_or_none(registry, marketplace):
    """Where the marketplace's own tree lives, or None when the entry records
    no usable location.

    A `directory` source means the repo working tree IS the load path -
    editing that tree is the deploy, and the cache is only a snapshot.

    The None-returning form exists so that scanning every marketplace (the
    --marketplace auto-detection) can skip an entry it cannot resolve instead
    of killing the run over a marketplace the user never asked about.
    """
    entry = registry.get(marketplace) or {}
    source = entry.get("source") or {}
    if source.get("source") == "directory" and source.get("path"):
        return source["path"]
    return entry.get("installLocation") or None


def marketplace_root(registry, marketplace):
    """marketplace_root_or_none, but a resolution failure exits 2 - the form
    used once the run has committed to one named marketplace."""
    if marketplace not in registry:
        _die("no marketplace named %r in the registry (known: %s)"
             % (marketplace, ", ".join(sorted(registry)) or "none"))
    root = marketplace_root_or_none(registry, marketplace)
    if not root:
        _die("marketplace %r records neither a directory source nor an "
             "installLocation" % marketplace)
    return root


def plugin_root(mkt_root, plugin):
    """The plugin's directory, read from the marketplace manifest rather than
    assumed to be plugins/<name>."""
    manifest = os.path.join(mkt_root, ".claude-plugin", "marketplace.json")
    if not os.path.isfile(manifest):
        _die("no marketplace manifest at %s" % manifest)
    data = _load_json_or_die(manifest, "the marketplace manifest")
    plugins = data.get("plugins") or []
    if not isinstance(plugins, list):
        _die("marketplace manifest %s: plugins must be a list, not %s"
             % (manifest, type(plugins).__name__))
    for entry in plugins:
        if entry.get("name") == plugin:
            source = entry.get("source")
            if not source:
                _die("marketplace manifest %s lists plugin %r with no source key"
                     % (manifest, plugin))
            return os.path.normpath(os.path.join(mkt_root, source))
    _die("marketplace manifest %s lists no plugin named %r" % (manifest, plugin))


def source_skills(root):
    """{skill name: path to its SKILL.md} for one plugin tree."""
    skills_dir = os.path.join(root, "skills")
    found = {}
    if not os.path.isdir(skills_dir):
        return found
    for name in sorted(os.listdir(skills_dir)):
        candidate = os.path.join(skills_dir, name, "SKILL.md")
        if os.path.isfile(candidate):
            found[name] = candidate
    return found


def _git_dir_above(path):
    """Check if a .git entry exists anywhere from path up to filesystem root.

    Returns True if found; False otherwise. Checks for existence, not
    directory-ness, because in a git worktree .git is a plain file, not a dir.
    """
    path = os.path.abspath(path)
    while True:
        git_candidate = os.path.join(path, ".git")
        if os.path.exists(git_candidate):
            return True
        parent = os.path.dirname(path)
        if parent == path:  # reached filesystem root
            return False
        path = parent


def git_output(repo, *args):
    """stdout of a git command, or None if git is absent or the command
    failed. A None return always means 'no information', never 'no'."""
    try:
        result = subprocess.run(["git", "-C", repo] + list(args),
                                capture_output=True, text=True)
    except OSError:
        return None
    if result.returncode != 0:
        return None
    return result.stdout.strip()


PRUNE = frozenset(["node_modules", ".git", "obj", "bin", "__pycache__",
                   ".venv"])


def _key(path):
    """Case-insensitive absolute key for deduplication. Windows paths differ
    in drive-letter case and in the 8.3 short form (e.g., THODSA~1.SON)
    versus the long form (e.g., thodsaphon.sonthipin). This function uses
    realpath instead of abspath to collapse both forms to one canonical name.

    This choice is deliberate and load-bearing: without realpath, a --root
    given in short form would overlap a derived root in long form without
    dedup, and role classification would misclassify a cache path arriving in
    a different form as a vendored copy - which incorrectly carries a write
    repair forbidden for the cache. Do not simplify this back to abspath.
    """
    return os.path.normcase(os.path.realpath(path))


def derive_roots(registry, claude_home, agents_home):
    """Where to look, computed rather than configured (ADR 0108).

    A repo that vendors a plugin is overwhelmingly a sibling of the repo that
    publishes it, so the parent of each directory-sourced marketplace is the
    rule that finds vendored copies with no machine-specific input.
    """
    candidates = []
    for entry in registry.values():
        source = (entry or {}).get("source") or {}
        if source.get("source") == "directory" and source.get("path"):
            parent = os.path.dirname(os.path.normpath(source["path"]))
            if parent:
                candidates.append(parent)
    candidates.append(claude_home)
    candidates.append(agents_home)

    seen, roots = set(), []
    for candidate in candidates:
        if not os.path.isdir(candidate):
            continue
        key = _key(candidate)
        if key in seen:
            continue
        seen.add(key)
        roots.append(os.path.abspath(candidate))
    return roots


def _is_generated_skills_tree_hit(dirpath):
    """True when dirpath is `<marketplace-root>/skills/<name>` - a
    marketplace's OWN generated distribution tree (built by
    scripts/generate_skills_tree.py), not a copy anyone should repair.

    Each SKILL.md under that tree is a REWRITTEN copy of its plugin source
    by design (plugin-root references resolved to skill-relative paths), so
    it deliberately differs from the source it came from and grades as
    drifted every time - its only repair is regeneration, never a hand
    edit or a merge (ADR workflow-daily-work-0165).

    Discriminated structurally, never by name or path string: the directory
    holding `skills/` must itself hold `.claude-plugin/marketplace.json` -
    the same manifest plugin_root() reads to resolve a plugin's source. A
    plugin's own skills/ instead sits under a directory holding
    `.claude-plugin/plugin.json`, so this test does not fire for a plugin's
    source tree or for an ordinary vendored copy that happens to sit under a
    directory literally named `skills`.
    """
    skills_dir = os.path.dirname(dirpath)
    if os.path.basename(skills_dir) != "skills":
        return False
    candidate_root = os.path.dirname(skills_dir)
    return os.path.isfile(os.path.join(candidate_root, ".claude-plugin",
                                       "marketplace.json"))


def scan_for_skill_dirs(roots, names, errors=None, skipped=None):
    """Every directory named after one of `names` that holds a SKILL.md.

    Finds copies nobody registered - the reason a scan was chosen over a
    declared manifest (ADR 0105). Whether a hit is OURS is a separate
    question, answered from content by classify().

    Names are compared case-insensitively (os.path.normcase), the same
    canonicalization the dedup below uses: on a case-insensitive filesystem a
    directory named `Copy-Audit/` is a real copy, and a case-sensitive test
    made it invisible.

    `errors` collects the directories os.walk could not read. For a tool whose
    contract is "find every copy", a skipped directory must never be silent -
    a copy inside it would simply be absent from the report, which reads as
    "there is no copy there".

    `skipped`, when given, collects hits excluded here because
    _is_generated_skills_tree_hit() says so (ADR workflow-daily-work-0165).
    That exclusion belongs in this inventory step, not as a labelled verdict
    in classify(): the tool's contract is "find every copy that MIGHT need
    repair", and a generated-tree hit never does, so it should never surface
    as a row at all. Per the tool's own honesty rule, the caller must still
    account for `skipped` in its summary - silently dropping hits from the
    scan would be exactly the "no copy there" under-report this docstring
    already warns against for `errors`.
    """
    wanted = set(os.path.normcase(n) for n in names)
    seen, hits = set(), []

    def record(exc):
        if errors is not None:
            errors.append("%s (%s)" % (getattr(exc, "filename", None) or "?",
                                       getattr(exc, "strerror", None) or exc))

    for root in roots:
        for dirpath, dirnames, filenames in os.walk(root, onerror=record):
            dirnames[:] = [d for d in dirnames if d not in PRUNE]
            if (os.path.normcase(os.path.basename(dirpath)) in wanted
                    and "SKILL.md" in filenames):
                if _is_generated_skills_tree_hit(dirpath):
                    if skipped is not None:
                        skipped.append(os.path.abspath(dirpath))
                    continue
                key = _key(dirpath)
                if key not in seen:
                    seen.add(key)
                    hits.append(os.path.abspath(dirpath))
    return sorted(hits)


def source_blockers(root):
    """Reasons the source cannot be trusted as a baseline (ADR 0106).

    Empty list = trustworthy. Anything else and every downstream verdict
    would be graded against the wrong source, so the caller must refuse.
    """
    top = git_output(root, "rev-parse", "--show-toplevel")
    if not top:
        # None means no information, so distinguish:
        # - genuinely not a git repo: return [] (nothing to gate)
        # - git present but refused to answer: return blocker (cannot verify)
        if _git_dir_above(root):
            return [
                "git could not determine the repository root at %s - "
                "refusing rather than reporting an unverified baseline" % root]
        return []                      # not a git checkout: nothing to gate
    # Normalize both paths to canonical form for relpath calculation.
    # Windows hands out 8.3 short names (e.g. THODSA~1.SON) for temp paths
    # while git returns the long form. Without realpath, os.path.relpath
    # would calculate a .. path that matches nothing, and every later
    # git command scoped to --<rel> would check a nonexistent path while
    # appearing to pass. This line is load-bearing.
    top_normalized = os.path.normpath(os.path.realpath(top))
    root_normalized = os.path.normpath(os.path.realpath(root))
    rel = os.path.relpath(root_normalized, top_normalized).replace(os.sep, "/")
    blockers = []

    dirty = git_output(top, "status", "--porcelain", "--", rel)
    if dirty is None:
        blockers.append(
            "git status could not run at %s - refusing rather than reporting "
            "an unverified baseline" % top)
    elif dirty:
        blockers.append(
            "uncommitted changes under %s (%d path(s)) - commit them first"
            % (rel, len(dirty.splitlines())))

    head = git_output(top, "rev-parse", "--abbrev-ref", "HEAD")
    if head is None:
        blockers.append(
            "git rev-parse could not run at %s - refusing rather than reporting "
            "an unverified baseline" % top)
    else:
        refs = git_output(top, "for-each-ref", "--format=%(refname:short)",
                          "refs/heads")
        if refs is None:
            blockers.append(
                "git for-each-ref could not run at %s - refusing rather than reporting "
                "an unverified baseline" % top)
        else:
            for ref in refs.splitlines():
                if ref == head:
                    continue
                ahead = git_output(top, "rev-list", "--count",
                                   "HEAD..%s" % ref, "--", rel)
                if ahead is None:
                    blockers.append(
                        "git rev-list could not run at %s - refusing rather than reporting "
                        "an unverified baseline" % top)
                    break
                elif ahead != "0":
                    blockers.append(
                        "branch %s is %s commit(s) ahead under %s - merge it first"
                        % (ref, ahead, rel))
    return blockers


PROVENANCE_MIN = 0.70
PROVENANCE_MIN_LINES = 10


def _distinct_lines(text):
    """How many distinct non-blank lines a text has - the evidence available
    to line_overlap, counted the same way."""
    return len(set(line for line in text.splitlines() if line.strip()))


def line_overlap(a_text, b_text):
    """Share of the smaller file's non-blank lines present in the other.

    Measured against the SMALLER side so that a copy which is a strict subset
    of the source - the common drift, a line dropped - scores 1.0 rather than
    being penalised for the source having grown (ADR 0114).
    """
    a = set(line for line in a_text.splitlines() if line.strip())
    b = set(line for line in b_text.splitlines() if line.strip())
    if not a or not b:
        return 0.0
    return len(a & b) / float(min(len(a), len(b)))


def historical_hashes(path, limit=50):
    """Normalized hashes of this file's previous committed versions.

    A copy matching one of these is certainly ours, however far it has since
    fallen behind - the case line overlap alone would misjudge.

    IMPORTANT: An empty set means "no evidence found", not "confirmed no history".
    When git cannot answer (failed rev-parse or log), this function returns an
    empty set indistinguishably from "this file has no history". A copy that
    would have matched a historical version then gets graded by line overlap
    alone, and at PROVENANCE_MIN=0.70 can return UNRELATED - meaning the tool
    says nothing about a copy that is genuinely stale.

    Mitigation: The source-health gate (source_blockers) runs first and refuses
    the whole run when git cannot answer for this repository, blocking the tool
    before classify() is called. Holes: --allow-dirty-source bypasses the gate,
    and calling audit() directly (as tests do) has no gate at all.
    """
    directory = os.path.dirname(path)
    top = git_output(directory, "rev-parse", "--show-toplevel")
    if not top:
        return set()
    # Normalize both paths to resolve short forms and slashes consistently
    top = os.path.normpath(os.path.realpath(top))
    path_normalized = os.path.normpath(os.path.realpath(path))
    rel = os.path.relpath(path_normalized, top).replace(os.sep, "/")
    revs = git_output(top, "log", "--format=%H", "-n", str(limit), "--", rel)
    hashes = set()
    for rev in (revs or "").splitlines():
        try:
            blob = subprocess.run(
                ["git", "-C", top, "show", "%s:%s" % (rev, rel)],
                capture_output=True)
        except OSError:
            return hashes
        if blob.returncode == 0:
            hashes.add(content_hash(normalize(blob.stdout)))
    return hashes


def classify(src_bytes, copy_bytes, historical=()):
    """Grade one copy against the source. Returns (verdict, overlap).

    A name match alone never earns STALE (ADR 0107): provenance is confirmed
    from content, or the copy is somebody else's and we say nothing about it.

    The PROVENANCE_MIN_LINES floor is what makes that true for a tiny file.
    A SKILL.md stub of two lines - `---` and `name: <dir>` - matches the
    source on both of them by construction: `---` is universal, and the name
    equals the directory name, which is WHY that directory was scanned. Over
    the min() denominator that scores 1.000, so a stub belonging to somebody
    else's project graded STALE at maximum confidence - the exact failure
    provenance exists to prevent, rendered as certainty. Below the floor the
    overlap carries no information and the copy is not ours to talk about.
    """
    src = normalize(src_bytes)
    copy = normalize(copy_bytes)
    if content_hash(src) == content_hash(copy):
        return "IN SYNC", 1.0
    if content_hash(copy) in set(historical):
        return "STALE", 1.0
    src_text = src.decode("utf-8", "replace")
    copy_text = copy.decode("utf-8", "replace")
    overlap = line_overlap(src_text, copy_text)
    if min(_distinct_lines(src_text),
           _distinct_lines(copy_text)) < PROVENANCE_MIN_LINES:
        return "UNRELATED", overlap
    if overlap >= PROVENANCE_MIN:
        return "STALE", overlap
    return "UNRELATED", overlap


WORKTREE_MARK = os.path.join(".claude", "worktrees")


def _under(path, parent):
    """Check if path is under parent, using the same canonicalization as _key."""
    key, root = _key(path), _key(parent)
    return key == root or key.startswith(root + os.sep)


def role_of(path, claude_home, agents_home, source_root):
    """What kind of distribution point this copy is. The role decides the
    repair, and whether a repair may be offered at all."""
    if _under(path, os.path.join(claude_home, "plugins", "cache")):
        return "cache"
    if os.path.normcase(WORKTREE_MARK) in _key(path):
        return "worktree"
    # Both stores an npx-skills install writes: the central store under the
    # agents home, and the per-agent copy under <claude_home>/skills. The
    # per-agent copy used to fall through to `vendored`, whose repair says
    # "edit it in place" - which leaves the central store drifted and is
    # clobbered by the next per-agent install.
    if _under(path, agents_home) or _under(path, os.path.join(claude_home,
                                                             "skills")):
        return "agent-store"
    if _under(path, source_root):
        return "source"
    return "vendored"


def repair_for(role, copy_path, source_root):
    """What the runner should DO about this copy. Never a write into the
    cache (ADR 0104)."""
    if role == "cache":
        return ("none - the runtime maintains this snapshot. Edit the source "
                "at %s and let the next session refresh it. Never hand-patch "
                "the cache: a patched cache reports success while the real "
                "source stays old." % source_root)
    if role == "worktree":
        return ("none - this is another branch's checkout. Merge or rebase "
                "that branch; do not edit its files to match.")
    if role == "agent-store":
        return ("reinstall this skill for the agents that read the store, "
                "then re-run. Note that a skills `update` short-circuits on "
                "the source hash without checking this copy, so an update "
                "alone will not repair it.")
    if role == "source":
        return ("this is the source - no repair needed.")
    if role == "vendored":
        # A vendored copy is not always inside a git checkout. Asserting
        # "commit it there" for a plain, untracked directory (a backup
        # copy someone made by hand, for instance) is a confidently-worded
        # false claim - the same class of failure this tool exists to
        # catch elsewhere. _git_dir_above already answers the question
        # (it checks existence, not directory-ness, because .git is a
        # plain file inside a worktree).
        if _git_dir_above(copy_path):
            return ("edit %s in its own repo and commit it there - the copy is "
                    "git-tracked by that project, so copying a file in would leave "
                    "their tree dirty." % copy_path)
        return ("edit %s in place - no .git was found above it, so there "
                "is no repo behind it to keep in sync with." % copy_path)
    raise ValueError("unknown role: %r" % role)


def claimed_install(claude_home, marketplace, plugin):
    """What the install manifest CLAIMS. Never evidence: the directory it
    names can be absent while every field says it exists."""
    path = os.path.join(claude_home, "plugins", "installed_plugins.json")
    if not os.path.isfile(path):
        return None
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except ValueError:
        return None
    entries = (data.get("plugins") or {}).get("%s@%s" % (plugin, marketplace))
    for entry in entries or []:
        install_path = entry.get("installPath") or ""
        return {"version": entry.get("version"),
                "install_path": install_path,
                "dir_exists": os.path.isdir(install_path)}
    return None


def agent_list_warning(agents_home):
    """The trap where a skills install succeeds for every agent except this
    one. It re-arms on the next install, so the check is unconditional."""
    lock = os.path.join(agents_home, ".skill-lock.json")
    if not os.path.isfile(lock):
        return None
    try:
        with open(lock, encoding="utf-8") as f:
            data = json.load(f)
    except ValueError:
        return "the skills lock at %s is not valid JSON" % lock
    agents = data.get("lastSelectedAgents") or []
    if "claude-code" not in agents:
        return ("`claude-code` is missing from lastSelectedAgents in %s - a "
                "skills install will succeed for every other agent and report "
                "success while nothing lands for Claude Code." % lock)
    return None


def _cache_base(claude_home, marketplace, plugin):
    return os.path.join(claude_home, "plugins", "cache", marketplace, plugin)


def _cache_version(path, claude_home, marketplace, plugin):
    """The version segment of a cache-role path, when that path sits under
    THIS plugin's own cache tree
    (claude_home/plugins/cache/<marketplace>/<plugin>/<version>/...).

    Returns None when the path is a cache hit for something else entirely
    (a different marketplace or plugin that happens to share a skill name) -
    that case is graded normally, unaffected by version comparison; it is
    not what "a superseded cache version" means.
    """
    base = _cache_base(claude_home, marketplace, plugin)
    if not _under(path, base):
        return None
    rel = os.path.relpath(os.path.realpath(path), os.path.realpath(base))
    parts = rel.split(os.sep)
    return parts[0] if parts and parts[0] not in ("", os.curdir) else None


def _version_key(name):
    """Sort key for a cache version directory name. Numeric segments compare
    numerically, so 0.45.0 sorts below 0.100.0 rather than above it."""
    return [(0, int(part), "") if part.isdigit() else (1, 0, part)
            for part in str(name).split(".")]


def cache_versions(claude_home, marketplace, plugin):
    """Version directories that actually EXIST under this plugin's cache
    tree, lowest first. This is evidence; the install manifest is a claim."""
    base = _cache_base(claude_home, marketplace, plugin)
    if not os.path.isdir(base):
        return []
    return sorted((name for name in os.listdir(base)
                   if os.path.isdir(os.path.join(base, name))),
                  key=_version_key)


def cache_grading(claude_home, marketplace, plugin):
    """Which cache version directory is graded, why, and whether the claim is
    itself a finding.

    The install manifest's claimed version is used when its directory exists
    (ADR 0111). When it does not - the headline failure this tool exists to
    expose - the HIGHEST version directory present is graded instead, because
    that is the snapshot the runtime is actually loading. Grading none of them
    reported a clean machine in exactly that case, and called the only version
    present "older than the claimed version", which was false.
    """
    claim = claimed_install(claude_home, marketplace, plugin)
    if claim and claim["dir_exists"]:
        return {"claim": claim, "version": claim["version"],
                "because": "the version the install manifest claims",
                "finding": None}
    present = cache_versions(claude_home, marketplace, plugin)
    if claim is None:
        finding = ("the install manifest records no entry for %s@%s - nothing "
                   "claims this plugin is installed" % (plugin, marketplace))
    else:
        finding = ("the install manifest claims version %s at %s, but that "
                   "directory does NOT exist - a manifest naming a directory "
                   "that was never created is the failure this tool exists to "
                   "expose" % (claim["version"], claim["install_path"]))
    return {"claim": claim, "version": present[-1] if present else None,
            "because": ("the highest cache version present - the install "
                        "manifest's claim is not usable, so what is on disk "
                        "is what loads"),
            "finding": finding}


def _row(path, skill, role, verdict=None, overlap=None, repair="",
         not_graded_reason=None):
    """The one row shape the report reads.

    A row is graded, or it carries the reason it was not - one representation,
    so a reason can only be printed for the branch that produced it, and a
    suppression cannot be added without a reason to show for it.
    """
    return {"path": path, "skill": skill, "role": role,
            "verdict": verdict if not_graded_reason is None else "NOT GRADED",
            "overlap": overlap, "repair": repair,
            "graded": not_graded_reason is None,
            "not_graded_reason": not_graded_reason or ""}


def audit(plugin, marketplace, claude_home, agents_home, extra_roots=()):
    """Measure every copy of `plugin` on this machine against its source.
    Reports; changes nothing."""
    registry = load_registry(claude_home)
    mkt_root = marketplace_root(registry, marketplace)
    source_root = plugin_root(mkt_root, plugin)
    skills = source_skills(source_root)
    if not skills:
        _die("plugin %r at %s has no skills/<name>/SKILL.md to compare"
             % (plugin, source_root))

    roots = derive_roots(registry, claude_home, agents_home)
    known = set(_key(r) for r in roots)
    for extra in extra_roots:          # additive, never a replacement
        if os.path.isdir(extra) and _key(extra) not in known:
            roots.append(os.path.abspath(extra))
            known.add(_key(extra))

    history = dict((name, historical_hashes(path))
                   for name, path in skills.items())
    by_name = dict((os.path.normcase(name), name) for name in skills)

    grading = cache_grading(claude_home, marketplace, plugin)

    # A dated backup snapshot under <claude_home>/backups is, by purpose,
    # supposed to be behind (ADR 0112). Grading it would tell a reader to
    # "edit and commit" inside what is often not even a git repo - a wrong
    # instruction that reads as confidently actionable. Scoped to the exact
    # claude_home/backups subtree via _under rather than matching the word
    # "backups" anywhere in a path: a project's own backups/ directory is a
    # real vendored copy and stays graded.
    # normpath: the default claude_home comes from expanduser("~/.claude"),
    # which mixes separators, and the reason string is read by a person.
    backups_root = os.path.normpath(os.path.join(claude_home, "backups"))

    scan_errors = []
    generated_tree_skips = []
    rows = []
    for directory in scan_for_skill_dirs(roots, list(skills), scan_errors,
                                         generated_tree_skips):
        display = os.path.basename(directory)
        name = by_name[os.path.normcase(display)]
        role = role_of(directory, claude_home, agents_home, source_root)
        if _under(directory, backups_root):
            rows.append(_row(directory, display, role, not_graded_reason=(
                "a dated backup snapshot under %s, which is supposed to be "
                "behind" % backups_root)))
            continue
        version = (_cache_version(directory, claude_home, marketplace, plugin)
                   if role == "cache" else None)
        if version is not None and version != grading["version"]:
            rows.append(_row(directory, display, role, not_graded_reason=(
                "a cache version directory other than the graded %s (%s)"
                % (grading["version"], grading["because"]))))
            continue
        verdict, overlap = classify(read_normalized(skills[name]),
                                    read_normalized(os.path.join(directory,
                                                                 "SKILL.md")),
                                    history.get(name, set()))
        rows.append(_row(directory, display, role, verdict=verdict,
                         overlap=overlap,
                         repair="" if verdict != "STALE"
                                else repair_for(role, directory, source_root)))

    return {"rows": sorted(rows, key=lambda r: (r["role"], r["path"])),
            "source_root": source_root,
            "skills": sorted(skills),
            "claim": grading["claim"],
            "claim_finding": grading["finding"],
            "cache_graded_version": grading["version"],
            "cache_graded_because": grading["because"],
            "scan_errors": scan_errors,
            "generated_tree_skips": sorted(generated_tree_skips),
            "warning": agent_list_warning(agents_home)}


def report(result):
    """Print the human-readable audit report. Returns the count of findings,
    which main() uses for --strict: every STALE row, plus an install claim
    that names a directory nobody can load."""
    print("source: %s" % result["source_root"])
    print("skills: %d (%s)" % (len(result["skills"]),
                               ", ".join(result["skills"])))
    claim = result["claim"]
    if claim:
        print("install manifest CLAIMS version %s at %s (directory %s) "
              "- a claim, not evidence"
              % (claim["version"], claim["install_path"],
                 "exists" if claim["dir_exists"] else "ABSENT"))
    if result["claim_finding"]:
        print("FINDING: %s" % result["claim_finding"])
    if result["cache_graded_version"]:
        print("cache: grading version %s - %s"
              % (result["cache_graded_version"],
                 result["cache_graded_because"]))
    if result["scan_errors"]:
        print("%d director%s could not be read, so any copy inside them is "
              "MISSING from this report, not absent:"
              % (len(result["scan_errors"]),
                 "y" if len(result["scan_errors"]) == 1 else "ies"))
        for entry in result["scan_errors"]:
            print("  - %s" % entry)
    if result["generated_tree_skips"]:
        print("%d hit%s skipped: a marketplace's own generated skills/ tree "
              "(each SKILL.md there is a REWRITTEN copy by design - the "
              "repair is regeneration via scripts/generate_skills_tree.py, "
              "never a hand edit; ADR workflow-daily-work-0165):"
              % (len(result["generated_tree_skips"]),
                 "" if len(result["generated_tree_skips"]) == 1 else "s"))
        for entry in result["generated_tree_skips"]:
            print("  - %s" % entry)
    print("")
    grouped = {}
    for row in result["rows"]:
        grouped.setdefault(row["role"], []).append(row)
    for role in sorted(grouped):
        print("  [%s]" % role)
        for row in grouped[role]:
            overlap_text = ("  n/a" if row["overlap"] is None
                            else "%3.0f%%" % (row["overlap"] * 100))
            print("    %-10s %-24s overlap %s  %s"
                  % (row["verdict"], row["skill"], overlap_text, row["path"]))
            if row["repair"]:
                print("      fix: %s" % row["repair"])
            if not row["graded"]:
                print("      not graded: %s" % row["not_graded_reason"])
        print("")
    stale = [r for r in result["rows"] if r["verdict"] == "STALE"]
    unrelated = [r for r in result["rows"] if r["verdict"] == "UNRELATED"]
    print("%d stale, %d unrelated (same name, different lineage - not ours), "
          "%d in sync - compared on skills/<name>/SKILL.md only, so a copy "
          "whose references/ or scripts/ drifted still reads IN SYNC"
          % (len(stale), len(unrelated),
             sum(1 for r in result["rows"] if r["verdict"] == "IN SYNC")))
    reasons = {}
    for row in result["rows"]:
        if not row["graded"]:
            reasons[row["not_graded_reason"]] = \
                reasons.get(row["not_graded_reason"], 0) + 1
    for reason in sorted(reasons):
        print("%d row%s NOT graded: %s"
              % (reasons[reason], "" if reasons[reason] == 1 else "s", reason))
    print("provenance threshold: %.0f%% line overlap, over at least %d "
          "distinct non-blank lines on the smaller side (below that, overlap "
          "is not evidence). A verdict's overlap shows which side of the "
          "threshold the call came from."
          % (PROVENANCE_MIN * 100, PROVENANCE_MIN_LINES))
    if result["warning"]:
        print("\nwarning: %s" % result["warning"])
    return len(stale) + (1 if result["claim_finding"] else 0)


def detect_marketplace(registry, plugin):
    """The one marketplace listing `plugin`, or exit 2 naming the count.

    A marketplace this run never asked about may record no usable location or
    no manifest at all; that is skipped, not fatal. Dying here named a
    marketplace irrelevant to the question and refused to answer it.
    """
    owners = []
    for name in sorted(registry):
        root = marketplace_root_or_none(registry, name)
        if not root:
            continue
        manifest = os.path.join(root, ".claude-plugin", "marketplace.json")
        if not os.path.isfile(manifest):
            continue
        try:
            with open(manifest, encoding="utf-8") as f:
                data = json.load(f)
        except (ValueError, OSError):
            continue
        if any((p or {}).get("name") == plugin
               for p in data.get("plugins") or []):
            owners.append(name)
    if len(owners) != 1:
        _die("pass --marketplace: %d marketplaces list plugin %r (%s)"
             % (len(owners), plugin, ", ".join(owners) or "none"))
    return owners[0]


def main(argv):
    parser = argparse.ArgumentParser(
        description="Find every copy of a plugin or skill and grade it.")
    parser.add_argument("--plugin", required=True)
    parser.add_argument("--marketplace", default=None,
                        help="defaults to the only marketplace listing the "
                             "plugin, if exactly one does")
    parser.add_argument("--claude-home",
                        default=os.path.expanduser("~/.claude"))
    parser.add_argument("--agents-home",
                        default=os.path.expanduser("~/.agents"))
    parser.add_argument("--root", action="append", default=[],
                        help="an extra scan root; additive, never a "
                             "replacement for the derived roots")
    parser.add_argument("--strict", action="store_true",
                        help="exit 1 when any finding exists")
    parser.add_argument("--allow-dirty-source", action="store_true",
                        help="report against a source that is not clean; the "
                             "report is stamped ungraded")
    args = parser.parse_args(argv)

    registry = load_registry(args.claude_home)
    marketplace = args.marketplace or detect_marketplace(registry, args.plugin)

    # The source-health gate runs BEFORE audit(), and this order is
    # load-bearing: historical_hashes() silently degrades to "no evidence
    # found" when git cannot answer for this repo, which would under-report
    # real drift if audit() ran against an untrustworthy source first.
    source_root = plugin_root(marketplace_root(registry, marketplace),
                              args.plugin)
    blockers = source_blockers(source_root)
    if blockers:
        if not args.allow_dirty_source:
            sys.stderr.write(
                "cannot run: the source is not a trustworthy baseline, so "
                "every verdict would be graded against the wrong source.\n")
            for blocker in blockers:
                sys.stderr.write("  - %s\n" % blocker)
            sys.stderr.write("  re-run with --allow-dirty-source to report "
                             "anyway (the report is then ungraded).\n")
            return 2
        print("UNGRADED REPORT - the source is not clean:")
        for blocker in blockers:
            print("  - %s" % blocker)
        print("")

    result = audit(args.plugin, marketplace, args.claude_home,
                   args.agents_home, args.root)
    findings = report(result)
    return 1 if (findings and args.strict) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
