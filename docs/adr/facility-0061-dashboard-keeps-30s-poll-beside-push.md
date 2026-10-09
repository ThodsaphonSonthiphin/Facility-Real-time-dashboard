# Dashboard Keeps the 30 Second Poll Beside Push — RTK Query pollingInterval

```mermaid
flowchart TD
    Q{"เมื่อ SignalR แพตช์ cache ของ getServicePoints ได้แล้ว (#26)<br/>Dashboard ยังต้องดึง snapshot ใหม่ทุก 30 วินาทีไหม"} -->|chosen| A["A: คง 30 วินาที ย้ายเข้า RTK Query<br/>pollingInterval: 30_000 บน hook ของ Dashboard<br/>refetchOnFocus แทน visibilitychange ที่เขียนเอง"]
    Q -->|rejected| B["B: ลดเป็น 60 วินาที:<br/>การ์ดเปลี่ยนสีตามเวลาช้าลงเป็นนาที เพื่อประหยัด request ที่มาจากจอ Admin ไม่กี่จอ ไม่คุ้ม"]
    Q -->|rejected| C["C: poll เฉพาะตอน hub หลุด:<br/>ตอน hub ต่ออยู่ ไม่มี event ไหนบอกว่าเลยรอบ การ์ดจะไม่เปลี่ยนสีจนกว่ามีคนสแกน"]
    Q -->|rejected| D["D: ให้ browser คำนวณสถานะตามเวลาเอง:<br/>กติกาสถานะมีสองที่ ซึ่ง facility-0019 ปฏิเสธไว้แล้ว"]
    Q -->|rejected| E["E: server ตั้ง timer แล้ว push เมื่อจุดเปลี่ยนสถานะ:<br/>เป็นงาน backend ซึ่ง milestone frontend-restructure กำหนดให้คงไว้ตามเดิม"]
```

- Decision map: [#27 polling-vs-push](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/27) บนแผนที่ [#1](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/1) (milestone frontend-restructure)
- วันที่: 2026-10-08
- ที่มา: เจ้าของโปรเจกต์เลือกข้อ A
- เกี่ยวข้อง: facility-0019 (กติกาสถานะอยู่ที่ server ที่เดียว, ดึงซ้ำทุก 30 วินาที), facility-0022 (ตำแหน่งของ `pollingInterval` ยกมาที่ #27), facility-0024 (server data อยู่ใน RTK Query cache), facility-0058 (Dashboard เฉพาะ Admin)

## Context & Decision

SignalR บอกได้แค่ว่า "มีคนสแกน" แต่การเปลี่ยนสถานะตามเวลา เช่น จุดที่ไม่มีใครสแกนจนเลยรอบ ไม่มี event ใดส่งมา ตาม facility-0019 สถานะคำนวณที่ server ที่เดียว และ Dashboard เห็นการเปลี่ยนนั้นจากการดึง snapshot ใหม่ทุก 30 วินาที (`setInterval` ใน `useDashboard.ts` บน `50a8d55`)

จึงตัดสินใจ **คงการดึงทุก 30 วินาทีไว้ข้าง push** และย้ายเข้า RTK Query:

1. Dashboard เรียก `useGetServicePointsQuery(undefined, { pollingInterval: 30_000, refetchOnFocus: true })` แทน `setInterval` และ `visibilitychange` ที่เขียนเอง
2. `refetchOnFocus` ต้องมี `setupListeners(store.dispatch)` ใน store
3. การแพตช์ผ่าน `updateCachedData` จาก hub (#26) ยังเป็นทางหลักของการสแกน poll เป็นตาข่ายสำหรับการเปลี่ยนตามเวลาและการแพตช์ที่หลุดไป
4. poll อยู่ที่ hook ของหน้า Dashboard ไม่ใช่ที่ endpoint หน้าอื่นที่ใช้ `getServicePoints` จะไม่ poll ตาม

## Consequences

- การ์ดเปลี่ยนสีตามเวลาภายใน 30 วินาทีหลังถึงเวลา เหมือนวันนี้
- request ทุก 30 วินาทีมาจากจอ Admin เท่านั้น เพราะ Dashboard เป็นของ Admin (facility-0058) มือถือแม่บ้านไม่ poll
- `refetchOnReconnect` และสิ่งที่ Dashboard แสดงตอน hub หลุด ยังไม่ตัดสินในนี้ — เป็นของ #28 hub-reconnect-catchup
