### UI Prototype: Flow การสแกนบนมือถือ (scan-flow)

ได้จัดทำหน้าจอ Prototype จำลองเปรียบเทียบทั้ง 2 รูปแบบ:

- **Prototype File**: [`docs/decision-map/facility-poc/mockups/scan-flow-prototype.html`](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/blob/master/docs/decision-map/facility-poc/mockups/scan-flow-prototype.html)
- **Flow 1 (Native Camera - แนะนำ)**:
  - ใช้กล้องมือถือ/LINE สแกนป้าย QR แล้วแตะเปิดลิงก์เบราว์เซอร์ -> กดยืนยันทำความสะอาด (แตะรวม 2 ครั้ง)
  - รองรับ HTTP บนวง WiFi เดียวกันได้ทันที 100% โดยไม่ต้องติดตั้ง SSL Certificate (mkcert) บนมือถือส่วนตัวของแม่บ้าน
- **Flow 2 (In-Web Camera)**:
  - เปิดเว็บก่อน -> กดปุ่มเปิดกล้องในเว็บ -> ส่อง QR -> กดยืนยัน (แตะรวม 4 ขั้นตอน)
  - มีข้อจำกัด: Browser บังคับ Secure Context (HTTPS) ทำให้ต้องลง Root CA Certificate บนมือถือแม่บ้านทุกเครื่อง
