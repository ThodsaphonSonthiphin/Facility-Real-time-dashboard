# Facility Real-time Dashboard (POC)

ระบบติดตามสถานะการทำความสะอาดและสุขอนามัยแบบเรียลไทม์ (Facility Real-time Dashboard) พัฒนาขึ้นเพื่อทดสอบแนวคิด (Proof of Concept) ก่อนเริ่มฝึกงานจริง

> **หมายเหตุสำหรับ branch นี้ (`feat/pilot-data-model`):** Backend ใช้โมเดลข้อมูลแบบ Pilot แล้ว แต่ Frontend ยังไม่ได้อัปเดต ตอนนี้จึง login จากหน้าเว็บได้เฉพาะ Admin เท่านั้น และหน้า Dashboard/หน้าสแกนจะแสดงสถานะที่ไม่รู้จัก จนกว่าแผน frontend จะเสร็จ

## 🏗️ สถาปัตยกรรมระบบ (Architecture)

- **Backend**: .NET 10 LTS (C# 13) Minimal APIs + SignalR Hub (`FacilityRealtime.Api`)
- **Database**: MySQL 8.0.16 ขึ้นไป (ต้องเป็นเวอร์ชันที่บังคับใช้ CHECK constraint) (EF Core `MySql.EntityFrameworkCore 10.x`) — API apply migration เองตอนเริ่ม
- **Frontend**: Vite + React 19 + TypeScript + Vanilla CSS (`frontend/`)
- **Real-time Pipeline**: เมื่อแม่บ้านสแกนและบันทึกข้อมูล -> Backend บันทึกลง MySQL -> ส่งผ่าน SignalR Hub (`/hubs/scan`) -> หน้า Dashboard อัปเดตการ์ดและ KPI ทันทีโดยไม่ต้องรีเฟรช
- **Authentication**: login ได้ access token (JWT อายุ 5 นาที) ที่หน้าเว็บเก็บไว้ในหน่วยความจำ และ refresh token ใน HttpOnly cookie ที่หมุนใบใหม่ทุกครั้ง (ADR facility-0011 ถึง facility-0016) หน้า Dashboard, หน้าสแกน และ SignalR hub ต้อง login ก่อน (facility-0017, facility-0018)

---

## 🚀 วิธีการรันระบบ (How to Run)

### 1. ฐานข้อมูล (MySQL)
ตรวจสอบให้แน่ใจว่า MySQL กำลังทำงาน (ต้องเป็น MySQL 8.0.16 or later — CHECK constraints are enforced):
```bash
brew services start mysql
```

**ทำครั้งเดียว ถ้าเครื่องนี้เคยรันเวอร์ชันใดก็ตามก่อนสคีมา Pilot (เฟส 1 ที่สร้างตารางด้วย `EnsureCreated` หรือเฟส 2 ที่มี migration เรื่อง auth/refresh token):** สคีมา Pilot เป็น migration ชุดใหม่ชุดเดียว (`PilotSchema`) ที่แทนที่ของเดิม ถ้าไม่ล้างฐานข้อมูลเดิม API จะเริ่มไม่ขึ้นและแจ้ง "table already exists" และอาจทิ้งฐานข้อมูลไว้ในสภาพ migrate ค้างครึ่งทาง จึงต้องล้างหนึ่งครั้ง (ข้อมูลสแกนทดสอบหาย แล้ว API seed ข้อมูลตัวอย่างให้ใหม่):
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
  - `http://localhost:5173/dashboard` — เฉพาะบัญชี Admin เท่านั้นที่เข้าได้ (facility-0058) แล้วเปิดค้างไว้ได้
- **หน้าสแกนทำความสะอาดสำหรับแม่บ้าน (สำหรับเปิดบนมือถือผ่าน WiFi วงเดียวกัน)**:
  - `http://<MAC_LAN_IP>:5173/scan/token-restroom-m1` (เช่น `http://10.249.194.205:5173/scan/token-restroom-m1`)
  - แม่บ้านจะ login ด้วยรหัสพนักงาน + เบอร์โทรเมื่อแผน frontend เปลี่ยนหน้า login แล้ว (ตอนนี้ login จากหน้าเว็บได้แค่ Admin) เมื่อ login ครั้งแรกบนมือถือแล้วระบบจำไว้ จนกด "ออก" หรือไม่ได้ใช้เลย 30 วัน

### บัญชีผู้ใช้ทดสอบ (Seed Data, ข้อมูลสมมุติทั้งหมด ใช้ได้เฉพาะตอนรันแบบ Development)
- **แม่บ้าน Area AR01 กะเช้า**: รหัสพนักงาน `E1001` / เบอร์โทร `0810000001`
- **แม่บ้าน Area AR01 กะดึก**: รหัสพนักงาน `E1002` / เบอร์โทร `0810000002`
- **แม่บ้าน Area AR02 (Office กะเช้าอย่างเดียว)**: รหัสพนักงาน `E1003` / เบอร์โทร `0810000003`
- **หัวหน้า ตึก A กะเช้า**: รหัสพนักงาน `S2001` / เบอร์โทร `0820000001`
- **ผู้ดูแลระบบ**: username `admin` / password `admin1234`

หน้า login ของ frontend ยังมีช่อง username/password จนกว่าแผน frontend จะเปลี่ยน ตอนนี้จึง login จากหน้าเว็บได้แค่ Admin

ลองใน Swagger (`/swagger`): แม่บ้านต้องลงเวลาเข้างานก่อนส่งงาน — `POST /api/attendance` ด้วย `qrToken` = `token-checkin-ar01`, `eventType` = `ShiftIn` และพิกัด เช่น `latitude` 13.7563, `longitude` 100.5018, `accuracyM` 10 (ป้ายของ AR01 มีพิกัดตัวอย่าง ป้ายของ AR02 ยังไม่มี) จากนั้นจึง `POST /api/scan-records` ได้ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะถึง 60 นาทีหลังกะ (ADR 0069)

### หมายเหตุด้านความปลอดภัยของ POC
- cookie ของ refresh token ไม่ได้ตั้ง `Secure` เพราะมือถือเข้าผ่าน HTTP ในวง WiFi (ADR facility-0013) ถ้าติดตั้งบน HTTPS ให้ตั้งค่า `Jwt:RefreshCookieSecure` เป็น `true`
- บัญชีทดสอบข้างบนเป็นข้อมูลตัวอย่างเท่านั้น
