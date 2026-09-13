# English error explanations

Canonical wording of the nine **Error class** explanations used by
`practice-english-writing` (ADRs 0175–0189). The skill points here; nothing else
restates these sentences. To change an explanation, change **this file only**.

## The rules this file obeys

| | rule | ADR |
|---|---|---|
| location | **This file is the only home.** One plugin-level reference, the shape `diagram-convention.md` already uses. | 0187 |
| register | **Plain Thai, no particles.** No ครับ — the card is a reference, not a conversation, and the skill removes ครับ from the user's own English as the `other` class. | 0188 |
| length | **Two lines, hard ceiling.** Line 1 is the fact about Thai; line 2 is the English consequence. | 0188 |
| accuracy | **Teachable first.** The card carries the simple rule; the precise version lives in `Caveat` below it and is shown only on request. | 0189 |
| authority | The **user** is authoritative on whether the Thai reads naturally. The **cited source** is authoritative on whether the claim about Thai is true. A dispute names which of the two it is. | 0189 |

A `Caveat` line exists only where the teachable version is knowingly coarser than the
truth. Where there is no caveat, the two-line version *is* accurate.

---

## `article` — Articles (a / an / the) · คำนำหน้านาม

```
คำนามไทยไม่ต้องมีคำนำหน้า
อังกฤษต้องเติม a / an / the แทบทุกครั้ง
```

**Caveat.** Thai is not strictly article-less: it has no definite article and no
*obligatory* article, but `หนึ่ง` + classifier behaves closely like an indefinite
article (Chaiphet 2023). The teachable line overstates this deliberately.

## `sv-agreement` — Subject–verb agreement · ความสอดคล้องของประธานกับกริยา

```
กริยาไทยไม่ผันตามประธาน
อังกฤษเติม -s เมื่อประธานเป็นเอกพจน์บุรุษที่ 3
```

## `preposition` — Prepositions · คำบุพบท

```
ไทยใช้บุพบทน้อยกว่า คำเดียวครอบคลุมหลายความหมาย
อังกฤษเลือกบุพบทตามคำกริยา ไม่ใช่ตามความหมาย
```

**Caveat.** One Thai preposition spans several English ones — `ที่` alone covers
*at / in / on*. And the error runs in both directions, omission *and* insertion: Thai
`ถาม` takes a direct object, which is what produces *"I ask **to** my father"*.

## `plural` — Noun number (-s) · รูปพหูพจน์

```
คำนามไทยไม่บอกจำนวน ใช้ตัวเลข + ลักษณนามแทน
อังกฤษเติม -s แม้จะมีตัวเลขอยู่แล้ว เช่น two years
```

## `verb-tense` — Verb tense · กาลของกริยา

```
กริยาไทยไม่ผันตามเวลา ใช้ แล้ว / กำลัง / จะ บอกแทน
อังกฤษต้องเปลี่ยนรูปกริยา แม้จะมีคำบอกเวลาอยู่แล้ว
```

**Caveat.** Thai marks **aspect**, not tense — `แล้ว`, `กำลัง`, `เคย` are aspect
markers and `จะ` is irrealis. Tense is left to context. This matters because it explains
why a time adverbial feels sufficient: once *last year* is present, Thai marks nothing
further on the verb.

## `copula` — Missing or extra "be" · กริยา be ที่ขาดหรือเกิน

```
คำคุณศัพท์ไทยเป็นกริยาในตัว เช่น สวย = "to be beautiful"
อังกฤษต้องมี is / are / was นำหน้าคำคุณศัพท์เสมอ
```

**Caveat.** The over-correction is as common as the omission — *"They are always support
me"* appears once the learner has been told to add *is*.

## `existential` — there is/are, not have · โครงสร้าง there is/are

```
ไทยใช้ มี ทั้งความหมาย "ครอบครอง" และ "มีอยู่"
อังกฤษแยกเป็น have กับ there is / there are
```

## `word-choice` — Direct translation · การเลือกใช้คำ

```
คำไทยหนึ่งคำมักตรงกับหลายคำในอังกฤษ เช่น เปิด = open / turn on / play
แปลตรงตัวจึงได้คำที่ความหมายใกล้เคียงแต่ผิดบริบท
```

## `capitalisation` — Capital letters · ตัวพิมพ์ใหญ่

```
ไทยไม่มีตัวพิมพ์ใหญ่-เล็ก จึงไม่มีนิสัยนี้ติดมา
อังกฤษขึ้นต้นประโยคด้วยตัวพิมพ์ใหญ่ และ I ต้องใหญ่เสมอ
```

---

## `other`

Reserved, per the open-set rule (ADR 0180 and ticket #16). It has **no canonical
explanation** — a correction filed under `other` carries a free-text note instead, and
those notes are the evidence for what the tenth class should be.

## Documented errors — the day-one seed

Used as **Drill items** only when the **Mistake profile** has no sample of the user's own —
on the first run, or for a class whose samples were deleted (ADR 0199). Every sentence below
is a real error by a real Thai writer, taken from the sources at the foot of this file. **None
is invented, and none may be**: a seed without a citation is indistinguishable from a
fabricated one, which is what ticket #16 ruled out.

The drill must say plainly that these are not the user's own sentences.

| class | documented error | correct | source |
|---|---|---|---|
| `article` | *I was like a dogs.* | I was like a dog. | Suraprajit 2021 |
| `plural` | *I was like a dogs.* | I was like a dog. | Suraprajit 2021 |
| `sv-agreement` | *She like gardening.* | She likes gardening. | Suraprajit 2021 |
| `verb-tense` | *I start over at KU last year.* | I started over at KU last year. | Suraprajit 2021 |
| `copula` | *it hard to find a job* | it is hard to find a job | Suraprajit 2021 |
| `preposition` | *I ask to my father.* | I asked my father. | Suraprajit 2021 |
| `existential` | *Have many trees in the university.* | There are many trees in the university. | Kaweera 2013 |
| `word-choice` | *the company decided to open the song again* | the company decided to play the song again | Takahashi & Thumawongsa 2024 |
| `capitalisation` | **none** | | — |

**`capitalisation` has no documented seed, and one must not be written for it.** The research
on ticket #16 collected no cited example of a Thai-learner capitalisation error — the class
was added from the user's own review of a real card (ADR 0180), not from the literature. So
`capitalisation` simply does not appear in day-one drills. It appears as soon as the user
makes one, which in practice is almost immediately, since Thai has no upper and lower case at
all.

Leaving the cell empty is the correct outcome, not a gap to fill.

## Sources

Thai facts above are drawn from the research recorded on ticket #16 of Decision map #15 —
principally Chaiphet (2023) for the noun phrase, WALS for word order and modifier
placement, and the Thai error-analysis literature (Watcharapunyawong & Usaha 2013;
Suraprajit 2021; Kaweera 2013) for which classes are real. See that ticket for the full
bibliography and for the counter-signal against treating any of this as settled.
