# Phone Pages: Overrides

Applies to the Cleaner and Supervisor pages (M0–M16: login, My Work, check-in, scan and result, My Building, inspection). Rules here override `../MASTER.md`; everything else follows it.

## Context

One hand, standing at a QR sign, often in a hurry, sometimes outdoors in sun. Mobile data, not always Wi-Fi (facility-0055). Mid-range Android phones as well as iPhones.

## Layout

- One column, content width up to `--phone-width` (480 px), 16 px side gutter.
- The primary action sits at the bottom of the content, full width, 56 px high, in thumb reach.
- A height in the page's flow is measured in `svh` (the screen with the browser's toolbars shown), never `dvh`. A phone hides its toolbar as the page scrolls, `dvh` grows with it, and anything placed by that height slides along with the scroll (the check-in page, owner, 2026-10-27). A fixed layer that covers the screen, such as the Drawer, may use `dvh`.
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
- **My Work B, decided while building #73 (owner, 2026-10-22):** a ต้องแก้ไข row in ต้องทำต่อ also shows its defect ("หัวหน้าให้แก้ 09:40: “…”"), so a second sent-back point is not hidden until it reaches the large card. The progress bar shows ตรวจผ่าน and รอตรวจ in their own colours.
- **Check-in page, decided while building #75 (owner, 2026-10-22):** when the page offers more than one ลงเวลา button (such as ออกพัก and เลิกงาน), only the first, the next event, is blue; the others are outlined. All are 56 px and full width, so "ลงเวลา buttons are 56 px primary" in #75 means the size.
- **Inspection page, decided while building #76 (owner, 2026-10-22):** สะอาด / ไม่สะอาด are two `ChoiceTile`s, and either turns blue when chosen, like the scan page's เรียบร้อย / พบปัญหา: blue means "the answer you picked", not good or bad. (The old look turned ไม่สะอาด red.)
