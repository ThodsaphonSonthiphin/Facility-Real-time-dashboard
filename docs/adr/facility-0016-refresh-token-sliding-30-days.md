# Refresh Token Lifetime: Sliding 30 Days

```mermaid
flowchart TD
    Q{"refresh token อายุเท่าไหร่"} -->|chosen| A["6a sliding 30 วัน: ไม่ได้ใช้เลย 30 วันต้อง login ใหม่ ใช้ทุกวันค้างไปจนกด Logout"]
    Q -->|rejected| B["6b sliding 30 วัน + เพดาน 90 วัน: บังคับ login ใหม่ทุก 90 วันแม้ใช้ทุกวัน เกินจำเป็นสำหรับ POC"]
    Q -->|rejected| C["6c 7 วันตายตัว: ต้อง login ใหม่ทุกสัปดาห์ ขัดกับ Persistent Session (facility-0002)"]
```

- Decision map: [#15 สิทธิ์ Admin](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/15)
- วันที่: 2026-09-13
- เกี่ยวข้อง: facility-0002 (Persistent Session), facility-0014 (Rotation), facility-0015 (Reuse Detection)

## Context & Decision

Persistent Session (facility-0002) ให้ผู้สแกน login ค้างได้จนกด Logout จึงตัดสินใจให้ refresh token มีอายุ **30 วันนับจากครั้งล่าสุดที่ใช้** refresh แต่ละครั้งออกใบใหม่ (facility-0014) ที่นับอายุใหม่อีก 30 วัน ถ้าใช้ทุกวันจะค้างไปเรื่อยๆ ถ้าไม่ได้เปิดเลย 30 วันต้อง login ใหม่ ไม่มีเพดานอายุรวม
