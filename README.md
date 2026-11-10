# Janitorial Management Dashboard (POC)

เว็บติดตามงานทำความสะอาดในโรงงาน แม่บ้านสแกน QR ลงเวลาก่อนเริ่มงาน แล้วสแกน QR ที่ห้องเมื่อทำความสะอาดเสร็จ หัวหน้าใช้ QR ที่ห้องเดียวกันเพื่อตรวจและแจ้งสิ่งที่ต้องแก้ แอดมินดูได้ว่าพื้นที่ไหนยังไม่มีคน งานไหนเลยเวลา และงานไหนรอตรวจ

**สถานะ ณ 10 พ.ย. 2026** พัฒนาตามขอบเขตที่ตกลงเสร็จแล้ว รวมถึงงานลบและสรุปข้อมูลเก่าอัตโนมัติที่รวมเข้าโครงการแล้วใน PR #147 ขั้นต่อไปคือให้ทีมรับช่วงติดตั้งบนเครื่องจริงและทดลองใช้ในพื้นที่เดียว การนำเข้าจาก Excel เป็นงานเสริม ตอนนี้เพิ่มข้อมูลผ่านหน้าแอดมินได้

## เริ่มอ่านตามหน้าที่

| ถ้าคุณต้องการ | เปิดเอกสาร |
|---|---|
| พี่เลี้ยงหรือผู้ประสานงานต้องการทราบว่าระบบช่วยอะไรและงานถึงไหนแล้ว | [ภาพรวมระบบ](docs/business-overview.md) และ [สถานะส่งมอบ](docs/handoff.md) |
| แม่บ้าน หัวหน้า หรือแอดมินต้องการดูว่าต้องทำอะไรในแต่ละขั้น | [Flowchart การทำงานทั้งสามบทบาท](docs/role-workflows.md) |
| ทีม IT ต้องติดตั้ง ดูแล หรือกู้บัญชีแอดมิน | [คู่มือส่งมอบและติดตั้ง](docs/handover.md) |
| ทีมพัฒนาต้องการดูโครงสร้างหรือเหตุผลเบื้องหลังกติกา | [สถาปัตยกรรม](docs/architecture.md) และ [บันทึกการตัดสินใจ (ADR)](docs/adr/README.md) |

## 🏗️ สถาปัตยกรรมระบบ (Architecture)

- **Backend**: .NET 10 Minimal APIs + SignalR Hub (`JanitorialManagementDashboard.Api`)
- **Database**: MySQL 8.0.16 ขึ้นไป (ต้องเป็นเวอร์ชันที่บังคับใช้ CHECK constraint) (EF Core `MySql.EntityFrameworkCore 10.x`) — API apply migration เองตอนเริ่ม
- **Frontend**: Vite + React 19 + TypeScript, Redux Toolkit + RTK Query, React Router 7, Tailwind CSS 4 + Enterprise Light (`frontend/`, โครงตาม facility-0025, สไตล์ตาม facility-0086, 0087, 0100, 0101) ระบบจริง API เสิร์ฟหน้าเว็บเองจาก origin เดียว (facility-0067)
- **Real-time Pipeline**: เมื่อแม่บ้านสแกนและบันทึกข้อมูล -> Backend บันทึกลง MySQL -> ส่งผ่าน SignalR Hub (`/hubs/scan`) -> หน้า Dashboard อัปเดตการ์ดและ KPI ทันทีโดยไม่ต้องรีเฟรช
- **Authentication**: login ได้ access token (JWT อายุ 5 นาที) ที่หน้าเว็บเก็บไว้ในหน่วยความจำ และ refresh token ใน HttpOnly cookie ที่หมุนใบใหม่ทุกครั้ง (ADR facility-0011 ถึง facility-0016) หน้า Dashboard, หน้าสแกน และ SignalR hub ต้อง login ก่อน (facility-0017, facility-0018)

---

## 🚀 วิธีการรันระบบ (How to Run)

### 1. ฐานข้อมูล (MySQL)
ตรวจสอบให้แน่ใจว่า MySQL กำลังทำงาน (ต้องเป็น MySQL 8.0.16 or later — CHECK constraints are enforced):
```bash
brew services start mysql
```

**กรณีฐานข้อมูลทดสอบเก่าก่อนสคีมา Pilot (เฟส 1 ที่สร้างตารางด้วย `EnsureCreated` หรือเฟส 2 ที่มี migration เรื่อง auth/refresh token):** สคีมา Pilot เป็น migration ชุดใหม่ชุดเดียว (`PilotSchema`) ที่แทนที่ของเดิม ถ้าไม่ล้างฐานข้อมูลเดิม API จะเริ่มไม่ขึ้นและแจ้ง "table already exists" และอาจทิ้งฐานข้อมูลไว้ในสภาพ migrate ค้างครึ่งทาง ให้ใช้ฐานข้อมูลทดสอบใหม่ ตั้ง `ConnectionStrings:DefaultConnection` ใน user-secrets ให้ชี้ไปฐานข้อมูลนั้น แล้วให้ API migrate เอง อย่าล้าง `facility_dashboard` เพื่อเริ่มระบบตามปกติ เพราะอาจมีข้อมูลพัฒนาที่เจ้าของต้องการเก็บ

### 2. ตั้ง connection string และ JWT signing key (ครั้งเดียวต่อเครื่อง)
ตั้ง `ConnectionStrings:DefaultConnection` ใน private user-secrets ของโปรเจกต์ API ด้วยฐานข้อมูลและบัญชี MySQL ของเครื่อง เก็บรหัสผ่านนอก repo และตรวจชื่อฐานข้อมูลก่อนเริ่ม เพราะ API migrate อัตโนมัติ private user-secrets มีผลเหนือ `appsettings.Development.json`

repo นี้เป็น public จึงไม่มี key อยู่ในไฟล์ config และ API จะไม่ยอมเริ่มถ้ายังไม่ได้ตั้ง:
```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/JanitorialManagementDashboard.Api
```

### 3. รัน Backend API (.NET 10)
รัน Backend บน Port `5001` (หลีกเลี่ยง Port 5000 ของ AirPlay บน macOS):
```bash
dotnet run --project backend/src/JanitorialManagementDashboard.Api --launch-profile http --urls "http://localhost:5001"
```

### 4. รัน Frontend (Vite React)
```bash
cd frontend
npm install
npm run dev -- --host localhost --port 5173 --strictPort
```

ครั้งแรกที่ `npm install` จะติดตั้ง pre-commit hook (husky) ซึ่งรัน `npm run lint`, `npx tsc -b` และ `npm test` ของ frontend ทุกครั้งที่ commit

ให้ API เสิร์ฟหน้าเว็บเอง (แบบระบบจริง ไม่ต้องรัน Vite):
```bash
npm --prefix frontend run build:api   # build ลง backend/src/JanitorialManagementDashboard.Api/wwwroot (git ไม่เก็บ)
dotnet run --project backend/src/JanitorialManagementDashboard.Api --launch-profile http --urls "http://localhost:5001"
# เปิด http://localhost:5001/login  ตรวจว่า API ทำงานที่ /api/health
```

### 5. รันชุดทดสอบ
ชุดทดสอบ API ใช้ SQLite ในหน่วยความจำ ไม่ต้องเปิด MySQL:
```bash
dotnet test backend/JanitorialManagementDashboard.slnx
npm --prefix frontend test
```

---

## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง

- **หน้าจอ Dashboard (สำหรับ Admin บนคอมพิวเตอร์)**:
  - `http://localhost:5173/dashboard` — เฉพาะบัญชี Admin เท่านั้นที่เข้าได้ (facility-0058) แล้วเปิดค้างไว้ได้
- **มือถือของแม่บ้านและหัวหน้า (ระบบจริงเปิดผ่าน HTTPS จากอินเทอร์เน็ต, facility-0049)**:
  - `https://<โดเมนระบบ>/login` (ทดลองบนมือถือด้วย HTTPS tunnel ตาม [handover §2.9](docs/handover.md#29-ทดลองบนเครื่องตัวเองก่อน-ไม่มี-server)) เข้าด้วยรหัสพนักงาน + เบอร์โทร แม่บ้านไปหน้า "งานของฉัน" (`/my-work`) หัวหน้าไปหน้า "ตึกของฉัน" (`/my-building`) หน้าตึกของฉันอัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ
  - Admin เข้าที่ `/login/admin` ด้วยชื่อผู้ใช้ + รหัสผ่าน แล้วไปหน้า Dashboard (`/dashboard`: สถานะทุกจุดตาม Area อัปเดตสด กรองตามตึก สถานะ และมีปัญหาที่แจ้ง) เมนู Admin มี "การเข้างาน" (`/admin/attendance`: แต่ละ Area มีคนหรือยัง เพิ่ม/แก้เวลาพร้อมเหตุผล), "รายการที่ขึ้นเตือน" (`/admin/alerts`: ปิดเตือน มอบหมายและยกเลิกคนทำแทน ตัวเลขในเมนูคือเตือนที่ยังไม่ได้คุย), "พื้นที่และป้าย" (`/admin/areas`: Area จุด เวลารอบ รัศมี พิกัดป้าย ออก QR ใหม่ และพิมพ์ป้ายที่ `/admin/print`), "บัญชีผู้ใช้" (`/admin/accounts`), "สรุปการตรวจ" (`/admin/inspection-summary`: การตรวจของหัวหน้าแต่ละคนตามช่วงวัน), "ส่งออก Excel" (`/admin/export`: ดาวน์โหลด .xlsx การลงเวลา การส่งงาน การตรวจงาน) และ "ประวัติการแก้ไข" (`/admin/history`: ทุกการแก้ของ Admin พร้อมก่อน → หลังและเหตุผล อ่านอย่างเดียว) หน้า Admin โหลดแยกจากหน้ามือถือ มือถือจึงไม่ต้องโหลดโค้ดของ Admin
  - เก็บพิกัดป้ายบนมือถือที่ `/admin/areas/<id>/capture` ยืนที่ป้ายแล้วกด "ใช้ตำแหน่งตรงนี้" (30 วินาที ต้องเป็น https หรือ `localhost`) พิมพ์ป้ายจากที่อยู่ https ของเซิร์ฟเวอร์ เพราะที่อยู่บนป้ายคือที่อยู่ของหน้าที่สั่งพิมพ์
  - ป้ายลงเวลาของ Area เปิด `/check-in/<token>` และป้ายจุดเปิด `/scan/<token>` (แม่บ้านเห็นหน้าส่งงาน หัวหน้าเห็นหน้าตรวจงาน) ป้ายของ seed เช่น `/check-in/token-checkin-ar01` และ `/scan/token-restroom-m1`
  - ลงเวลาและส่งงานต้องอ่าน GPS ได้ browser ให้อ่านเฉพาะหน้า https หรือ `localhost` มือถือที่เปิด `http://<IP>` จะขึ้น "อ่านตำแหน่งไม่ได้" จนกว่าจะมี HTTPS (facility-0049) ระหว่างนี้ทดสอบบน Mac ที่ `http://localhost:5173` หรือ `:5001` แล้วตั้งตำแหน่งใน Chrome DevTools › More tools › Sensors › Location
  - login ครั้งแรกแล้วระบบจำไว้ จนกด "ออกจากระบบ" หรือไม่ได้ใช้เลย 30 วัน

### บัญชีผู้ใช้ทดสอบ (Seed Data, ข้อมูลสมมุติทั้งหมด ใช้ได้เฉพาะตอนรันแบบ Development)
- **แม่บ้าน Area AR01 กะเช้า**: รหัสพนักงาน `1000001` / เบอร์โทร `0810000001`
- **แม่บ้าน Area AR01 กะดึก**: รหัสพนักงาน `1000002` / เบอร์โทร `0810000002`
- **แม่บ้าน Area AR02 (Office กะเช้าอย่างเดียว)**: รหัสพนักงาน `1000003` / เบอร์โทร `0810000003`
- **หัวหน้า ตึก A กะเช้า**: รหัสพนักงาน `2000001` / เบอร์โทร `0820000001`
- **ผู้ดูแลระบบ**: username `admin` / password `admin1234`

แม่บ้านและหัวหน้า login ที่ `/login` ด้วยรหัสพนักงาน + เบอร์โทร ส่วน Admin login ที่ `/login/admin` ด้วย username + password

ลองใน Swagger (`/swagger`): แม่บ้านต้องลงเวลาเข้างานก่อนส่งงาน — `POST /api/attendance` ด้วย `qrToken` = `token-checkin-ar01`, `eventType` = `ShiftIn` และพิกัด เช่น `latitude` 13.7563, `longitude` 100.5018, `accuracyM` 10 (ป้ายของ AR01 มีพิกัดตัวอย่าง ป้ายของ AR02 ยังไม่มี) จากนั้นจึง `POST /api/scan-records` ได้ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะถึง 60 นาทีหลังกะ (ADR facility-0069)

หัวหน้าตรวจงานใน Swagger (login ด้วย `2000001` / `0820000001` หัวหน้าตึก A กะเช้า, ADR facility-0029, 0047, 0048, 0057):

- `POST /api/inspections/scan` สแกนป้ายจุด เช่น `{ "qrToken": "token-restroom-m1", "latitude": 13.7563, "longitude": 100.5018, "accuracyM": 10 }` ได้การ์ดของจุด งานที่ส่งล่าสุดของรอบปัจจุบัน (`submission`) หรือเหตุผลที่ยังตรวจไม่ได้ (`refusal`) จุดในตึกอื่นหรือสแกนนอกกะของตัวเอง (ตามนาฬิกา เช้า 07:00–19:00 ดึก 19:00–07:00) ถูกบล็อกและลงรายการที่ขึ้นเตือน เว้นแต่ Admin มอบหมายตรวจแทนไว้
- `POST /api/inspections` บันทึกผล `{ "qrToken": …, "scanRecordId": <submission.id>, "result": "Passed" | "Rework", "defect": "…", "latitude": …, "longitude": …, "accuracyM": … }` เลือก `Rework` (ไม่สะอาด) ต้องมี `defect` เลือก `Passed` ห้ามส่ง `defect` ถ้าแม่บ้านส่งงานใหม่หลังสแกน ได้ 409 ให้สแกนใหม่
- `GET /api/my-building` ทุกจุดในตึกของตัวเอง (และตึกที่ได้รับมอบหมายกะนี้) จุดรอตรวจขึ้นก่อน เปิดนอกกะได้แต่ `canInspect` เป็น false
- Hub `/hubs/scan` ส่ง `ScanRecorded` และ `InspectionRecorded` ให้ Admin ที่เปิดใช้งาน หัวหน้าของตึกนั้น แม่บ้านของ Area นั้น และคนทำแทนที่มีสิทธิ์ โดยอ่านผู้รับจากฐานข้อมูลตอนส่ง (facility-0105)

ตั้งค่า Area ใน Swagger (login เป็น `admin`, ADR facility-0045):

- `GET /api/admin/areas/{id}` ดูป้ายทุกป้ายพร้อม QR Token, พิกัด และช่วงรอบ
- `POST /api/admin/areas` เพิ่ม Area ระบบสร้างป้ายลงเวลา `<รหัส>-IN` ให้เอง
- `POST /api/admin/areas/{areaId}/points` เพิ่มจุดพร้อมช่วงรอบ เช่น `{ "shift": "Day", "start": "07:00", "end": "09:00" }`
  - ช่วงรอบห้ามซ้อนกัน ต้องอยู่ในกะ และห้ามจบตรง 19:00 หรือ 07:00 (ADR facility-0047)
- `PUT /api/admin/signs/{id}/location` บันทึกพิกัดป้าย
- `PUT /api/admin/signs/{id}/radius` ตั้งรัศมีของป้าย 10–500 เมตร (ค่าเริ่ม 50)
- `POST /api/admin/signs/{id}/regenerate-token` ออก QR ใหม่
- ทุกการแก้ลงตาราง `audit_log`

ดูการเข้างาน รายการที่ขึ้นเตือน และประวัติใน Swagger (login เป็น `admin`):

- `GET /api/admin/attendance` การเข้างานของกะที่กำลังเดินอยู่ หรือส่ง `shiftDate=2026-10-08&shift=Day` เพื่อดูกะอื่น (ADR facility-0050)
- `PUT /api/admin/attendance` เพิ่มหรือแก้เวลา เช่น `{ "userId": 3, "shiftDate": "2026-10-08", "shift": "Day", "eventType": "ShiftOut", "time": "18:58", "reason": "ลืมสแกน" }` ต้องมีเหตุผลทุกครั้ง (ADR facility-0051)
- `GET /api/admin/alerts` รายการที่ขึ้นเตือนของเมื่อวานและวันนี้ และ `POST /api/admin/alerts/review` ปิดเตือน
- `POST /api/admin/cover-assignments` มอบหมายแม่บ้านทำแทน Area (`areaId`) หรือหัวหน้าตรวจแทนตึก (`buildingId`, กะไหนก็ได้ยกเว้นตึกและกะของตัวเอง) ใส่ `blockedScanId` เพื่อมอบหมายจากการสแกนที่ถูกบล็อก (ADR facility-0041, 0048)
- `GET /api/admin/cover-assignments/candidates?areaId=&shiftDate=&shift=` รายชื่อคนที่ทำแทนได้ของ Area และกะนั้น
- `GET /api/admin/audit-log/actors` รายชื่อผู้ทำสำหรับตัวกรอง
- `GET /api/admin/audit-log` ประวัติ Admin อ่านอย่างเดียว กรองได้ด้วย `from`, `to` (yyyy-MM-dd), `actorId`, `category` (Attendance, Cover, AlertReview, Setup, QrToken, Account, Export), `q` (คำค้นไม่เกิน 100 ตัวอักษร ค้นในสรุป ชื่อผู้ทำ และเหตุผลของทุกแถวในช่วงวันที่), `beforeId` และ `limit` (1–500 ค่าเริ่ม 100) (ADR facility-0057)

จัดการบัญชีและส่งออก Excel ใน Swagger (login เป็น `admin`, ADR facility-0020, 0053, 0054):

- `GET /api/admin/accounts` รายชื่อบัญชีทั้งหมด (ไม่มีเบอร์โทรและรหัสผ่าน) และ `POST /api/admin/accounts` เพิ่มบัญชี: แม่บ้านและหัวหน้าใส่ `employeeId` กับ `phone` (10 หลัก ขึ้นต้นด้วย 0) ส่วน Admin ใส่ `username` กับ `password` ชั่วคราว ช่องที่ไม่เกี่ยวกับบทบาทให้เว้นไว้ ห้ามส่งค่าว่าง
- `PUT /api/admin/accounts/{id}` แก้ชื่อที่แสดง Area ตึก หรือกะ (บทบาท รหัสพนักงาน และ username แก้ไม่ได้) PUT แทนที่ทุกช่อง: ถ้าไม่ส่ง `areaId` แม่บ้านจะหลุดจาก Area จึงต้องส่ง `areaId` เดิมกลับไปถ้าจะให้อยู่ Area เดิม
- `POST /api/admin/accounts/{id}/deactivate` ปิดบัญชี (ให้ออกจากระบบทุกเครื่อง และแม่บ้านจะหลุดจาก Area) และ `POST /api/admin/accounts/{id}/activate` เปิดบัญชีอีกครั้ง ปิดบัญชีตัวเองไม่ได้ ไม่มีการลบบัญชี
- `PUT /api/admin/accounts/{id}/phone` เปลี่ยนเบอร์โทรของแม่บ้านหรือหัวหน้า และ `PUT /api/admin/accounts/{id}/password` รีเซ็ตรหัสผ่านของ Admin ทั้งสองอย่างยกเลิก refresh token ทุกเครื่อง ต้อง login ใหม่เมื่อ access token หมดอายุภายใน 5 นาที และประวัติไม่เก็บเบอร์หรือรหัส (ADR facility-0057)
- `POST /api/admin/export` ได้ไฟล์ `.xlsx` เช่น `{ "from": "2026-10-01", "to": "2026-10-07", "buildingId": 1, "sets": ["Attendance", "Submissions", "Inspections"] }` เลือกได้ 1–31 วัน หรือใช้ `buildingIds: [1, 2]` เพื่อเลือกหลายตึก (ห้ามส่งพร้อม `buildingId`) ไม่ใส่ตัวเลือกตึกคือทุกตึก ชีต "การส่งงาน" มีแถว "ไม่ได้ทำ" ชีต "การตรวจงาน" มีแถว "ไม่ได้ตรวจ" และ "ไม่ได้แก้" ของกะที่ปิดแล้ว ทุกการส่งออกลงตาราง `audit_log`
- `GET /api/admin/inspection-summary?from=&to=&buildingId=` สรุปการตรวจของหัวหน้าแต่ละคน (ค่าเริ่ม 7 วันล่าสุด เลือกได้ไม่เกิน 31 วัน) อ่านอย่างเดียว (ADR facility-0048)

### หมายเหตุด้านความปลอดภัยของ POC
- ระบบจริงใช้ HTTPS และต้องตั้ง `Jwt:RefreshCookieSecure` เป็น `true` (facility-0013, 0049) ส่วน localhost HTTP ใช้เฉพาะพัฒนา
- ปิดบัญชีมีผลทันทีต่อ API และข่าวสด (facility-0105) ส่วนเปลี่ยนเบอร์/รีเซ็ตรหัสผ่านมีผลครบทุกเครื่องภายในอายุ access token 5 นาที
- Login Lock จำกัดคู่ชื่อที่ใช้ login + IP และมีเพดานต่อ IP (facility-0104); แอปส่ง security headers และ `Cache-Control: no-store` ให้ `/api` (facility-0106)
- บัญชีทดสอบข้างบนเป็นข้อมูลตัวอย่างเท่านั้น

### คู่มือสำหรับ server จริง
- [docs/handover.md](docs/handover.md): คู่มือติดตั้งระบบบน server Linux (facility-0093) สร้างแอดมินคนแรกจากค่าใน server และกู้คืนด้วยคำสั่ง `reset-admin` (ADR facility-0092)

### Visible end-to-end testing

Run `npm --prefix frontend run e2e -- --dataset large` for real UI workflows in headed Chromium against an isolated synthetic MySQL database. See [the E2E guide](docs/testing/e2e.md) for prerequisites, dataset sizes, safety and reports.
