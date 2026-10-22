# Phone Pages: Overrides

Applies to the Cleaner and Supervisor pages (M0–M16: login, My Work, check-in, scan and result, My Building, inspection). Rules here override `../MASTER.md`; everything else follows it.

## Context

One hand, standing at a QR sign, often in a hurry, sometimes outdoors in sun. Mobile data, not always Wi-Fi (facility-0055). Mid-range Android phones as well as iPhones.

## Layout

- One column, content width up to `--phone-width` (480 px), 16 px side gutter.
- The primary action sits at the bottom of the content, full width, 56 px high, in thumb reach.
- At most one primary (blue) button per screen; other actions are secondary (outlined) or links.
- Respect the safe areas at top and bottom (`env(safe-area-inset-*)`), so nothing sits under the notch or the home bar.

## Type and density

- Body 17 px minimum; headings clearly larger; standard density (16–64 px spacing).
- Short lines; break long Thai sentences rather than shrinking them.

## Interaction

- No hover-dependent behaviour. Pressed feedback within about 100 ms (colour or opacity, no layout shift).
- The scan and check-in results must be readable at arm's length: status chip and heading large.

## Layouts chosen from the mockups (owner, 2026-10-22)

Mockups: branch `prototype/tailwind-look`, `docs/design/tailwind-look-prototype.html`.

- **My Work: variant B.** One large "ทำอันนี้ก่อน" card for the most urgent point (name, chip, round, what to do, "สแกน QR ที่จุดนี้"), a progress line ("x จาก y จุด ส่งแล้ว"), then short rows grouped ต้องทำต่อ / ส่งแล้ว / ยังไม่ถึงรอบ, each row name + Area + round + chip.
- **Scan page: variant A.** One form: two large choice tiles (เรียบร้อย / พบปัญหา, at least 96 px high), issue tags as 48 px toggle chips, the note field, then ส่งงาน 56 px full width.
- **My Work B, decided at the spec (owner, 2026-10-22):** the large card shows the most urgent point whose status is ต้องแก้ไข, เลยเวลา or ยังไม่ทำ, with words that fit the status (ต้องแก้ไข: "แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่"; เลยเวลา and ยังไม่ทำ: "สแกน QR ที่จุดนี้เพื่อส่งงาน"). When none is left there is no large card; an info note says "ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว". The last group is named **ยังไม่ต้องทำ** and holds both ยังไม่ถึงรอบ and นอกเวลา (the mockup's "ยังไม่ถึงรอบ" was wrong for นอกเวลา).
