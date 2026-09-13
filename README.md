# Facility Real-time Dashboard (POC)

ระบบติดตามสถานะการทำความสะอาดและสุขอนามัยแบบเรียลไทม์ (Facility Real-time Dashboard) พัฒนาขึ้นเพื่อทดสอบแนวคิด (Proof of Concept) ก่อนเริ่มฝึกงานจริง

## 🏗️ สถาปัตยกรรมระบบ (Architecture)

- **Backend**: .NET 10 LTS (C# 13) Minimal APIs + SignalR Hub (`FacilityRealtime.Api`)
- **Database**: MySQL 8+ (EF Core `MySql.EntityFrameworkCore 10.x`) — API apply migration เองตอนเริ่ม
- **Frontend**: Vite + React 19 + TypeScript + Vanilla CSS (`frontend/`)
- **Real-time Pipeline**: เมื่อแม่บ้านสแกนและบันทึกข้อมูล -> Backend บันทึกลง MySQL -> ส่งผ่าน SignalR Hub (`/hubs/scan`) -> หน้า Dashboard อัปเดตการ์ดและ KPI ทันทีโดยไม่ต้องรีเฟรช
- **Authentication**: login ได้ access token (JWT อายุ 5 นาที) ที่หน้าเว็บเก็บไว้ในหน่วยความจำ และ refresh token ใน HttpOnly cookie ที่หมุนใบใหม่ทุกครั้ง (ADR facility-0011 ถึง facility-0016) หน้า Dashboard, หน้าสแกน และ SignalR hub ต้อง login ก่อน (facility-0017, facility-0018)

---

## 🚀 วิธีการรันระบบ (How to Run)

### 1. ฐานข้อมูล (MySQL)
ตรวจสอบให้แน่ใจว่า MySQL กำลังทำงาน:
```bash
brew services start mysql
```

**ทำครั้งเดียว ถ้าเครื่องนี้เคยรันเวอร์ชันเฟส 1:** เฟส 1 สร้างตารางด้วย `EnsureCreated` ซึ่งไม่มีประวัติ migration ตอนนี้ API สั่ง migrate เองตอนเริ่ม จึงต้องล้างฐานข้อมูลเดิมหนึ่งครั้ง (ข้อมูลสแกนทดสอบหาย แล้ว API seed ข้อมูลตัวอย่างให้ใหม่):
```bash
mysql -u root -e "DROP DATABASE IF EXISTS facility_dashboard; CREATE DATABASE facility_dashboard CHARACTER SET utf8mb4;"
```

### 2. ตั้ง JWT signing key (ครั้งเดียวต่อเครื่อง)
repo นี้เป็น public จึงไม่มี key อยู่ในไฟล์ config และ API จะไม่ยอมเริ่มถ้ายังไม่ได้ตั้ง:
```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/FacilityRealtime.Api
```

### 3. รัน Backend API (.NET 10)
รัน Backend บน Port `5001` (หลีกเลี่ยง Port 5000 ของ AirPlay บน macOS):
```bash
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

### 4. รัน Frontend (Vite React)
```bash
cd frontend
npm install
npm run dev -- --host 0.0.0.0 --port 5173
```

### 5. รันชุดทดสอบ
ชุดทดสอบ API ใช้ SQLite ในหน่วยความจำ ไม่ต้องเปิด MySQL:
```bash
dotnet test backend/FacilityRealtime.slnx
npm --prefix frontend test
```

---

## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง

- **หน้าจอ Dashboard (สำหรับเปิดบนคอมพิวเตอร์/แท็บเล็ต)**:
  - `http://localhost:5173/dashboard` — ต้อง login ด้วยบัญชีใดก็ได้ แล้วเปิดค้างไว้ได้
- **หน้าสแกนทำความสะอาดสำหรับแม่บ้าน (สำหรับเปิดบนมือถือผ่าน WiFi วงเดียวกัน)**:
  - `http://<MAC_LAN_IP>:5173/scan/token-restroom-m1` (เช่น `http://10.249.194.205:5173/scan/token-restroom-m1`)
  - login ครั้งแรกบนมือถือแล้วระบบจำไว้ จนกด "ออก" หรือไม่ได้ใช้เลย 30 วัน

### บัญชีผู้ใช้ทดสอบ (Seed Data)
- **แม่บ้าน**: username: `somchai` / password: `password123`
- **ผู้ดูแลระบบ**: username: `admin` / password: `admin1234`

### หมายเหตุด้านความปลอดภัยของ POC
- cookie ของ refresh token ไม่ได้ตั้ง `Secure` เพราะมือถือเข้าผ่าน HTTP ในวง WiFi (ADR facility-0013) ถ้าติดตั้งบน HTTPS ให้ตั้งค่า `Jwt:RefreshCookieSecure` เป็น `true`
- บัญชีทดสอบข้างบนเป็นข้อมูลตัวอย่างเท่านั้น
