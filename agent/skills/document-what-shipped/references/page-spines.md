# Page spines

A **spine** is the section list of one document type. It is chosen by the **reader's question**,
because that is what the owner can answer without knowing what sections a runbook has.

| type | the reader's question | proven |
|---|---|---|
| User manual | "how do I do this?" | **yes** - published 2026-08-20 |
| Process and flow | "what happens, in what order, and who does what?" | **yes** - published 2026-08-20 |
| Release note | "what changed, and does it affect me?" | no |
| Runbook | "it went wrong - where do I look?" | no |
| Rules and limits | "what are the limits?" | no |
| *other* | ask for the reader's question, propose a spine, **write it into this file** | - |

An unproven spine is a proposal drawn from a real page, not an invention - each one says below
which section of yesterday's pages it was re-cut from. The first real run of one **updates the mark
in this file** rather than assuming it was fine.

## On every page, whatever the spine

- **The provenance line** - which environment, since when, proven on a real record or not.
- **A *not built yet* section**, whenever anything decided is unbuilt, saying what the reader
  should do instead until it ships.
- **The exact on-screen words** - button labels, page titles, status names - spelled as the product
  spells them.
- **Links out**: to the parent page, and to any sibling page that covers the other half.
- **No credential**, no signed link, no token, no price the reader is not meant to see.

---

## 1. User manual - "how do I do this?" (proven)

1. **Title**, then the status line, then one paragraph of what a reader can now do that they could
   not before.
2. **Who does what** - a small table: step, who, where. It answers "is this mine?" before the
   reader spends any attention.
3. **Before you start** - what has to be true for the feature to appear at all. A disabled button
   is the system saying no, not a fault; say which.
4. **The steps**, numbered, one per press. Each step: what the reader does, what they see, and one
   visual - a screenshot or the diagram that covers it. Where a step deliberately does nothing yet,
   say so and say why (a link that only renders a page, because a mail scanner opens every link).
5. **What one action writes**, in order, when the reader must know - "the quote closes, then a
   booking is created" - plus what happens if the second half fails, in the reader's terms.
6. **What happens next, and what does not.** The next role's step, and the gap that is real today.
7. **What a second attempt does.** Repeat presses, expired links, two people acting at once.
8. **Rules and limits** - a table of rule and value. Expiry, one-per-record, sign-in needed or not,
   what is shown and what is withheld.
9. **How to check it happened** - the record the system leaves, where it is, and what to search.
   This is the section that turns a manual into something a support person can use.
10. **Worked example** - the run that proved it, with real values and real outcomes.
11. **Where to look next** - the sibling page and the parent.

## 2. Process and flow - "what happens, in what order?" (proven)

1. **Title**, what this page is, and links to the pages it is the detail behind.
2. **Status note** - what is live, what is not, and that every diagram marks the unbuilt parts.
3. **Sequence view** - one diagram, actors and systems, one arrow per real write. Follow it with
   the *why* notes: why a two-step press exists, why two writes cannot be one.
4. **Where this joins the main flow** - a flowchart showing the new path beside the unchanged one,
   meeting where they meet.
5. **Outcome view** - every page or state an action can land on, including the failure and the
   already-answered case, and what each one deliberately does **not** say.
6. **State view** - one small state diagram per record that changes, and one line on what can never
   happen (a won quote never returns to active).
7. **Roles view** - who can do what, and the sentence that says which of them gained or lost
   access.
8. **What one action writes** - a table: record, what changes, why it matters.
9. **The safety design in one place** - a table: rule, and what it protects against.
10. **Not built yet** - numbered, each with what it does today instead.
11. **Related pages.**

## 3. Release note - "what changed, and does it affect me?" (not proven)

Re-cut from the change entry in the parent page's own change-log section.

1. **Date, environment, and one line** of what changed.
2. **What is new** - in reader-visible terms only. What they will see that was not there.
3. **What is unchanged** - said explicitly. This is the section that stops a rumour: "the reply
   route still works, no role gained or lost access".
4. **What is not built yet**, and what to do instead.
5. **Who must do something** - which role, which screen, and by when.
6. **Where the detail lives** - the manual and the flow page.

## 4. Runbook - "it went wrong, where do I look?" (not proven)

Re-cut from the manual's *how to check that it was recorded* section.

1. **Symptom table** - what the reader sees, what it means, what to do first.
2. **Where to look** - the system, the screen, and the exact search key. Name the field the search
   works on and why the record is named that way.
3. **What a healthy record looks like** - the status text, the fields, the timing.
4. **What each failure state means** - and which record is the authority when two disagree.
5. **Who owns the fix**, and what to hand them.
6. **What not to do** - the actions that make it worse or lose the evidence.

## 5. Rules and limits - "what are the limits?" (not proven)

Re-cut from the manual's *rules and limits* table.

1. **One table** - rule, value, and why the value is what it is.
2. **What each rule protects against** - a second table, or a column. A limit whose reason is
   missing gets argued with.
3. **What is out of scope** of this page, with the link that covers it.

---

## Writing a spine for *other*

1. ask for the reader's question in the reader's words;
2. propose sections, each one answering a part of that question, and get them confirmed before
   drafting;
3. after the run, append the spine here with the date, the reader's question, and `(not proven)`.

A spine is chosen, not filled in. If two sections of a spine would say the same thing for this
page, cut one - a section kept for symmetry is where invented content goes.
