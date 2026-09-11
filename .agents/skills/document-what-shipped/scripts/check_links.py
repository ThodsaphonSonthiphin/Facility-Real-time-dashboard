"""Resolve every internal link on a published page against its live destination.

Why this exists: a link that 404s looks EXACTLY like a page that was never created, so a
broken link is invisible to review. Both defects that survived review on 2026-08-20 were
found here - a page path holding a literal hyphen, and a parent page with zero links to
its two new children.

Run it after every publish, over every page the run touched - the parent included.
Exit code 1 means at least one link is dead.

  # Azure DevOps wiki, pages by id and/or by path
  python check_links.py ado --org Cartagena365 --project GlassHull \\
      --wiki GlassHull.wiki --page 685 --page 719 --page 720

  # a git file store: a local clone of a wiki, a repo docs/ folder, a plain folder
  python check_links.py files --root docs/published --page docs/published/manual.md

Add a resolver for a new destination family by writing one class with iter_pages() and
exists(); then record what you measured in references/destinations.md.
"""

import argparse
import io
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

LINK = re.compile(r"\]\((/[^)#\s]+)\)")          # markdown links to an absolute page path
REL_LINK = re.compile(r"\]\((?!https?:|/|#)([^)#\s]+)\)")   # relative links, file stores
ADO_RESOURCE = "499b84ac-1321-427f-aa17-267ca6975798"       # fixed Azure DevOps resource GUID


def slug_to_path(slug):
    """Azure DevOps: '-' in a link means a SPACE in the page path; a literal hyphen is %2D."""
    return slug.replace("%2D", "\x00").replace("-", " ").replace("\x00", "-")


class AdoWiki(object):
    """API page store. Measured against GlassHull.wiki on 2026-08-20."""

    name = "azure devops wiki"

    def __init__(self, args):
        self.base = "https://dev.azure.com/%s/%s/_apis/wiki/wikis/%s" % (
            args.org, args.project, args.wiki)
        self.pages = args.page
        self.token = subprocess.run(
            ["az", "account", "get-access-token", "--resource", ADO_RESOURCE,
             "--query", "accessToken", "-o", "tsv"],
            capture_output=True, text=True, shell=True).stdout.strip()
        if not self.token:
            sys.exit("could not get an Azure DevOps token - run `az login` first")
        self._seen = {}
        # An ADO wiki IS a git repo and its attachments are blobs under /.attachments, which is what
        # makes them checkable at all. Measured 2026-08-21: 11 referenced attachments, all present.
        self.git = "https://dev.azure.com/%s/%s/_apis/git/repositories/%s" % (
            args.org, args.project, args.wiki)
        self._attachments = None

    def _get(self, url):
        req = urllib.request.Request(url)
        req.add_header("Authorization", "Bearer " + self.token)
        req.add_header("Accept", "application/json")
        try:
            with urllib.request.urlopen(req) as r:
                return r.status, json.loads(r.read().decode("utf-8") or "{}")
        except urllib.error.HTTPError as e:
            return e.code, None

    def iter_pages(self):
        for ref in self.pages:
            if ref.isdigit():
                url = self.base + "/pages/%s?includeContent=true&api-version=7.0" % ref
            else:
                url = (self.base + "/pages?path=" + urllib.parse.quote(ref)
                       + "&includeContent=true&api-version=7.0")
            st, d = self._get(url)
            if st != 200 or not d:
                print("\nCOULD NOT READ page %s (HTTP %s)" % (ref, st))
                continue
            yield d.get("path") or ref, (d.get("content") or "")

    def links(self, content):
        return sorted(set(LINK.findall(content)))

    def _attachment_names(self):
        """Every blob under /.attachments, listed once per run.

        The trap: `path` + `recursionLevel` answers HTTP 400 naming the fix - a COLLECTION wants
        `scopePath`, not `path`. That error message is the only documentation of it.
        """
        if self._attachments is None:
            st, d = self._get(
                self.git + "/items?scopePath=/.attachments&recursionLevel=oneLevel"
                "&versionDescriptor.version=wikiMaster"
                "&versionDescriptor.versionType=branch&api-version=7.0")
            if st != 200 or not d:
                self._attachments = False          # could not list; fall back to not-checked
            else:
                self._attachments = set(
                    i["path"].rsplit("/", 1)[-1] for i in d.get("value", [])
                    if not i.get("isFolder"))
        return self._attachments

    def exists(self, link):
        if link.startswith("/.attachments/"):
            name = link.rsplit("/", 1)[-1]
            names = self._attachment_names()
            if names is False:
                return None, name                 # None = not checked, see note below
            return name in names, "attachment: " + name
        path = slug_to_path(link)
        if path not in self._seen:
            st, _ = self._get(self.base + "/pages?path=" + urllib.parse.quote(path)
                              + "&api-version=7.0")
            self._seen[path] = st
        return self._seen[path] == 200, path


class FileStore(object):
    """Git file store, checked on disk: a wiki clone, a repo docs/ folder, a plain folder."""

    name = "file store"

    def __init__(self, args):
        self.root = os.path.abspath(args.root)
        self.pages = args.page

    def iter_pages(self):
        for p in self.pages:
            with open(p, "rb") as f:
                yield p, f.read().decode("utf-8")

    def links(self, content):
        return sorted(set(LINK.findall(content)) | set(REL_LINK.findall(content)))

    def exists(self, link):
        cand = link.lstrip("/")
        base = os.path.join(self.root, cand)
        for path in (base, base + ".md", base.replace(" ", "-"), base.replace(" ", "-") + ".md"):
            if os.path.exists(path):
                return True, os.path.relpath(path, self.root)
        return False, cand


RESOLVERS = {"ado": AdoWiki, "files": FileStore}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dest", choices=sorted(RESOLVERS))
    ap.add_argument("--page", action="append", default=[],
                    help="page id or path (ado), or file path (files). Repeatable")
    ap.add_argument("--org"), ap.add_argument("--project"), ap.add_argument("--wiki")
    ap.add_argument("--root", default=".")
    args = ap.parse_args()
    if not args.page:
        sys.exit("give at least one --page. Check the parent too, not only the new page")

    r = RESOLVERS[args.dest](args)
    dead = unchecked = total = 0
    for name, content in r.iter_pages():
        links = r.links(content)
        print("\npage %s | %d chars | %d internal links" % (name, len(content), len(links)))
        for link in links:
            ok, resolved = r.exists(link)
            total += 1
            if ok is None:
                unchecked += 1
                print("   UNCHECKED attachment:", resolved)
            elif ok:
                print("   OK       %s -> %r" % (link, resolved))
            else:
                dead += 1
                print("   DEAD     %s -> %r" % (link, resolved))

    print("\n%d links checked on %s: %d dead, %d unchecked" % (total, r.name, dead, unchecked))
    if unchecked:
        print("note: attachment existence is NOT verified here - no measured call for it. "
              "Confirm attachments by opening the published page once.")
    if dead:
        print("A dead link is indistinguishable from a page that never existed. Fix before "
              "telling anybody the page is live.")
        sys.exit(1)


if __name__ == "__main__":
    main()
