### UI Prototype: หน้า Real-time Dashboard (dashboard-prototype)

ได้จัดทำหน้าจอ Prototype จำลองเปรียบเทียบการจัดวางของ Dashboard ผู้ดูแล:

- **Prototype File**: [`docs/decision-map/facility-poc/mockups/dashboard-prototype.html`](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/blob/master/docs/decision-map/facility-poc/mockups/dashboard-prototype.html)
- **การจัดวางหลัก (Layout Option A - Cards Grid - แนะนำ)**:
  - ด้านบน: **KPI Summary Bar** สรุปยอดรวม แยกตามสีสถานะ (แดง Issue, ส้ม Overdue, เขียว Normal) พร้อมคลิกเพื่อ Filter ได้ทันที
  - กลาง: **Service Point Cards Grid** (Responsive 3–4 คอลัมน์) แต่ละการ์ดมีขอบสีตามสถานะเด่นชัด แสดงเวลาสแกนล่าสุด ผู้สแกน เวลาครบรอบถัดไป และหากเป็น Issue จะแสดงกล่องแท็กปัญหาด่วนสีแดงชัดเจน
  - เรียงลำดับความสำคัญตาม ADR 0005: จุดที่เป็น 🔴 Issue ขึ้นก่อน > 🟠 Overdue > 🟢 Normal
- **ตัวเลือกเสริม (Option B - Table View)**: มีปุ่มสลับเป็นมุมมองตารางสำหรับดูข้อมูลแบบแถว
