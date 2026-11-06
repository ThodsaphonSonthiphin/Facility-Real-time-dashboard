# ออกแบบระบบ: Wireframe, Prototype และฐานข้อมูล

เอกสารออกแบบเดิมเก็บไว้เป็นประวัติของการพัฒนา ปัจจุบันใช้ Enterprise Light ตาม facility-0087 (amendment 2026-10-29), 0100 และ 0101 ดู [DESIGN_SPEC](enterprise-light/DESIGN_SPEC.md), [MASTER](../../design-system/janitorial/MASTER.md) และ [คู่มืออ่าน ADR](../adr/README.md) หากแบบเดิมขัดกับ ADR ให้ถือการตัดสินใจและ amendment ล่าสุดเป็นหลัก สถานะงานอยู่ใน [handoff](../handoff.md)

| ไฟล์ | คืออะไร | เปิดดู |
|---|---|---|
| [wireframe/](wireframe/) | Wireframe 30 หน้า: มือถือ M0–M17 (แม่บ้าน หัวหน้า และ Admin เก็บพิกัด) และคอมพิวเตอร์ D0–D11 (Dashboard ของ Admin) | [Artifact](https://claude.ai/artifact/FACV3wuV2aN19YduyhxVnW) |
| [prototype.html](prototype.html) | Prototype ที่กดได้จริง: มือถือด้านซ้าย Dashboard ของ Admin ด้านขวา นาฬิกาจำลองด้านบน กฎของ ADR 0041, 0047, 0048, 0050 ทำงานจริง | เปิดไฟล์ในเบราว์เซอร์ได้เลย หรือ [Artifact](https://claude.ai/artifact/2GFyPGKAhEf8x2EbG9G69G) |
| [database.html](database.html) | แบบฐานข้อมูลเดิม 15 ตาราง (โค้ดปัจจุบันมี 14 ตาราง; งาน retention ยังไม่สร้าง): แผนภาพ ER, การจัดงานเข้ารอบ, กติกาที่ฐานข้อมูลกันหรือแอปกัน, งานลบข้อมูล และตารางเดิมเปลี่ยนเป็นอะไร | เปิดไฟล์ในเบราว์เซอร์ได้เลย หรือ [Artifact](https://claude.ai/artifact/BJsETTv17ZgPVhM1dn3KwM) |

## หมายเหตุ

- ไฟล์ใน `wireframe/` เป็นต้นฉบับของเครื่องมือ Design บน claude.ai (`canvas.json` บอกตำแหน่งและชื่อของแต่ละหน้า) ต้องเปิดผ่านลิงก์ Artifact จึงจะเห็นเป็นหน้าจอ เพราะไฟล์ใช้ `support.js` ของเครื่องมือนั้น
- ชื่อคน รหัสพนักงาน และเบอร์โทรทั้งหมดเป็นตัวอย่าง เบอร์โทรใช้ `00000000xx` ซึ่งไม่ใช่เบอร์จริง ข้อมูลจริงกรอกบนเซิร์ฟเวอร์เท่านั้น (ADR 0056)
- Prototype เก็บข้อมูลไว้ในหน้าอย่างเดียว รีเฟรชแล้วกลับไปเริ่มต้นใหม่
- ช่วงลงเวลาได้รับการตัดสินและสร้างแล้ว: 60 นาทีก่อนกะเริ่มถึง 60 นาทีหลังกะจบ (facility-0069) ค่าจำลอง `CFG.checkinGrace` ใน prototype ไม่ใช่สิ่งที่ยังรอยืนยัน
- ทางเลือกเชิงเทคนิคที่ ADR ไม่ได้ระบุ อยู่ในหัวข้อ "สิ่งที่ตัดสินตอนออกแบบ" ของ `database.html`
