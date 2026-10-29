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

เขียนสำหรับ Ubuntu Server 24.04 LTS หรือใหม่กว่า ยังไม่ได้เลือก server จริง (facility-0093) ถ้าเลือก Windows Server ต้องเขียนส่วนนี้เพิ่ม

### 2.1 สิ่งที่ต้องมี

- server Linux ที่เปิด port 80 และ 443 จากอินเทอร์เน็ตได้ (มือถือแม่บ้านใช้เน็ตของตัวเอง facility-0049)
- domain ที่ชี้มาที่ server เช่น `facility.example.com` ป้าย QR พิมพ์จาก domain นี้ เปลี่ยน domain = พิมพ์ป้ายใหม่ทั้งหมด
- ASP.NET Core Runtime 10 (`sudo apt install aspnetcore-runtime-10.0`) บน Ubuntu 24.04 ต้องรัน `sudo add-apt-repository ppa:dotnet/backports` และ `sudo apt update` ก่อน
- MySQL 8 ขึ้นไป
- Caddy สำหรับ https (`sudo apt install caddy`) ขอและต่ออายุใบรับรองให้เอง

### 2.2 ฐานข้อมูลและผู้ใช้ MySQL ของระบบ

ระบบใช้ผู้ใช้ MySQL ของตัวเอง ที่มีสิทธิ์แค่ฐานข้อมูลของระบบ ไม่ใช้ root เข้า MySQL ด้วย root แล้วรัน (เปลี่ยน `<รหัสผ่าน>` ห้ามมี `;` `'` `"` หรือ `\` ในรหัสผ่าน และห้ามมีช่องว่างหน้าหรือท้าย)

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
dotnet publish backend/src/FacilityRealtime.Api -c Release -o ../facility-publish
```

โฟลเดอร์ `../facility-publish` อยู่นอก repo ห้ามเอาเข้า git

บน server สร้างโฟลเดอร์ก่อน `sudo mkdir -p /opt/facility` แล้วคัดลอกโฟลเดอร์ `facility-publish` ไปที่ `/opt/facility/app` และ `scripts/facility.sh` ไปที่ `/opt/facility/facility.sh` แล้วบน server

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin facility
sudo chown -R root:root /opt/facility
sudo chmod -R a+rX,go-w /opt/facility
sudo chmod 755 /opt/facility/facility.sh
```

บน server เครื่องแรก หลังคัดลอกแล้วให้รัน `bash scripts/facility.test.sh` จากสำเนาของ repo เพื่อตรวจ `facility.sh` บนระบบนั้น (ใช้ `stat` ของ GNU) ซึ่งที่ผ่านมาเคยรันบน macOS เท่านั้น

### 2.4 ไฟล์ค่า

```bash
sudo /opt/facility/facility.sh init /etc/facility/facility.env
```

ตอบคำถามทีละข้อ บน server คำถามที่อยู่ `FacilityRealtime.Api.dll` ต้องตอบ `/opt/facility/app/FacilityRealtime.Api.dll` ถ้าเว้นว่าง `facility.sh` ที่คัดลอกมาลำพังหาโปรแกรมไม่เจอ สคริปต์สุ่ม `Jwt__SigningKey` ให้ ไฟล์ที่ได้อ่านได้เฉพาะ root

`init` ไม่รับคำตอบที่มี `'` `"` หรือ `\` ที่ตำแหน่งใดก็ตาม หรือมีช่องว่างหน้าหรือท้าย (และ host, port, ชื่อฐานข้อมูล, ผู้ใช้, รหัสผ่าน MySQL ห้ามมี `;` ด้วย) สคริปต์บอกว่าให้แก้คำตอบข้อไหน และยังไม่เขียนอะไรลงไฟล์จนกว่าทุกคำตอบจะผ่าน

| ค่า | ความหมาย |
|---|---|
| `ConnectionStrings__DefaultConnection` | ที่อยู่ฐานข้อมูล ผู้ใช้ และรหัสผ่านจากข้อ 2.2 |
| `Jwt__SigningKey` | กุญแจเซ็น token อย่างน้อย 32 ไบต์ เปลี่ยนแล้วแค่ token อายุ 5 นาทีหมดอายุ ทุกเครื่องต่ออายุเงียบ ๆ จากฐานข้อมูล (refresh token เก็บในฐานข้อมูล) ผู้ใช้ไม่รู้สึก |
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
LogNamespace=facility

[Install]
WantedBy=multi-user.target
```

สร้าง `/etc/systemd/journald@facility.conf` ให้ log ของระบบเก็บ 90 วันแล้วลบเอง (facility-0098, 0099) ค่านี้ใช้กับ log ของระบบนี้เท่านั้น log ของโปรแกรมอื่นบนเครื่องไม่ถูกแตะ ต้องใช้ systemd 245 ขึ้นไป (ดูด้วย `systemctl --version`)

```ini
[Journal]
MaxRetentionSec=90day
MaxFileSec=1day
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now facility
sudo journalctl --namespace=facility -u facility -f
```

log ต้องไม่มีข้อผิดพลาด ถ้าขึ้น `ConnectionStrings__DefaultConnection is not set.` ให้ตรวจไฟล์ค่า

ดูเฉพาะเหตุการณ์ด้านความปลอดภัย: `sudo journalctl --namespace=facility -u facility | grep Security`

- `Login failed: employee 1000001, wrong secret` ใส่เบอร์ผิด (`account turned off` คือบัญชีถูกปิดใช้งาน)
- `Login failed: unknown account (employee login)` รหัสพนักงานที่พิมพ์ไม่มีในระบบ ระบบไม่เขียนสิ่งที่พิมพ์ เพราะอาจเป็นเบอร์โทรที่พิมพ์สลับช่อง
- `Login refused while locked: ...` ผิด 5 ครั้งแล้ว ถูกล็อก 15 นาที ทุกครั้งที่ลองระหว่างล็อกขึ้นหนึ่งบรรทัด
- `Refresh token reused, session ended: ...` มีคนใช้ token เดิมซ้ำ ระบบให้บัญชีนั้นเข้าระบบใหม่ (facility-0015)

log ไม่มีเบอร์โทร รหัสผ่าน token หรือพิกัด (facility-0097)

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
set -e
umask 077
mkdir -p /var/backups/facility
file=/var/backups/facility/facility-$(date +%F-%H%M%S).sql
trap 'rm -f "$file.part"' EXIT
mysqldump --single-transaction facility > "$file.part"
mv "$file.part" "$file"
gzip "$file"
find /var/backups/facility -name '*.sql.gz' -mtime +14 -delete
```

ชื่อไฟล์มีวันและเวลา จึงไม่มีรอบไหนเขียนทับ backup ของรอบอื่น (รวมถึงที่ทำมือก่อนอัปเดต) ถ้า dump ล้มเหลว สคริปต์หยุดทันที ลบไฟล์ `.part` ที่ค้าง และไม่ลบไฟล์เก่า cron รันด้วย root ซึ่งต้องเข้า MySQL ได้โดยไม่ต้องพิมพ์รหัส (Ubuntu ตั้ง root ของ MySQL แบบ `auth_socket` ให้อยู่แล้ว) ควรคัดลอกไฟล์ backup ออกไปเก็บนอก server ด้วย

กู้คืน: `sudo systemctl stop facility` แล้ว `sudo sh -c 'gunzip < /var/backups/facility/<ไฟล์>.sql.gz | mysql facility'` แล้ว `sudo systemctl start facility`

### 2.8 อัปเดตเป็นเวอร์ชันใหม่

1. ทำข้อ 2.3 บนเครื่องที่มีโค้ด
2. `sudo systemctl stop facility` แล้วสำรองฐานข้อมูลทันทีด้วย `sudo /etc/cron.daily/facility-backup`
3. เก็บโฟลเดอร์เก่าไว้ถอยกลับ `sudo mv /opt/facility/app /opt/facility/app.previous` แล้วคัดลอกโฟลเดอร์ใหม่ไปเป็น `/opt/facility/app` คัดลอก `facility.sh` ตัวใหม่ไปทับด้วย แล้วทำ `chown`/`chmod` ตามข้อ 2.3 อีกครั้ง
4. `sudo systemctl start facility` ระบบ migrate ฐานข้อมูลเองตอนเริ่ม
5. ถ้าต้องถอยกลับ: หยุดระบบ ย้าย `app.previous` กลับเป็น `app` กู้ฐานข้อมูลจาก backup ที่ทำก่อนอัปเดต (ข้อ 2.7) แล้วเริ่มระบบ

### 2.9 ทดลองบนเครื่องตัวเองก่อน (ไม่มี server)

ทำข้อ 2.2 ก่อน โดยใช้ฐานข้อมูลทดลองที่ชื่อเข้ากับ `facility_*_test` และผู้ใช้ MySQL ของมันเอง

ทำ `npm --prefix frontend ci` และ `npm --prefix frontend run build:api` (สองคำสั่งแรกของข้อ 2.3) ก่อน เพื่อให้มีหน้าเว็บ แล้ว

```bash
scripts/facility.sh init ~/.facility/trial.env
scripts/facility.sh start ~/.facility/trial.env
```

ตอน `init` คำถามที่อยู่ dll ให้เว้นว่าง `start` รันค้างอยู่ในหน้าต่างนั้น จึงเปิด Terminal หน้าต่างที่สองแล้วรัน

```bash
cloudflared tunnel --url http://localhost:5001
```

ใช้ฐานข้อมูลทดลองและข้อมูลสมมติเท่านั้น ลิงก์ `trycloudflare.com` เปลี่ยนทุกครั้งที่เปิด ป้าย QR ที่พิมพ์จากลิงก์นี้ใช้ได้แค่ครั้งนั้น

## 3. สร้างแอดมินคนแรก (ครั้งเดียว ตอน deploy ครั้งแรก)

ฐานข้อมูลใหม่ยังไม่มีบัญชีใดเลย และบัญชีทุกใบเพิ่มได้จากหน้า "บัญชีผู้ใช้" ซึ่งต้องเข้าระบบเป็นแอดมินก่อน แอดมินคนแรกจึงสร้างจากค่าใน server

1. ค่า 3 ตัวนี้อยู่ในไฟล์ค่าแล้ว ถ้าสร้างด้วย `facility.sh init` (ข้อ 2.4)
   - `FirstAdmin__Username` ชื่อผู้ใช้ ตัวอักษรอังกฤษ ตัวเลข `.` `_` หรือ `-` ยาว 3–100 ตัว
   - `FirstAdmin__Password` รหัสผ่าน ยาว 15–100 ตัว และไม่ใช่รหัสที่คนใช้กันเยอะ
   - `FirstAdmin__DisplayName` ชื่อที่แสดงในระบบ ยาว 1–150 ตัว

   ห้ามใส่ `'` `"` หรือ `\` ในรหัสผ่านและชื่อ และห้ามมีช่องว่างหน้าหรือท้าย
   ห้ามเขียนค่าเหล่านี้ลงไฟล์ `appsettings.json` หรือเก็บใน git
2. ระบบรันอยู่แล้วหลังข้อ 2.5 ดูใน log (`sudo journalctl --namespace=facility -u facility`) ต้องมีบรรทัด `Created the first Admin 'first.admin'`
3. เข้า `https://<ที่อยู่ระบบ>/login/admin` ด้วยชื่อผู้ใช้และรหัสผ่านนั้น
4. ลบบรรทัด `FirstAdmin__Password` ออกจากไฟล์ค่า แล้ว `sudo systemctl restart facility` ถ้ายังตั้งค้างไว้ log จะเตือน `FirstAdmin__Password is still set`
5. ไปหน้า "บัญชีผู้ใช้" กด "รีเซ็ตรหัสผ่าน" ที่บัญชีของตัวเอง แล้วตั้งรหัสใหม่ที่มีแค่คุณรู้ เพราะรหัสเดิมเคยอยู่ในค่าตั้งของ server
6. เพิ่มแอดมินคนที่สองในหน้า "บัญชีผู้ใช้" (ดูข้อ 5)
7. เพิ่มตึกในหน้า "ตึก" แล้วจึงเพิ่มพื้นที่และบัญชีแม่บ้านกับหัวหน้า

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

## 6. เทส end-to-end (`npm run e2e`)

คำนี้ (end-to-end) แปลว่า "ทดสอบตั้งแต่ต้นจนจบ" เหมือนให้คนทดลองเดินระบบจริงทุกขั้น แต่เป็นหุ่นยนต์ที่เปิดเบราว์เซอร์แล้วกดแทนเรา

ให้ AI หรือคนรับช่วงต่อเทสหน้าจอจริงแทนการกดเอง (facility-0094) รันบนเครื่องนักพัฒนา ห้ามรันบน server

ต้องมี: MySQL ในเครื่อง (`root` ไม่มีรหัส หรือตั้ง `E2E_MYSQL_PASSWORD`), .NET 10, Node, `openssl` (จบแค่นี้) ครั้งแรกที่รันจะดาวน์โหลด Chromium (เบราว์เซอร์สำหรับให้หุ่นยนต์ใช้) ราว 150 MB

```bash
cd frontend
npm install
npm run e2e                                  # ทั้งหมด
npm run e2e -- --project=phone               # เฉพาะหน้ามือถือ
npm run e2e -- --project=phone --headed       # เปิดเบราว์เซอร์ให้ดูหุ่นยนต์กดจริง
npm run e2e -- --grep "check-in"             # เฉพาะเทสที่ชื่อมีคำนี้
npx playwright show-report                   # ดูผล, screenshot และ trace ของรอบล่าสุด
```

สคริปต์สร้างฐานข้อมูล `facility_e2e_test` ใหม่ทุกครั้ง (เหมือนเปิดสมุดเปล่าเล่มใหม่ให้หุ่นยนต์ลองเขียน ไม่แตะสมุดจริง) รัน API พอร์ต 5101 (เปลี่ยนด้วย `E2E_PORT`) แล้วลบทุกอย่างตอนจบ รวมถึง `wwwroot` (ไฟล์หน้าเว็บที่ build แล้ว) ซึ่งเป็นโฟลเดอร์เดียวกับที่ test build พอร์ต 5001 ใช้เปิดหน้าเว็บ ดังนั้น e2e จะลบหน้าเว็บของ test build นั้นทิ้ง อย่ารัน e2e ระหว่างที่กำลังเช็ก branch บนมือถือ ถ้าเผลอรันแล้วต้อง `npm run build:api` ใหม่ อย่ารันสองรอบพร้อมกัน เพราะใช้พอร์ตและฐานข้อมูลเดียวกัน

ถ้าเปลี่ยนข้อมูลตัวอย่าง (seed) ใน `DbInitializer.cs` ต้องแก้ `frontend/e2e/support/seed.ts` ให้ตรงกัน ถ้ารันช่วง 19:00–20:00 เวลาไทย จะมีหนึ่งเทสข้ามตัวเอง (เพราะรอบแรกของกะกลางคืนยังไม่เริ่ม จึงยังไม่มีงานให้ส่ง) ผลจึงเป็น "8 ผ่าน 1 ข้าม" ซึ่งปกติ

### ถ้ารันค้างหรือถูกปิดกลางทาง

```bash
cd frontend
npm run e2e:clean
```

หยุด API ที่ค้าง (pid อยู่ใน `.run/e2e-api.pid` และจะหยุดเฉพาะโปรเซสที่เป็น API ของเราจริง), ลบ `facility_e2e_test`, ลบ `wwwroot` log ของ API รอบล่าสุดอยู่ที่ `.run/e2e-api.log`

### สิ่งที่เทสนี้จับไม่ได้

หน้าตาเฉพาะ Safari บน iPhone, การเลื่อนหน้าที่ขึ้นกับแถบเครื่องมือของเบราว์เซอร์มือถือ, GPS จริง และการพิมพ์ป้าย D5 ยังต้องเช็กบนเครื่องจริง
