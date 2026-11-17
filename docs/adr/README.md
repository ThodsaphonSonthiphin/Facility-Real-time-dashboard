# ADR reading guide — current decisions

ทบทวน 3 พ.ย. 2026 เทียบ `master` `aa1963d` และ [handoff](../handoff.md) เลขล่าสุดคือ **facility-0107** เอกสารนี้เป็นทางเข้าอ่าน ADR ไม่ใช่การตัดสินใจใหม่ และไม่ใช่หลักฐานว่าขึ้น production หรือทำ pilot แล้ว

อ่าน amendment ที่ใหม่กว่าด้วยเสมอ ADR เก่าเก็บทางเลือกและเหตุผลในวันนั้นไว้ ข้อความเดิมที่ถูกแทนที่ไม่ใช่ backlog ปัจจุบัน หากขัดกับ wireframe/แผนเก่า ให้ยึด ADR และ amendment ที่ยังมีผล

## ตารางงานแม่บ้านตาม Requirement ใหม่ — 16–17 พ.ย. 2026

เลขล่าสุดคือ **facility-0222** ข้อตกลงในส่วนนี้เป็นแบบที่ยืนยันระหว่าง `grill-then-plan` **ยังไม่ได้พัฒนา** ดู [ข้อมูลจาก Supervisor และคำถามที่ยังเปิด](../requirements-gathering/2026-11-16-supervisor-scheduling-requirements.md) ผลกระทบต่อแผนหน้าภาพรวมทบทวนแล้วใน [delta review](../superpowers/reviews/2026-11-17-overview-plan-assumption-delta.md)

| เรื่อง | ข้อตกลงและจุดเข้าอ่าน |
|---|---|
| Save incomplete rosters with visible gaps | [0208](facility-0208-save-incomplete-rosters-with-visible-gaps.md) |
| Save the submitted scheduling batch atomically | [0209](facility-0209-schedule-batches-save-atomically.md) |
| Copy day-off choices by stable identity and preserve source gaps | [0210](facility-0210-copy-days-off-by-identity-with-visible-source-gaps.md) |
| Choose a first-break group explicitly for relief work without a replaced Cleaner | [0211](facility-0211-standalone-relief-work-requires-explicit-break-group.md) |
| Preserve an in-progress break across an Area transfer | [0212](facility-0212-area-transfer-preserves-an-in-progress-break.md) |
| Cancel pending transfers; append changes after confirmed arrival | [0213](facility-0213-cancel-pending-transfer-or-append-after-arrival.md) |
| Historical plan corrections require reason and history | [0214](facility-0214-historical-plan-corrections-require-reason-and-history.md) |
| Use effective-dated scheduling records and a shared scoped projection | [0215](facility-0215-effective-dated-scheduling-records-and-scoped-projection.md) |
| Overview entry includes confirmed transfer arrival | [0216](facility-0216-overview-entry-includes-confirmed-transfer-arrival.md) |
| Do not invent a day-off quota or automatic weekly defaults | [0217](facility-0217-no-invented-day-off-quota-or-automatic-week-defaults.md) |
| Limit schedule return memory to ongoing use | [0218](facility-0218-schedule-return-memory-is-limited-to-ongoing-use.md) |
| Do not impose a synthetic week limit on schedule reading | [0219](facility-0219-schedule-reading-has-no-arbitrary-week-limit.md) |
| Use the multi-person weekly roster for relief timetable editing | [0220](facility-0220-relief-editor-uses-the-multi-person-weekly-roster.md) |
| Use revisions and durable command receipts for scheduling writes | [0221](facility-0221-schedule-commands-use-revisions-and-durable-receipts.md) |
| Identify attendance lifecycle and preserve legacy stored event values | [0222](facility-0222-attendance-lifecycle-identifies-legacy-and-numbered-breaks.md) |
| วิธีบันทึกยืมสแปร์ | ซุปตกลงกันก่อนตามช่องทางเดิม แล้วซุปปลายทางบันทึกงาน ไม่เพิ่มปุ่มอนุมัติในเว็บ [0207](facility-0207-destination-supervisor-records-relief-loan-after-offline-agreement.md) |
| ใครตกลงให้ยืมสแปร์ | กรณีคนละซุป ให้ซุปตึกต้นทางตกลงก่อน แล้วซุปปลายทางจัดงาน วิธีบันทึกยืนยันใน 0207 แล้ว [0206](facility-0206-source-building-supervisor-agrees-before-cross-team-relief-assignment.md) |
| มาวันหยุดก่อนปรับตาราง | รับสแกนที่ผ่านสิทธิ์ตามเวลาจริง แสดง conflict ให้ซุปตรวจและปรับตารางวันนั้น ไม่เปลี่ยนวันหยุด/ย้ายสแปร์เอง [0205](facility-0205-allow-eligible-day-off-check-in-with-explicit-schedule-review.md) |
| ย้ายหน้างานระหว่างกะ | ซุปสั่งย้าย คงเวลาเข้างานและลำดับพักเดิม [0203](facility-0203-midshift-area-transfer-preserves-shift-and-break-progress.md); สแกนป้ายพื้นที่ใหม่ยืนยันถึงจริงหนึ่งครั้ง ไม่สร้าง ShiftIn ซ้ำ [0204](facility-0204-midshift-transfer-requires-one-destination-sign-arrival-confirmation.md) |
| เข้างานก่อนยกเลิกลาในเว็บ | รับสแกนที่ผ่านสิทธิ์ตามเวลาจริง แสดงรายการขัดกันให้ซุปตรวจและยกเลิกลา ไม่ยกเลิกลา/ย้ายคนอัตโนมัติ [0202](facility-0202-allow-eligible-check-in-before-explicit-leave-cancellation-with-visible-conflict.md) |
| ลาครึ่งวันแล้วกลับบ้าน | สแกนเลิกงานตามเวลาออกจริง แยกรายการลา และจบการส่งงานในกะนั้น [0200](facility-0200-half-day-final-departure-records-actual-shift-out.md) |
| พักหลังออกงานเพราะลา | ช่วงลาที่บันทึกไว้หลังออกจริงแสดงไม่ต้องลงเวลาเนื่องจากลา สแกนที่ขาดก่อนออกยังขาดตามจริง [0201](facility-0201-leave-after-final-departure-makes-later-break-actions-inapplicable.md) |
| ตึกประจำของสแปร์และการยืม | สแปร์มีตึกประจำ ตึกที่ขาดขอคนส่วนเกินจากอีกตึกมาทำแทนได้ ผู้ตกลง/ผู้บันทึกและขั้นตอนในเว็บยังเปิด [0194](facility-0194-relief-cleaners-have-home-buildings-with-surplus-borrowing.md) |
| เปลี่ยนงานหลังเริ่มกะ | ซุปประจำตึกเปลี่ยนหน้างานได้ตามความเหมาะสม/จำเป็นเร่งด่วน คนมาทำแทนทำงานตามตาราง วิธีย้ายยืนยันเพิ่มใน 0203/0204 ส่วนกรณียกเว้นยังต้องระบุ [0199](facility-0199-building-supervisor-may-change-assignments-after-work-starts.md) |
| มาทำงานในวันลา | ยกเลิกวันลา ลำดับกับ check-in ยืนยันเพิ่มใน 0202 แล้ว ส่วนจัดสแปร์เดิมยังเปิด [0198](facility-0198-cancel-recorded-leave-when-working-on-that-leave-day.md) |
| มาทำงานวันหยุด | มีกรณีนี้จริง ลำดับแก้ตาราง/ลงเวลายืนยันเพิ่มใน 0205 ส่วนผลต่อคนแทนเดิมยังไม่เลือกแทนซุป [0197](facility-0197-working-on-a-planned-day-off-occurs.md) |
| ไม่มีลาเป็นชั่วโมงไปทำธุระ | ไม่สร้างขั้นตอนออกไปลา/กลับจากลาสำหรับกรณีที่ไม่เกิด ลาเต็มกะ/ครึ่งวันยังอยู่ในขอบเขต [0196](facility-0196-no-hourly-errand-leave-and-return-workflow.md) |
| คนออกครึ่งวัน/กรณี OT | ไม่มีคนรับหน้าที่แทนจนจบกะ แต่มีคนช่วยงานจำเป็น เช่น เก็บขยะ/เติมกระดาษในห้องน้ำ ไม่ถือว่ารับแทนทั้ง Area [0195](facility-0195-partial-shift-departures-have-no-replacement-but-essential-task-help.md) |
| คัดลอกแล้วไม่ทับวันที่เลือกไว้ | เก็บวันหยุดที่เลือกไว้ในสัปดาห์เป้าหมาย แล้วเติมเฉพาะคนที่ยังไม่ได้เลือก [0193](facility-0193-copy-previous-week-days-off-fills-only-unset-choices.md) |
| ใช้วันหยุดจากสัปดาห์ก่อน | มีปุ่มให้หัวหน้ากดคัดลอกวันหยุดจากสัปดาห์ก่อน แล้วแก้เฉพาะคนที่เปลี่ยน ก่อนตรวจสรุปและบันทึก [0192](facility-0192-weekly-days-off-have-an-explicit-copy-previous-week-action.md) |
| หน้าจัดวันหยุดรายสัปดาห์ | ใช้ตารางรวมรายคน เห็นหลายคนและวันหยุดสัปดาห์ก่อน เลือกวันหยุดสัปดาห์นี้ในแต่ละแถว แล้วตรวจสรุปก่อนบันทึก [0191](facility-0191-weekly-days-off-use-the-multi-person-roster-layout.md) |
| เวลาพักที่เคยตั้งเฉพาะวัน | เปลี่ยนกลุ่มพักประจำใหม่แล้ว ยังเก็บเวลาพักที่ตั้งแยกเฉพาะวันนั้นไว้ กรณีวันทำงานที่ใช้เวลาพักได้ตามปกติ [0189](facility-0189-regular-break-changes-preserve-dated-break-overrides.md) |
| หน้าแก้ตารางประจำ | ใช้แบบที่แสดง: เลือกแม่บ้าน → แก้พื้นที่/วันหยุด/กลุ่มพัก → เลือกวันเริ่มใช้ → ตรวจสรุปก่อนบันทึก [0188](facility-0188-regular-timetable-editor-reviews-changes-before-save.md) |
| วันเริ่มใช้ตารางประจำใหม่ | หัวหน้าเลือกวันที่อนาคตเองได้ รวมถึงกลางสัปดาห์ ไม่ต้องรอจันทร์ถัดไป [0187](facility-0187-regular-timetable-has-a-selected-future-start-date.md) |
| ตัวกรองเริ่มต้นของหัวหน้า | เปิดครั้งแรกเห็นทุกตึกที่ตนรับผิดชอบในกะตัวเอง แล้วกรองดูตึกเดียวได้ [0186](facility-0186-supervisor-schedule-starts-with-all-assigned-buildings.md) |
| จำตัวกรองเมื่อกลับเข้าตาราง | หัวหน้าและ Admin กลับจากหน้าอื่นระหว่างใช้งานครั้งเดิม ให้จำตึกและกะที่มีสิทธิ์ไว้พร้อมสัปดาห์ที่เพิ่งดู [0185](facility-0185-scheduling-restores-building-and-shift-filters-on-return.md) |
| ตัวกรองเริ่มต้นของ Admin | เปิดครั้งแรกเห็นทุกตึกของกะปัจจุบันตามเวลา แล้วเลือกตึกหรือเปลี่ยนกะได้ กะดึกยึดวันเริ่มกะ [0184](facility-0184-admin-schedule-starts-all-buildings-current-shift.md) |
| สัปดาห์เริ่มต้นและการกลับเข้าตาราง | เปิดครั้งแรกเห็นสัปดาห์นี้ทันที [0180](facility-0180-cleaner-schedule-opens-current-week.md); กลับเข้าหน้าระหว่างใช้งานครั้งเดิมให้จำสัปดาห์ที่เพิ่งดู [0181](facility-0181-cleaner-schedule-restores-last-viewed-week-on-return.md); หัวหน้าและ Admin ใช้สองข้อนี้เหมือนแม่บ้าน [0182](facility-0182-all-scheduling-roles-share-week-navigation-defaults.md); ถ้ายังทำกะดึกคืนวันอาทิตย์อยู่ ให้เปิดสัปดาห์ที่มีวันเริ่มกะนั้นก่อน [0183](facility-0183-ongoing-night-shift-keeps-its-start-week-on-first-open.md) ส่วนค่าเริ่มต้นตัวกรองยังไม่ยืนยัน |
| ทางเข้าตาราง | แม่บ้านใช้ “ตารางของฉัน” ก่อนรายการงาน [0176](facility-0176-cleaner-schedule-entry-precedes-work-list.md), หัวหน้าใช้ “จัดตารางงาน” ก่อนรายการตึก [0177](facility-0177-supervisor-schedule-entry-precedes-building-list.md), Admin เพิ่ม “ตารางงาน” ถัดจากการเข้างาน [0178](facility-0178-admin-schedule-menu-follows-attendance.md); เข้าดูหลัง login โดยไม่ต้องสแกน QR หรือเปิด GPS [0179](facility-0179-schedule-viewing-does-not-require-qr-or-gps.md) |
| ประวัติตาราง | เปลี่ยนตารางสำหรับช่วงถัดไปไม่ย้อนเปลี่ยนวันเก่า [0173](facility-0173-later-timetable-changes-preserve-earlier-schedules.md), ระบุวันที่ไม่มีประวัติโดยไม่เดาจากค่าปัจจุบัน [0174](facility-0174-show-unavailable-schedule-history-without-inference.md), แก้แผนไม่เปลี่ยนเวลาสแกนจริง [0175](facility-0175-schedule-edits-do-not-rewrite-actual-scan-times.md) |
| อัปเดตตารางที่เปิดค้าง | หน้าดูตารางโหลดค่าล่าสุดอัตโนมัติ [0170](facility-0170-open-schedule-views-refresh-on-change.md), แจ้งข้อมูลใหม่โดยไม่ล้างค่าที่กำลังกรอก [0171](facility-0171-schedule-refresh-preserves-unsaved-input.md), แสดงเมื่อข้อมูลอาจไม่ล่าสุดและโหลดใหม่หลังเชื่อมต่อกลับ [0172](facility-0172-schedule-views-show-staleness-and-refetch-on-reconnect.md) |
| เริ่มใช้สแกน 6 ครั้ง | กะที่ใช้อยู่จบแบบเดิม เริ่มแบบใหม่พร้อมกันตามกะที่กำหนดก่อนเปิดรับลงเวลา [0168](facility-0168-six-action-attendance-starts-at-a-shift-boundary.md); หน้าเก่าโหลดล่าสุดก่อนลงเวลาในกะใหม่ โดยจำกัดเงื่อนไขกับส่วนที่ใช้รูปแบบใหม่ [0169](facility-0169-refresh-stale-attendance-pages-for-new-flow.md) |
| บันทึกตารางอย่างปลอดภัย | กันข้อมูลเก่าทับข้อมูลที่เพิ่งแก้และเก็บค่าที่กรอกไว้เทียบ [0165](facility-0165-scheduling-rejects-stale-overwrites.md), คำสั่งบันทึกเดิมมีผลครั้งเดียว [0166](facility-0166-repeated-schedule-save-applies-once.md), ตรวจผลเมื่อการเชื่อมต่อขาดก่อนลองบันทึกใหม่ [0167](facility-0167-check-uncertain-schedule-save-before-retry.md) |
| ผู้จัดตาราง | หัวหน้าบันทึกเอง [0131](facility-0131-supervisor-manages-cleaner-schedules-in-web.md), จำกัดความรับผิดชอบ [0132](facility-0132-supervisor-scheduling-scoped-to-own-building-and-shift.md), Admin จัดได้ทั้งหมดพร้อมประวัติ [0133](facility-0133-admin-manages-schedules-across-buildings-and-shifts.md) |
| พื้นที่ประจำและวันหยุด | มีพื้นที่ประจำ [0134](facility-0134-cleaners-retain-regular-areas-with-dated-exceptions.md), หัวหน้าเลือกวันหยุดต่างกันแต่ละสัปดาห์ได้ แม้ส่วนใหญ่มักใช้วันเดิม [0190](facility-0190-supervisor-selects-cleaner-days-off-by-week.md) แก้ข้อจำกัดวันเดิมตายตัวใน [0135](facility-0135-cleaners-have-recurring-weekly-days-off.md), คนหยุดแต่งานพื้นที่ยังครบ [0136](facility-0136-regular-day-off-keeps-all-area-cleaning-rounds.md) |
| แม่บ้านสำรอง | ไม่มีพื้นที่ประจำ [0137](facility-0137-dedicated-relief-cleaners-cover-days-off.md), จัดงานครบ 6 วัน [0138](facility-0138-relief-cleaners-receive-six-days-of-assigned-work.md), ไปต่างตึกได้ [0139](facility-0139-relief-cleaners-may-work-across-buildings.md), มีกะประจำ [0143](facility-0143-relief-cleaners-keep-a-regular-shift.md), หนึ่งพื้นที่เต็มกะ [0144](facility-0144-relief-cleaner-takes-one-area-for-the-whole-shift.md), ตารางซ้ำและแก้เฉพาะวันที่ [0145](facility-0145-relief-schedules-repeat-weekly-with-dated-overrides.md) |
| หัวหน้าดูแลหลายตึก | มอบหมายในชุดตึกที่รับผิดชอบ [0140](facility-0140-supervisor-may-schedule-multiple-assigned-buildings.md), ใช้ชุดเดียวกันทั้งดูงาน/จัดงาน/ตรวจ [0141](facility-0141-supervisor-shares-building-scope-for-view-schedule-and-inspection.md), ตึกหนึ่งมีหัวหน้าประจำกะละคนเดียว [0142](facility-0142-one-regular-supervisor-per-building-and-shift.md) |
| พัก 2 รอบ | สแกน 6 ครั้ง [0146](facility-0146-two-breaks-use-six-scanned-attendance-actions.md), พัก 2 กะเช้า 16:00–17:00 [0147](facility-0147-day-shift-second-break-is-1600-to-1700.md), พัก 1 สลับกลุ่ม/พัก 2 พร้อมกันก่อน OT [0148](facility-0148-first-break-staggered-second-break-shared-before-ot.md), รับเวลาจริงเทียบแผน [0149](facility-0149-break-scans-record-actual-times-against-plan.md), กะยัง 07:00–19:00 / 19:00–07:00 [0150](facility-0150-cleaners-keep-full-day-and-night-shift-hours.md) |
| กลุ่มพัก | คนประจำมีกลุ่มเริ่มต้นและแก้เฉพาะวันที่ [0151](facility-0151-regular-cleaners-have-default-break-groups-with-dated-changes.md), สแปร์เริ่มจากกลุ่มคนที่ทำแทนและหัวหน้าเปลี่ยนได้ [0152](facility-0152-relief-break-group-defaults-to-replaced-cleaner.md) |
| ลงเวลาและเติมเวลาที่ขาด | ข้ามจังหวะพักที่ลืมโดยไม่สร้างเวลา [0153](facility-0153-missing-break-scan-can-be-skipped-without-fabricating-time.md), หัวหน้าเติมเวลาที่ขาดของทีมพร้อมเหตุผล/ประวัติ [0154](facility-0154-supervisor-may-fill-missing-team-attendance-with-reason.md), สแปร์ใช้ป้ายพื้นที่ที่ได้รับมอบหมายวันนั้น [0155](facility-0155-relief-attendance-uses-the-assigned-area-sign.md) |
| วันลา | หัวหน้าบันทึกหลังตกลงกันแล้ว ไม่มีขั้นตอนยื่นขอลา/อนุมัติในเว็บ [0156](facility-0156-supervisor-records-agreed-leave-without-request-workflow.md); รองรับเต็มกะและบางช่วง เช่น ครึ่งวัน/รายชั่วโมง [0157](facility-0157-recorded-leave-includes-partial-shifts.md) บันทึกลาเต็มกะได้แม้ยังไม่มีคนแทนและแสดงรอจัดคน [0158](facility-0158-full-shift-leave-can-be-saved-with-replacement-pending.md); การจัดสแปร์เมื่อมีลาบางช่วง **รอเจ้าของไปถามหัวหน้างาน** ยังไม่มีคำตอบยืนยัน |
| ตารางส่วนตัวแม่บ้าน | แม่บ้านดูพื้นที่ กลุ่มพัก วันหยุดและวันลาของตนเองบนมือถือได้ แต่แก้ตารางไม่ได้ [0159](facility-0159-cleaners-can-view-their-own-schedule-read-only.md); รูปแบบ mockup ยอมรับแล้วตาม [0160](facility-0160-scheduling-mockup-layout-accepted.md) และเมนูตาม [0176](facility-0176-cleaner-schedule-entry-precedes-work-list.md) ส่วนวันเริ่มต้นและช่วงวันที่ยังรอยืนยัน |
| ส่งงานเมื่อขาดสแกนกลับจากพัก | ยังส่งงานได้หลังเข้างานและก่อนลงเวลาเลิกงาน โดยคงเวลาที่ขาดให้ตรวจเติม [0161](facility-0161-missing-break-return-does-not-block-work-submission.md) |
| รอบงานกับเวลาพัก | ใช้เวลารอบเดิม ไม่เพิ่มการเลื่อนอัตโนมัติ เพราะตารางจริงไม่ชนเวลาพัก [0163](facility-0163-keep-configured-rounds-without-automatic-break-rescheduling.md) แทนข้อเสนอสมมติ [0162](facility-0162-cleaning-rounds-move-automatically-when-breaks-conflict.md) |
| ประวัติลงเวลาเก่า | เก็บ 4 จังหวะเดิม ไม่สร้างหรือเตือนว่าขาดพักรอบที่ 2 ของกะเก่า เริ่ม 6 จังหวะกับกะที่ใช้แบบใหม่ [0164](facility-0164-preserve-legacy-four-action-attendance-history.md) |

**Mockup ตารางงานฉบับแรกยอมรับแล้ว:** เจ้าของให้ใช้รูปแบบหน้าหัวหน้าจัดตารางและหน้าแม่บ้าน/สแปร์ดูตารางของตนเองเป็นฐานคุยรายละเอียดต่อ ตาม [0160](facility-0160-scheduling-mockup-layout-accepted.md) ดูขอบเขตและแหล่งไฟล์ใน [บันทึก Requirement](../requirements-gathering/2026-11-16-supervisor-scheduling-requirements.md) ยังไม่ได้ยืนยันรายละเอียดที่ค้างหรือเขียน spec/แผนใหม่

**รอหัวหน้ายืนยัน Workflow เป็นชุด 5 ข้อ:** ทีมสแปร์/การยืมคน, การทำแทนเมื่อลาบางช่วง, การสแกนเมื่อออกไปลา/กลับมา, มาทำงานในวันที่หยุด/ลา, และเปลี่ยนคนหรือพื้นที่หลังเริ่มงาน ดู [สรุปและแบบถามหัวหน้า](../requirements-gathering/2026-11-16-supervisor-workflow-confirmation.md) เจ้าของขอให้ถามเรื่องหน้างานกับหัวหน้าก่อน ส่วน UI/เทคนิคให้ทีมตัดสินใจและรวมตรวจเป็นชุด ยังไม่มีคำตอบแทนหน้างาน

## แบบหน้าภาพรวมใหม่ — 16 พ.ย. 2026

ส่วนนี้เพิ่มจากฐานทบทวน 3 พ.ย. ด้านบน ชุดข้อตกลงหน้าภาพรวมถึง **facility-0130**: 0114–0127 เป็นข้อตกลงออกแบบที่เจ้าของยืนยันแล้ว ส่วน 0128–0130 ยอมรับเป็นฐานวางแผนแล้วหลังเจ้าของเรียก `sp-writing-plans` โดยมี [เอกสารออกแบบ](../superpowers/specs/2026-11-16-combined-overview-dashboard-design.md) และ [แผนงาน](../superpowers/plans/2026-11-16-combined-overview-dashboard.md) **ยังไม่ได้ implement** และเจ้าของสั่งให้พัก implementation ไว้

| เรื่อง | จุดเข้าอ่าน |
|---|---|
| ผู้ดูและการกดต่อ | [0114 Admin](facility-0114-overview-dashboard-admin-audience.md), [0115 รายชื่อไป Area](facility-0115-overview-dashboard-drill-through-to-area.md), [0124 Mockup ที่ยืนยัน](facility-0124-overview-mockup-layout-accepted.md) |
| การนับพื้นที่ | [0116 ลงเวลาแล้ว](facility-0116-overview-entry-count-persists-within-shift.md), [0118 ส่งครบ](facility-0118-overview-cleaning-completion-counts-whole-areas.md), [0119 ตรวจครบ](facility-0119-overview-inspection-count-measures-coverage.md), [0120 ไม่นับรอบอนาคต](facility-0120-overview-completion-excludes-future-rounds.md), [0122 กลุ่มซ้ำได้](facility-0122-overview-follow-up-area-groups-can-overlap.md), [0123 แยกทำแทน](facility-0123-overview-separates-entry-cover-and-unassigned.md), [0125 ส่งแก้ต้องตรวจใหม่](facility-0125-overview-inspection-completion-tracks-latest-submission.md) |
| ประวัติและเป้าหมาย | [0117 รอบที่พลาด](facility-0117-overview-preserves-missed-rounds-within-shift.md), [0121 วันและกะ](facility-0121-overview-selects-date-and-shift.md), [0126 ช่วงรายละเอียด](facility-0126-overview-history-uses-retained-detail-window.md), [0127 เป้าเก่าไม่เปลี่ยน](facility-0127-overview-history-preserves-original-shift-target.md) |
| แนวทางเทคนิคที่ยอมรับสำหรับแผน | [0128 ประวัติเป้าใน transaction](facility-0128-overview-target-configuration-revisions.md), [0129 เก็บ origin รอบ](facility-0129-overview-preserves-round-origin-identity.md), [0130 read model และโหมดกดต่อ](facility-0130-overview-shares-scoped-read-model.md) |

## ตารางฐานระบบเดิม

| เรื่อง | อ่าน ADR | สถานะ/กติกาปัจจุบันโดยย่อ |
|---|---|---|
| โครงสร้างและ data model | [0004](facility-0004-app-architecture.md), [0025 Frontend Target Tree](facility-0025-frontend-target-tree.md), [0040](facility-0040-area-is-the-unit-of-assignment.md), [0045](facility-0045-admin-manages-areas-and-captures-sign-location.md) | 4 backend projects; endpoint + rules + EF; frontend pages/shared/store ดู implementation note ของ 0004 |
| เส้นทางหน้าเว็บ | [0026 Frontend Router Shape](facility-0026-frontend-router-shape.md), [0067](facility-0067-api-serves-frontend-from-one-origin.md) | เส้นทางตามบทบาทและการกลับหน้าเดิมหลังเข้าสู่ระบบ ป้าย QR ใช้ URL ของหน้าที่เกี่ยวข้อง |
| สถานะจุดและการตรวจ | [0046](facility-0046-issue-is-a-tag-not-a-card-status.md), [0047](facility-0047-cleaning-rounds-are-time-windows.md), [0048](facility-0048-supervisor-assigned-to-building.md) | Issue เป็นป้ายแยก; รอบเป็นช่วงเวลา; ส่ง → ตรวจ → แก้ → ตรวจซ้ำ |
| การลงเวลาและทำแทน | [0026 Shift Check-In](facility-0026-shift-check-in-two-shifts-strict-presence.md), [0031](facility-0031-four-stage-shift-attendance-lifecycle.md), [0041](facility-0041-scan-only-own-area-with-cover-assignment.md), [0050](facility-0050-check-in-confirms-area-presence-not-time-attendance.md), [0051](facility-0051-admin-corrections-and-audit-log.md), [0069](facility-0069-attendance-scan-window-60-minutes-around-the-shift.md), [0075](facility-0075-covers-assigned-up-to-7-days-ahead.md) | 4 จังหวะ ยืนยันคนดูแล Area; ไม่คิดค่าจ้าง; มีเหตุผลเมื่อ Admin แก้เวลา |
| การเข้าระบบและ session | [0012](facility-0012-short-access-token-with-refresh.md), [0013](facility-0013-refresh-token-httponly-cookie.md), [0014](facility-0014-refresh-tokens-stored-server-side.md), [0015](facility-0015-refresh-token-reuse-detection.md), [0016](facility-0016-refresh-token-sliding-30-days.md), [0054](facility-0054-login-with-employee-id-and-phone.md), [0089](facility-0089-admin-password-15-characters-and-blocklist.md), [0104](facility-0104-login-lock-per-login-name-and-ip.md) | JWT 5 นาที + refresh cookie 30 วัน; Login Lock ตามชื่อ + IP และเพดานต่อ IP |
| ปิดบัญชีและข่าวสด | [0052](facility-0052-cleaner-my-work-page-and-resend.md), [0057](facility-0057-supervisor-building-page-and-audit-log-page.md), [0058](facility-0058-dashboard-is-admin-only.md), [0105](facility-0105-deactivation-takes-effect-at-once-on-hub-and-api.md) | Dashboard Admin-only; มือถือได้เฉพาะขอบเขตตน; อ่าน active recipients ตอนส่ง ไม่มี admins group |
| การเชื่อมต่อหลุด | [0061](facility-0061-dashboard-keeps-30s-poll-beside-push.md), [0064](facility-0064-dashboard-hub-offline-keeps-todays-behaviour.md), [0107](facility-0107-admin-pages-notice-a-lost-link-sooner.md) | Admin poll 30 s; browser offline ทันที; server timeout Admin 10 s / phone 30 s; ping 5 s |
| GPS และป้าย | [0007](facility-0007-scan-flow-native-camera.md), [0035](facility-0035-per-building-check-in-gps-flag-not-block.md), [0036](facility-0036-check-in-requires-location-permission.md), [0037](facility-0037-gps-for-every-scan-replaces-nfc.md), [0038](facility-0038-default-radius-50m-per-sign.md), [0059](facility-0059-qr-token-only-from-the-sign.md), [0060](facility-0060-admin-gets-qr-token-from-points-page-only.md) | อ่าน GPS ทุกการสแกน; ไกลป้ายติดธง ไม่บล็อก; เปิดหน้าจากป้าย; QR ไม่อยู่บน Dashboard |
| Pilot / deploy / กู้ Admin | [0043](facility-0043-pilot-scope-one-area.md), [0049](facility-0049-server-reachable-from-internet-over-https.md), [0067](facility-0067-api-serves-frontend-from-one-origin.md), [0092](facility-0092-first-admin-from-server-settings.md), [0093](facility-0093-safe-start-and-deploy-guide.md), [0106](facility-0106-security-headers-set-by-the-app.md) | 1 Area; fixed public HTTPS; API เสิร์ฟ frontend; server/domain/OS ยังรอ |
| ข้อมูลและ Excel | [0053](facility-0053-excel-export-instead-of-report-pages.md), [0055](facility-0055-data-retention-and-pdpa-before-production.md), [0056](facility-0056-accounts-created-one-by-one-excel-import-before-rollout.md), [0100](facility-0100-admin-pages-follow-approved-design.md) | export ทำแล้วและเลือกหลายตึกได้; import พักไว้; retention มี implementation ใน branch นี้ (ดู handoff สำหรับผลตรวจ/สถานะเผยแพร่); PDPA notice ถอนแล้ว |
| หน้าตาและฟอร์ม | [0063](facility-0063-frontend-css-global-tokens-per-page-files.md), [0086](facility-0086-frontend-styling-with-tailwind.md), [0087](facility-0087-new-look-readable-at-the-sign.md), [0088](facility-0088-icons-from-lucide-react-beside-text.md), [0100](facility-0100-admin-pages-follow-approved-design.md), [0101](facility-0101-admin-forms-and-confirmations-follow-approved-design.md) | 0086 แทน CSS ต่อหน้า; Tailwind 4 / Enterprise Light; nested forms/confirmations ตามแบบที่อนุมัติ |
| Server Log | [0095](facility-0095-logging-scope-security-events-and-errors.md), [0096](facility-0096-server-log-read-from-journal-only.md), [0097](facility-0097-server-log-names-account-by-employee-id-never-typed-text.md), [0098](facility-0098-server-log-kept-90-days.md), [0099](facility-0099-server-log-in-its-own-journal-namespace.md), [0103](facility-0103-server-log-written-through-serilog.md), [0104](facility-0104-login-lock-per-login-name-and-ip.md) | ILogger ผ่าน Serilog Console → journal namespace facility; 90 วัน; เพิ่ม normalized IP ใน login logs |
| E2E | [0094](facility-0094-playwright-end-to-end.md), [0102](facility-0102-headed-e2e-datasets.md), [0107](facility-0107-admin-pages-notice-a-lost-link-sooner.md) | unique owned DB/user ต่อรอบ; private publish; Chromium/WebKit/Firefox ตามคู่มือ E2E |

## เลข ADR ที่ใช้ซ้ำ

เลข `facility-0025` และ `facility-0026` มีอย่างละสองเอกสาร เวลาอ้างให้ลิงก์ด้วยชื่อไฟล์เต็มและระบุหัวข้อ

| เลข | เอกสาร | อ่านประกอบ |
|---|---|---|
| 0025 | [Frontend Target Tree](facility-0025-frontend-target-tree.md) | โครง `pages/`, `shared/`, `store/` |
| 0025 | [Scan Presence Proof — NFC Tap Link](facility-0025-scan-presence-nfc-one-time-tap-link.md) | การตัดสินใจเดิมที่ถูกแทนที่ด้วย [0037](facility-0037-gps-for-every-scan-replaces-nfc.md) |
| 0026 | [Frontend Router Shape](facility-0026-frontend-router-shape.md) | ตาราง route, guard และการกลับหน้าเดิมหลัง login |
| 0026 | [Shift Check-In — Two Shifts](facility-0026-shift-check-in-two-shifts-strict-presence.md) | อ่านร่วมกับ [0031](facility-0031-four-stage-shift-attendance-lifecycle.md), [0040](facility-0040-area-is-the-unit-of-assignment.md) และ [0050](facility-0050-check-in-confirms-area-presence-not-time-attendance.md) สำหรับกติกาลงเวลาปัจจุบัน |

## จุดที่อ่านเอกสารเก่าแล้วอาจเข้าใจผิด

- 0003 Wi-Fi/HTTP ถูก 0049 แทนสำหรับระบบจริง; ใช้ localhost/Vite ได้ตอนพัฒนา
- 0005 และ 0042 ต้องอ่านต่อถึง 0046–0047: ไม่ใช้ countdown interval และ Issue ไม่ใช่สถานะหลักของการ์ดแล้ว
- 0033 pilot ตึก A / 12 จุดถูก 0043 แทนด้วย 1 Area / อย่างน้อย 1 จุด
- 0063 CSS ต่อหน้าถูก 0086 แทน; UI ล่าสุดคือ Enterprise Light amendment ของ 0087 และ 0100–0101
- 0054 การล็อกทั้งบัญชีถูก 0104 แก้เป็นชื่อ + IP; 0012 การรอ deactivation ครบ 5 นาทีถูก 0105 แก้ ส่วนเปลี่ยนเบอร์/รีเซ็ตรหัสผ่านยังรอ access token ได้ถึง 5 นาที
- 0055 ถอนเฉพาะงานประกาศ/ใบยินยอมและคุย HR ก่อน production; retention และคำถามเรื่องไฟล์ Excel ยังคงอยู่
- 0094 fixed E2E database/shared wwwroot ถูก 0102 แก้เป็นทรัพยากรที่สร้างและพิสูจน์เจ้าของแยกต่อรอบ

รายละเอียดสถานะงานและผลตรวจล่าสุดอยู่ใน [handoff](../handoff.md) กติกาธุรกิจภาษาไทยอยู่ใน [business overview](../business-overview.md) โค้ดปัจจุบันอยู่ใน [code map](../code-map.md)
