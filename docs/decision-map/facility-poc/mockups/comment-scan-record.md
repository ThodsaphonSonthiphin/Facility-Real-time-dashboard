### UI Mockup การสแกน QR และผลกระทบต่อ Real-time Dashboard

ได้จัดทำหน้าจอ Mockup แบบ Interactive สำหรับเปรียบเทียบทั้ง 3 แนวทางการบันทึกข้อมูล (`scan-record`):

- **Mockup File**: [`docs/decision-map/facility-poc/mockups/scan-record-mockup.html`](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/blob/master/docs/decision-map/facility-poc/mockups/scan-record-mockup.html)
- **แนวทาง A (Stamp)**: สแกนเพื่อ Check-in บันทึกเฉพาะ timestamp + รหัสจุด
- **แนวทาง B (Status & Note - แนะนำ)**: สแกนแล้วเลือก ปกติ (1-tap ส่งได้ทันที) หรือสลับเป็น แจ้งปัญหาด่วน (ขยะล้น, ชำรุด, น้ำรั่ว) เพื่อให้ Dashboard เตือนแบบ Real-time
- **แนวทาง C (Checklist)**: รายการตรวจละเอียดตามประเภทจุด (เช่น แคนทีน vs ทำความสะอาดทั่วไป)
