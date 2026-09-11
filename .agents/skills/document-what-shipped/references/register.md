# Register - which words may appear

The reader is named at the start of the run, and the reader decides which words are allowed on the
page. This is not tone. It decides content.

| reader | banned | kept exactly |
|---|---|---|
| **customer or manager** | file paths, table and column names, branch names, commit and ticket numbers, ADR numbers, class and method names | button labels, page titles, status names, dates, the product's own nouns |
| **operator** | the same, plus internal service and queue names | the same, plus the screen each step happens on |
| **engineer** | nothing | the same, plus every identifier - here the path *is* the useful part |

**Banned for every reader, and not negotiable by the reader's answer:**

- any credential - a signed link, a token, a key, a password. A signed accept link is a credential
  for the record it names;
- personal data that the page does not need;
- a price or total the reader is not meant to see. If the product's own pages carry none, the
  documentation does not add any.

## Translate mechanism into cause and effect

The test is whether the reader can **act** on the sentence. An identifier they cannot open is
noise, and it goes stale while the sentence around it still reads as current.

| written for an engineer | written for a manager |
|---|---|
| "the controller writes the chosen voyage id, then calls the close-as-won path" | "the voyage the customer chose is recorded on the quote, then the quote closes as won" |
| "reject is decided in ADR 0246, unimplemented on the delivery ref" | "the reject link does not reject yet. Until it ships, ask the customer to reply by email if they want to decline in writing" |
| "the two writes are not transactional" | "these are two separate steps. If the second one fails the customer still sees the confirmation, because the answer itself was recorded - an Agent finishes it with Add Booking" |
| "expiry is 7 days from the send timestamp" | "the link works for **7 days** from the day the quote email was sent" |

Three habits carry most of it:

1. **Name the screen, not the code path.** A reader locates themselves by what is in front of them.
2. **Say what it protects against.** A rule with no reason gets argued with; a rule with one gets
   followed. *"Opening a link only shows a page, so a mail scanner cannot answer for the
   customer."*
3. **Say who does the next thing.** Most questions a manual receives are "is this mine?".

## Wording rules that apply whatever the reader

- Short sentences, one idea each, active voice. The register is close to ASD-STE100 Simplified
  Technical English, and that is deliberate: many readers are reading in a second language.
- **Quote on-screen words exactly, from the measurement.** Not from memory, and not tidied up. A
  reader who cannot match your word to the screen assumes they are on the wrong screen.
- **A status the reader sees may differ from what is stored.** Say the one they see, and say the
  other only if they will meet it - *the stored status is Requested, which the Portal shows as
  Planning*.
- **Every count and every scope word must come from the data.** "All", "every", "only", "both",
  "3 of 4" - each one is a measurement, not a flourish. A page that says *four voyages matched, the
  customer received three links* had both numbers counted; a page that says *all of them* when one
  was excluded is simply false.
- **Mark the unbuilt in the reader's words**, on the page, not in a side file: *"the reject link
  opens a page saying an Agent will be in touch. It does not close the quote."*

## Where the identifiers go instead

Into the record beside the draft - the ref, the build number, the queries, the ledger, the ADR
numbers. Nothing is lost; it is filed where the reader who needs it looks, and where a wrong value
can be corrected without editing a page a customer reads.

## Relation to `management-talk`

Use its register section - cause and effect, no identifier the reader cannot act on, the reader's
own vocabulary. Ignore its channel shapes: Slack posts, standup lines and JIRA comments are
messages, and this output is a durable page that will be read months later by somebody who was not
in the conversation.
