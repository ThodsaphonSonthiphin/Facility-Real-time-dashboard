### UI Prototype: Pagination หน้า Dashboard (dashboard-pagination)

เพิ่มการแบ่งหน้าลงใน Prototype เดิมของหน้า Dashboard:

- **Prototype File**: [`docs/decision-map/facility-poc/mockups/dashboard-prototype.html`](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/blob/master/docs/decision-map/facility-poc/mockups/dashboard-prototype.html)
- **ข้อมูลจำลอง**: 26 จุดบริการ (3 หน้าที่ 12 ต่อหน้า)
- **แถบเลขหน้า**: อยู่ใต้ grid ใช้ร่วมกันทั้ง Cards Grid และ Table View — "แสดง 1–12 จาก 26 จุด", ก่อนหน้า/ถัดไป, เลือก 12/24/48 ต่อหน้า
- **เรียงก่อนแบ่งหน้า**: 🔴 Issue > 🟠 Overdue > 🟢 Normal ใช้กับทุกจุด จุดเร่งด่วนอยู่หน้า 1 เสมอ และเลขหน้าที่มี Issue/Overdue จะมีจุดสี
- **Real-time**: ปุ่ม "จำลองจุดหน้าสุดท้ายพบปัญหา" — อยู่หน้าเดิม แล้วขึ้น toast "ย้ายไปอยู่หน้า 1" พร้อมปุ่มไปดู
