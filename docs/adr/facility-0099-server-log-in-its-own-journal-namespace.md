# Server Log อยู่ใน journal แยกของระบบ (namespace facility)

```mermaid
flowchart TD
    Q{"การเก็บ 90 วันตั้งที่ระบบเรา หรือทั้งเครื่อง"} -->|chosen| A["A: journal แยก LogNamespace=facility<br/>ตั้ง 90 วันในไฟล์ของ namespace นี้<br/>อ่านด้วย journalctl --namespace=facility -u facility"]
    Q -->|rejected| B["B: แก้ /etc/systemd/journald.conf ของเครื่อง<br/>log ของทุกโปรแกรมบนเครื่องถูกลบใน 90 วันด้วย"]
```

- วันที่: 2026-10-29
- ที่มา: เจ้าของเลือก "A" ในรอบ grill วันที่ 2026-10-29
- เกี่ยวข้อง: facility-0098 (เก็บ 90 วัน), facility-0096 (อ่านจาก journal), facility-0093 (คู่มือ deploy, ยังไม่ได้เลือกระบบปฏิบัติการ)

## Context & Decision

เครื่องที่ IT ให้อาจรันโปรแกรมอื่นด้วย การแก้ค่า journal ของทั้งเครื่องจะลบ log ของโปรแกรมเหล่านั้นตามไปด้วย จึงตัดสินใจให้ service ของระบบเขียนลง **journal namespace ของตัวเอง** (`LogNamespace=facility` ในไฟล์ service) และตั้งการเก็บ 90 วันในไฟล์ค่าของ namespace นั้น ค่าของเครื่องไม่ถูกแตะ

## Consequences

- คำสั่งอ่าน log ยาวขึ้น: `sudo journalctl --namespace=facility -u facility` คู่มือ deploy ที่ใช้ `journalctl -u facility` อยู่ต้องแก้ตาม
- ต้องใช้ systemd ที่มี `LogNamespace=` (Linux รุ่นใหม่ทั่วไปมีแล้ว)
- ถ้า IT เลือก Windows ต้องกลับมาตัดสินเรื่องที่เก็บและระยะเก็บ (facility-0096, 0098, ADR นี้) ใหม่ เพราะ Windows ไม่มี journal
