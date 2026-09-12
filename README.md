# Facility Real-time Dashboard (POC)

ระบบติดตามสถานะการทำความสะอาดและสุขอนามัยแบบเรียลไทม์ (Facility Real-time Dashboard) พัฒนาขึ้นเพื่อทดสอบแนวคิด (Proof of Concept) ก่อนเริ่มฝึกงานจริง

## 🏗️ สถาปัตยกรรมระบบ (Architecture)

- **Backend**: .NET 10 LTS (C# 13) Minimal APIs + SignalR Hub (`FacilityRealtime.Api`)
- **Database**: MySQL 8+ (EF Core `MySql.EntityFrameworkCore 10.x`)
- **Frontend**: Vite + React 19 + TypeScript + Vanilla CSS (`frontend/`)
- **Real-time Pipeline**: เมื่อแม่บ้านสแกนและบันทึกข้อมูล -> Backend บันทึกลง MySQL -> ส่งผ่าน SignalR Hub (`/hubs/scan`) -> หน้า Dashboard อัปเดตการ์ดและ KPI ทันทีโดยไม่ต้องรีเฟรช

---

## 🚀 วิธีการรันระบบ (How to Run)

### 1. ฐานข้อมูล (MySQL)
ตรวจสอบให้แน่ใจว่า MySQL กำลังทำงาน:
```bash
brew services start mysql
```

### 2. รัน Backend API (.NET 10)
รัน Backend บน Port `5001` (หลีกเลี่ยง Port 5000 ของ AirPlay บน macOS):
```bash
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

### 3. รัน Frontend (Vite React)
```bash
cd frontend
npm install
npm run dev -- --host 0.0.0.0 --port 5173
```

---

## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง

- **หน้าจอ Dashboard (สำหรับเปิดบนคอมพิวเตอร์/แท็บเล็ต)**:
  - `http://localhost:5173/dashboard`
- **หน้าสแกนทำความสะอาดสำหรับแม่บ้าน (สำหรับเปิดบนมือถือผ่าน WiFi วงเดียวกัน)**:
  - `http://<MAC_LAN_IP>:5173/scan/token-restroom-m1` (เช่น `http://10.249.194.205:5173/scan/token-restroom-m1`)

### บัญชีผู้ใช้ทดสอบ (Seed Data)
- **แม่บ้าน**: username: `somchai` / password: `password123`
- **ผู้ดูแลระบบ**: username: `admin` / password: `admin1234`
