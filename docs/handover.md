# คู่มือส่งมอบระบบ

สำหรับคนที่รับช่วงดูแลระบบตรวจงานทำความสะอาดต่อจากเจ้าของโปรเจกต์ ทำตามทีละขั้นได้โดยไม่ต้องอ่านโค้ด แต่ต้องเข้า server ที่รันระบบได้ (พิมพ์คำสั่งใน terminal ของ server ได้)

ลำดับครั้งแรก: ข้อ 2 ติดตั้งระบบบน server → ข้อ 3 สร้างแอดมินคนแรก → ข้อ 5 ให้มีแอดมิน 2 คน ข้อ 4 ใช้เมื่อไม่มีแอดมินคนไหนเข้าระบบได้

**รหัสผ่านทุกจุดในเอกสารนี้พิมพ์แบบไม่แสดงบนจอ ตรวจว่าแป้นพิมพ์เป็นภาษาอังกฤษก่อนพิมพ์** ถ้าแป้นเป็นภาษาไทย รหัสที่ได้จะเป็นตัวไทยโดยมองไม่เห็น

## 1. ก่อนเริ่ม

- ระบบคือโปรแกรม `FacilityRealtime.Api` (ไฟล์ `FacilityRealtime.Api.dll` ในโฟลเดอร์ที่ deploy)
- ระบบอ่านค่าตั้งจาก environment variable ของ server ชื่อที่มี `__` (ขีดล่างสองตัว) เช่น `ConnectionStrings__DefaultConnection` คือที่อยู่ฐานข้อมูล
- ค่าทั้งหมดอยู่ในไฟล์ค่าไฟล์เดียว (ข้อ 2.4) ทั้งระบบที่รันด้วย systemd และ `scripts/facility.sh` อ่านไฟล์นี้ จึงไม่ต้อง `export` ค่าเองในหน้าต่าง Terminal ค่าที่ `export` ไว้มีผลแค่หน้าต่างนั้น เคยทำให้คำสั่งไปทำกับฐานข้อมูลผิดตัว
- ถ้าไม่ได้ตั้ง `ConnectionStrings__DefaultConnection` ระบบไม่ยอมเริ่ม และ `reset-admin` ไม่ทำอะไร (facility-0093)

## 2. ติดตั้งระบบบน server (Linux)

เขียนสำหรับ Ubuntu Server 24.04 LTS ยังไม่ได้เลือก server จริง (facility-0093) ถ้าเลือก Windows Server ต้องเขียนส่วนนี้เพิ่ม

### 2.1 สิ่งที่ต้องมี

- server Linux ที่เปิด port 80 และ 443 จากอินเทอร์เน็ตได้ (มือถือแม่บ้านใช้เน็ตของตัวเอง facility-0049)
- domain ที่ชี้มาที่ server เช่น `facility.example.com` ป้าย QR พิมพ์จาก domain นี้ เปลี่ยน domain = พิมพ์ป้ายใหม่ทั้งหมด
- ASP.NET Core Runtime 10 (`sudo apt install aspnetcore-runtime-10.0`)
- MySQL 8 ขึ้นไป
- Caddy สำหรับ https (`sudo apt install caddy`) ขอและต่ออายุใบรับรองให้เอง

### 2.2 ฐานข้อมูลและผู้ใช้ MySQL ของระบบ

ระบบใช้ผู้ใช้ MySQL ของตัวเอง ที่มีสิทธิ์แค่ฐานข้อมูลของระบบ ไม่ใช้ root เข้า MySQL ด้วย root แล้วรัน (เปลี่ยน `<รหัสผ่าน>` ห้ามมี `;` `'` `"` หรือ `\` และห้ามมีช่องว่างหน้าหรือท้าย)

```sql
CREATE DATABASE facility CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'facility_app'@'localhost' IDENTIFIED BY '<รหัสผ่าน>';
GRANT ALL PRIVILEGES ON facility.* TO 'facility_app'@'localhost';
```

ตารางทั้งหมดสร้างเองตอนระบบเริ่มครั้งแรก (migrate)

### 2.3 สร้างไฟล์โปรแกรมแล้ววางบน server

บนเครื่องที่มีโค้ด (ต้องมี .NET SDK 10 และ Node.js)

```bash
npm --prefix frontend ci
npm --prefix frontend run build:api
dotnet publish backend/src/FacilityRealtime.Api -c Release -o publish
```

คัดลอกโฟลเดอร์ `publish` ไปที่ `/opt/facility/app` บน server และ `scripts/facility.sh` ไปที่ `/opt/facility/facility.sh` แล้วบน server

```bash
sudo useradd --system --no-create-home facility
sudo chmod 755 /opt/facility/facility.sh
```

### 2.4 ไฟล์ค่า

```bash
sudo /opt/facility/facility.sh init /etc/facility/facility.env
```

ตอบคำถามทีละข้อ ที่อยู่ `FacilityRealtime.Api.dll` คือ `/opt/facility/app/FacilityRealtime.Api.dll` สคริปต์สุ่ม `Jwt__SigningKey` ให้ ไฟล์ที่ได้อ่านได้เฉพาะ root

`init` ไม่รับคำตอบที่ขึ้นต้นด้วยเครื่องหมายคำพูด มี `\` หรือมีช่องว่างหน้าหรือท้าย (และ host, port, ชื่อฐานข้อมูล, ผู้ใช้, รหัสผ่าน MySQL ห้ามมี `;` `'` `"`) สคริปต์บอกว่าให้แก้คำตอบข้อไหน และยังไม่เขียนอะไรลงไฟล์จนกว่าทุกคำตอบจะผ่าน

| ค่า | ความหมาย |
|---|---|
| `ConnectionStrings__DefaultConnection` | ที่อยู่ฐานข้อมูล ผู้ใช้ และรหัสผ่านจากข้อ 2.2 |
| `Jwt__SigningKey` | กุญแจเซ็น token อย่างน้อย 32 ไบต์ เปลี่ยนเมื่อไหร่ ทุกคนต้องเข้าระบบใหม่ |
| `Jwt__RefreshCookieSecure` | `true` เพราะเปิดผ่าน https |
| `ASPNETCORE_URLS` | `http://localhost:5001` ระบบรับเฉพาะจากในเครื่อง Caddy ส่งต่อให้ |
| `ASPNETCORE_ENVIRONMENT` | `Production` ห้ามตั้ง `Development` บน server เพราะ seed มีรหัสแอดมินที่ทุกคนเห็น (facility-0049) |
| `FirstAdmin__*` | แอดมินคนแรก ดูข้อ 3 |
| `FACILITY_APP_DLL` | ให้ `facility.sh` รันไฟล์ที่ publish แล้ว ระบบไม่ได้อ่านค่านี้ |

แก้ไฟล์ภายหลังด้วย `sudo nano /etc/facility/facility.env` บรรทัดละ `ชื่อ=ค่า` ห้ามใส่เครื่องหมายคำพูด แล้ว `sudo systemctl restart facility`

### 2.5 ให้ระบบรันเองตลอด (systemd)

สร้าง `/etc/systemd/system/facility.service`

```ini
[Unit]
Description=Facility Real-time Dashboard
After=network.target mysql.service

[Service]
User=facility
WorkingDirectory=/opt/facility/app
EnvironmentFile=/etc/facility/facility.env
ExecStart=/usr/bin/dotnet /opt/facility/app/FacilityRealtime.Api.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now facility
sudo journalctl -u facility -f
```

log ต้องไม่มีข้อผิดพลาด ถ้าขึ้น `ConnectionStrings__DefaultConnection is not set.` ให้ตรวจไฟล์ค่า

### 2.6 https (Caddy)

แก้ `/etc/caddy/Caddyfile` เป็น

```
facility.example.com {
    reverse_proxy localhost:5001
}
```

แล้ว `sudo systemctl reload caddy` เปิด `https://facility.example.com` ต้องเห็นหน้าเข้าระบบ ต่อไปทำข้อ 3

### 2.7 สำรองฐานข้อมูลทุกวัน

สร้าง `/etc/cron.daily/facility-backup` (`sudo chmod 755`)

```bash
#!/bin/sh
mkdir -p /var/backups/facility
mysqldump --single-transaction facility | gzip > /var/backups/facility/facility-$(date +%F).sql.gz
find /var/backups/facility -name '*.sql.gz' -mtime +14 -delete
```

cron รันด้วย root ซึ่งต้องเข้า MySQL ได้โดยไม่ต้องพิมพ์รหัส (Ubuntu ตั้ง root ของ MySQL แบบ `auth_socket` ให้อยู่แล้ว) ควรคัดลอกไฟล์ backup ออกไปเก็บนอก server ด้วย

กู้คืน: หยุดระบบ (`sudo systemctl stop facility`) แล้ว `gunzip < ไฟล์.sql.gz | sudo mysql facility` แล้วเริ่มระบบ

### 2.8 อัปเดตเป็นเวอร์ชันใหม่

1. ทำข้อ 2.3 บนเครื่องที่มีโค้ด
2. `sudo systemctl stop facility` แล้วสำรองฐานข้อมูลตามข้อ 2.7 ทันที
3. แทนที่ `/opt/facility/app` ด้วยโฟลเดอร์ `publish` ใหม่ แล้ว `sudo systemctl start facility` ระบบ migrate ฐานข้อมูลเองตอนเริ่ม

### 2.9 ทดลองบนเครื่องตัวเองก่อน (ไม่มี server)

```bash
scripts/facility.sh init ~/.facility/trial.env
scripts/facility.sh start ~/.facility/trial.env
cloudflared tunnel --url http://localhost:5001
```

ใช้ฐานข้อมูลทดลองและข้อมูลสมมติเท่านั้น ลิงก์ `trycloudflare.com` เปลี่ยนทุกครั้งที่เปิด ป้าย QR ที่พิมพ์จากลิงก์นี้ใช้ได้แค่ครั้งนั้น

## 3. สร้างแอดมินคนแรก (ครั้งเดียว ตอน deploy ครั้งแรก)

ฐานข้อมูลใหม่ยังไม่มีบัญชีใดเลย และบัญชีทุกใบเพิ่มได้จากหน้า "บัญชีผู้ใช้" ซึ่งต้องเข้าระบบเป็นแอดมินก่อน แอดมินคนแรกจึงสร้างจากค่าใน server

1. ค่า 3 ตัวนี้อยู่ในไฟล์ค่าแล้ว ถ้าสร้างด้วย `facility.sh init` (ข้อ 2.4)
   - `FirstAdmin__Username` ชื่อผู้ใช้ ตัวอักษรอังกฤษ ตัวเลข `.` `_` หรือ `-` ยาว 3–100 ตัว
   - `FirstAdmin__Password` รหัสผ่าน ยาว 15–100 ตัว และไม่ใช่รหัสที่คนใช้กันเยอะ
   - `FirstAdmin__DisplayName` ชื่อที่แสดงในระบบ ยาว 1–150 ตัว

   ห้ามเขียนค่าเหล่านี้ลงไฟล์ `appsettings.json` หรือเก็บใน git
2. เริ่มระบบ log ต้องมีบรรทัด `Created the first Admin 'first.admin'`
3. เข้า `https://<ที่อยู่ระบบ>/login/admin` ด้วยชื่อผู้ใช้และรหัสผ่านนั้น
4. ลบบรรทัด `FirstAdmin__Password` ออกจากไฟล์ค่า แล้ว `sudo systemctl restart facility` ถ้ายังตั้งค้างไว้ log จะเตือน `FirstAdmin__Password is still set`
5. ไปหน้า "บัญชีผู้ใช้" กด "รีเซ็ตรหัสผ่าน" ที่บัญชีของตัวเอง แล้วตั้งรหัสใหม่ที่มีแค่คุณรู้ เพราะรหัสเดิมเคยอยู่ในค่าตั้งของ server
6. เพิ่มแอดมินคนที่สองในหน้า "บัญชีผู้ใช้" (ดูข้อ 5)

ค่าทั้งสามมีผลเฉพาะตอนที่ยังไม่มีบัญชีแอดมินเลยสักคน นับคนที่ปิดใช้งานด้วย ถ้ามีแอดมินแล้ว ระบบไม่สร้างซ้ำ ไม่เปลี่ยนรหัส และไม่เปิดบัญชีที่ปิดไว้ ถ้าต้องการกู้คืน ใช้ข้อ 4

### ถ้าระบบไม่ยอมเริ่ม

ข้อความที่ขึ้นต้นด้วยชื่อค่าบอกว่าค่าไหนผิด ระบบไม่พิมพ์รหัสผ่านออกมา แก้ค่านั้นแล้วเริ่มใหม่

- `FirstAdmin__Username: ...` ชื่อผู้ใช้ผิดรูปแบบ หรือไม่ได้ตั้ง
- `FirstAdmin__DisplayName: ...` ไม่ได้ตั้งชื่อที่แสดง หรือยาวเกิน 150 ตัว
- `FirstAdmin__Password: ...` รหัสสั้นหรือยาวเกิน เป็นรหัสที่คนใช้กันเยอะ หรือไม่ได้ตั้ง

ถ้ายังไม่มีแอดมินและไม่ได้ตั้งค่าทั้งสามเลย ระบบเริ่มได้ แต่ log เตือน `No Admin account exists` จนกว่าจะตั้งค่า

## 4. กู้คืนเมื่อไม่มีแอดมินคนไหนเข้าระบบได้ (`reset-admin`)

ใช้เมื่อแอดมินทุกคนลืมรหัส หรือถูกปิดใช้งานหมด ถ้ายังมีแอดมินอีกคนเข้าได้ ให้คนนั้นรีเซ็ตรหัสให้ในหน้า "บัญชีผู้ใช้" แทน

1. เข้า server แล้วรัน
   ```bash
   sudo /opt/facility/facility.sh reset-admin /etc/facility/facility.env <ชื่อผู้ใช้แอดมิน>
   ```
   สคริปต์ใช้ไฟล์ค่าเดียวกับระบบ จึงไปที่ฐานข้อมูลเดียวกันเสมอ ถ้าทดลองบนเครื่องตัวเอง ใช้ `scripts/facility.sh reset-admin ~/.facility/trial.env <ชื่อผู้ใช้แอดมิน>`
2. คำสั่งแสดง `คำสั่งนี้จะเปลี่ยนข้อมูลในฐานข้อมูล <server>:<port> / <database>` เช่น `localhost:3306 / facility` ตรวจว่าถูกตัว แล้วมันถามว่า `ถ้าถูกต้อง พิมพ์ yes แล้วกด Enter: ` พิมพ์ `yes` ถ้าพิมพ์อย่างอื่น คำสั่งพิมพ์ `ไม่ได้พิมพ์ yes ไม่ได้เปลี่ยนอะไร` แล้วจบด้วย exit code 1 โดยไม่แตะฐานข้อมูล
3. ถ้าชื่อนั้นเป็นรหัสพนักงานของแม่บ้านหรือหัวหน้า คำสั่งไม่ทำอะไรและบอกเหตุผล โดยไม่ถามรหัสผ่าน
4. คำสั่งถามรหัสผ่านใหม่สองครั้ง ตัวอักษรที่พิมพ์ไม่แสดงบนจอ คำสั่งไม่รับรหัสในบรรทัดคำสั่ง เพราะจะค้างใน history
   - ชื่อนั้นเป็นแอดมินอยู่แล้ว: ตั้งรหัสใหม่ เปิดบัญชีถ้าถูกปิด และทุกเครื่องที่เคยเข้าระบบด้วยบัญชีนี้ต้องเข้าใหม่
   - ยังไม่มีชื่อนี้: สร้างแอดมินใหม่ คำสั่งถามชื่อที่แสดงก่อน
5. คำสั่งจบด้วย exit code 0 เมื่อเสร็จ 1 เมื่อไม่ได้ทำ (ข้อความบอกเหตุผล) และ 2 เมื่อพิมพ์คำสั่งผิด ถ้าขึ้นข้อผิดพลาดว่าต่อฐานข้อมูลไม่ได้ ให้ตรวจ `ConnectionStrings__DefaultConnection` ในไฟล์ค่า
6. เข้าระบบแล้วเปลี่ยนรหัสของตัวเองที่หน้า "บัญชีผู้ใช้" ทันที

หมายเหตุ

- รันได้ขณะที่ระบบทำงานอยู่ ไม่ต้องหยุดระบบ
- ถ้าบัญชีเพิ่งถูกล็อกเพราะใส่รหัสผิด 5 ครั้ง การล็อกอยู่ในหน่วยความจำของระบบที่รันอยู่ ให้รอ 15 นาที หรือรีสตาร์ตระบบ
- หน้า "ประวัติการแก้ไขของแอดมิน" มีแถวที่ผู้ทำคือ "ระบบ" ทุกครั้งที่สร้างหรือกู้คืนแอดมินด้วยวิธีในเอกสารนี้
- ใครเข้า server ได้ก็ใช้คำสั่งนี้ได้ จึงต้องจำกัดคนที่เข้า server ได้
- migration `SystemAuditActor` ย้อนกลับ (rollback) ไม่ได้ หลังจากมีแถวในประวัติที่ผู้ทำคือ "ระบบ" แล้ว ถ้าต้องถอยไปเวอร์ชันก่อนหน้า ให้กู้ฐานข้อมูลจาก backup แทน

## 5. ให้มีแอดมินที่เปิดใช้งานอย่างน้อย 2 คนเสมอ

ถ้าแอดมินคนหนึ่งลืมรหัสหรือลาออก อีกคนรีเซ็ตรหัสหรือปิดบัญชีให้ได้ในหน้า "บัญชีผู้ใช้" โดยไม่ต้องเข้า server คำสั่ง `reset-admin` จึงแทบไม่ต้องใช้
