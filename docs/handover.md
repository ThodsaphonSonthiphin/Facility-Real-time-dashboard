# คู่มือส่งมอบระบบ Janitorial Management Dashboard

**สถานะ ณ 10 พ.ย. 2026** พัฒนาตามขอบเขตที่ตกลงเสร็จแล้ว ขั้นติดตั้งบนเครื่องจริงและทดลองใช้หน้างานอยู่ในส่วนของทีมรับช่วง ตามขอบเขตงานส่งมอบที่ตกลงไว้ ปัจจุบันยังไม่มีบันทึกยืนยันเครื่อง ระบบปฏิบัติการ ที่อยู่เว็บจริง หรือผลทดลองหน้างาน ดู [สถานะส่งมอบ](handoff.md) และ [ขอบเขตงานผู้ฝึกงาน](adr/facility-0109-internship-development-and-handover-scope.md)

## จากส่งมอบถึงทดลองใช้

| ใครลงมือ | ทำอะไร |
|---|---|
| ทีม IT | เตรียมเครื่องและที่อยู่เว็บ HTTPS ที่เปิดจากเน็ตมือถือได้ เพื่อให้แม่บ้านและหัวหน้าสแกน QR และใช้ตำแหน่งมือถือได้ คู่มือนี้มีขั้นตอนสำหรับ Linux ในข้อ 2 |
| ผู้ติดตั้งและแอดมิน | สร้างแอดมินคนแรกตามข้อ 3 จากนั้นจัดให้มีบัญชีแอดมินที่ใช้งานได้อย่างน้อย 2 คนตามข้อ 5 เพื่อให้ช่วยกันกู้บัญชีจากหน้าเว็บได้ |
| แอดมิน | ตั้งค่าพื้นที่ทดลอง เช่น ชั้น 1 เพิ่มห้องน้ำอย่างน้อย 1 ห้อง ตั้งเวลาทำความสะอาด และเพิ่มบัญชีคนที่ทำงานในพื้นที่นั้น พิมพ์ QR ลงเวลาและ QR ที่ห้องจากที่อยู่เว็บจริง ติดให้ตรงสถานที่แล้วบันทึกตำแหน่งของแต่ละป้าย |
| แม่บ้านและหัวหน้าในพื้นที่ทดลอง | ใช้มือถือจริงลองลงเวลา ส่งงาน ตรวจ สั่งแก้ และตรวจซ้ำตามข้อ 7 |

ขั้นตอนสำหรับผู้ติดตั้งด้านล่างต้องใช้สิทธิ์เข้าเครื่องและพิมพ์คำสั่งใน Terminal ข้อ 4 ใช้เมื่อไม่มีแอดมินคนไหนเข้าระบบได้ ขั้นตอนใช้งานประจำวันอยู่ใน [Flowchart ทั้งสามบทบาท](role-workflows.md)

**ตอนพิมพ์รหัสผ่านใน Terminal ให้ตรวจว่าแป้นพิมพ์เป็นภาษาอังกฤษ** หน้าจอจะไม่แสดงอักขระที่พิมพ์

## 1. ก่อนเริ่ม

- ระบบคือโปรแกรม `JanitorialManagementDashboard.Api` (ไฟล์ `JanitorialManagementDashboard.Api.dll` ในโฟลเดอร์ที่ deploy)
- ระบบอ่านค่าตั้งจาก environment variable ของ server ชื่อที่มี `__` (ขีดล่างสองตัว) เช่น `ConnectionStrings__DefaultConnection` คือที่อยู่ฐานข้อมูล
- ค่าทั้งหมดอยู่ในไฟล์ค่าไฟล์เดียว (ข้อ 2.4) ทั้งระบบที่รันด้วย systemd และ `scripts/janitorial.sh` อ่านไฟล์นี้ จึงไม่ต้อง `export` ค่าเองในหน้าต่าง Terminal ค่าที่ `export` ไว้มีผลแค่หน้าต่างนั้น เคยทำให้คำสั่งไปทำกับฐานข้อมูลผิดตัว
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
dotnet publish backend/src/JanitorialManagementDashboard.Api -c Release -o ../facility-publish
```

โฟลเดอร์ `../facility-publish` อยู่นอก repo ห้ามเอาเข้า git

บน server สร้างโฟลเดอร์ก่อน `sudo mkdir -p /opt/facility` แล้วคัดลอกโฟลเดอร์ `facility-publish` ไปที่ `/opt/facility/app` และ `scripts/janitorial.sh` ไปที่ `/opt/facility/janitorial.sh` แล้วบน server

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin facility
sudo chown -R root:root /opt/facility
sudo chmod -R a+rX,go-w /opt/facility
sudo chmod 755 /opt/facility/janitorial.sh
```

บน server เครื่องแรก หลังคัดลอกแล้วให้รัน `bash scripts/janitorial.test.sh` จากสำเนาของ repo เพื่อตรวจ `janitorial.sh` บนระบบนั้น (ใช้ `stat` ของ GNU) ซึ่งที่ผ่านมาเคยรันบน macOS เท่านั้น

### 2.4 ไฟล์ค่า

```bash
sudo /opt/facility/janitorial.sh init /etc/facility/facility.env
```

ตอบคำถามทีละข้อ บน server คำถามที่อยู่ `JanitorialManagementDashboard.Api.dll` ต้องตอบ `/opt/facility/app/JanitorialManagementDashboard.Api.dll` ถ้าเว้นว่าง `janitorial.sh` ที่คัดลอกมาลำพังหาโปรแกรมไม่เจอ สคริปต์สุ่ม `Jwt__SigningKey` ให้ ไฟล์ที่ได้อ่านได้เฉพาะ root

`init` ไม่รับคำตอบที่มี `'` `"` หรือ `\` ที่ตำแหน่งใดก็ตาม หรือมีช่องว่างหน้าหรือท้าย (และ host, port, ชื่อฐานข้อมูล, ผู้ใช้, รหัสผ่าน MySQL ห้ามมี `;` ด้วย) สคริปต์บอกว่าให้แก้คำตอบข้อไหน และยังไม่เขียนอะไรลงไฟล์จนกว่าทุกคำตอบจะผ่าน

| ค่า | ความหมาย |
|---|---|
| `ConnectionStrings__DefaultConnection` | ที่อยู่ฐานข้อมูล ผู้ใช้ และรหัสผ่านจากข้อ 2.2 |
| `Jwt__SigningKey` | กุญแจเซ็น token อย่างน้อย 32 ไบต์ เปลี่ยนแล้วแค่ token อายุ 5 นาทีหมดอายุ ทุกเครื่องต่ออายุเงียบ ๆ จากฐานข้อมูล (refresh token เก็บในฐานข้อมูล) ผู้ใช้ไม่รู้สึก |
| `Jwt__RefreshCookieSecure` | `true` เพราะเปิดผ่าน https |
| `ASPNETCORE_URLS` | `http://localhost:5001` ระบบรับเฉพาะจากในเครื่อง Caddy ส่งต่อให้ |
| `ASPNETCORE_ENVIRONMENT` | `Production` ห้ามตั้ง `Development` บน server เพราะ seed มีรหัสแอดมินที่ทุกคนเห็น (facility-0049) |
| `FirstAdmin__*` | แอดมินคนแรก ดูข้อ 3 |
| `FACILITY_APP_DLL` | ให้ `janitorial.sh` รันไฟล์ที่ publish แล้ว ระบบไม่ได้อ่านค่านี้ |

แก้ไฟล์ภายหลังด้วย `sudo nano /etc/facility/facility.env` บรรทัดละ `ชื่อ=ค่า` ห้ามใส่เครื่องหมายคำพูด แล้ว `sudo systemctl restart facility`

### 2.5 ให้ระบบรันเองตลอด (systemd)

สร้าง `/etc/systemd/system/facility.service`

```ini
[Unit]
Description=Janitorial Management Dashboard
After=network.target mysql.service

[Service]
User=facility
WorkingDirectory=/opt/facility/app
EnvironmentFile=/etc/facility/facility.env
ExecStart=/usr/bin/dotnet /opt/facility/app/JanitorialManagementDashboard.Api.dll
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

ถ้า log ว่างเปล่า (ระบบล้มก่อนเขียนอะไร) ดูบรรทัดของ systemd เอง: `sudo systemctl status facility` หรือ `sudo journalctl --namespace=+facility -u facility` (`+` รวม journal หลักของ systemd เข้าไปด้วย)

ดูเฉพาะเหตุการณ์ด้านความปลอดภัย: `sudo journalctl --namespace=facility -u facility | grep JanitorialManagementDashboard.Security`

ทุกบรรทัดขึ้นต้นด้วยระดับและหมวด เช่น `[WRN] JanitorialManagementDashboard.Security: Login failed: ...` ถ้าระบบเริ่มไม่ได้จะมีบรรทัด `[FTL] Program: Stopped on an error ...` ตามด้วยสาเหตุ ระบบเขียน log ผ่าน Serilog (facility-0103) ระดับ log ตั้งที่ `Serilog:MinimumLevel` ใน `appsettings.json` หรือตัวแปร `Serilog__MinimumLevel__Default` ในไฟล์ค่า ส่วน `Logging__LogLevel__*` ไม่มีผลแล้ว ห้ามเพิ่มที่ส่ง log ออกนอก server (เช่น Discord) โดยไม่มี ADR ใหม่

- `Login failed: employee 1000001, wrong secret, from 203.0.113.7` ใส่เบอร์ผิด (`account turned off` คือบัญชีถูกปิดใช้งาน) ท้ายบรรทัดคือ IP ที่ส่งมา (IPv6 เขียนเป็นช่วง /64)
- `Login failed: unknown account (employee login), from ...` รหัสพนักงานที่พิมพ์ไม่มีในระบบ ระบบไม่เขียนสิ่งที่พิมพ์ เพราะอาจเป็นเบอร์โทรที่พิมพ์สลับช่อง
- `Login refused while locked: ..., from ...` ชื่อนี้ผิด 5 ครั้งจาก IP นั้นแล้ว ถูกล็อก 15 นาทีเฉพาะจาก IP นั้น หรือ IP นั้นชนเพดานแล้ว ทุกครั้งที่ลองระหว่างล็อกขึ้นหนึ่งบรรทัด (facility-0104)
- `Login place locked after too many failures: 203.0.113.7` IP นี้ login ผิดรวมทุกชื่อครบ 20 ครั้งใน 15 นาที จึง login ไม่ได้ 15 นาที ถ้าเห็นบ่อยจาก IP เดิม บล็อก IP นั้นที่ firewall ได้
- `Refresh token reused, session ended: ...` มีคนใช้ token เดิมซ้ำ ระบบให้บัญชีนั้นเข้าระบบใหม่ (facility-0015)

log ไม่มีเบอร์โทร รหัสผ่าน token หรือพิกัด (facility-0097)

### 2.6 https (Caddy)

แก้ `/etc/caddy/Caddyfile` เป็น

```
facility.example.com {
    reverse_proxy localhost:5001
}
```

แล้ว `sudo systemctl reload caddy` เปิด `https://facility.example.com` ต้องเห็นหน้าเข้าระบบ

เช็ก security header ที่ระบบส่งเอง (facility-0106) ด้วย `curl -sI https://facility.example.com/login/admin` ต้องเห็น `content-security-policy`, `x-frame-options: DENY`, `x-content-type-options: nosniff`, `referrer-policy: no-referrer`, `permissions-policy` และ `strict-transport-security: max-age=31536000` ต้อง curl ผ่านโดเมนจริง ถ้า curl ไปที่ `localhost:5001` จะไม่เห็น `strict-transport-security` เป็นเรื่องปกติ ถ้าผ่านโดเมนแล้วยังไม่มี แปลว่าระบบไม่รู้ว่าคำขอมาทาง https หรือระบบไม่ได้รันเป็น `Production` ลอง login ผิดหนึ่งครั้งด้วยบัญชีทดสอบ แล้วดู journal ว่าท้ายบรรทัด `from ...` เป็น IP ของเครื่องผู้ใช้ ไม่ใช่ loopback ของ Caddy (facility-0104) เพื่อตรวจว่า forwarding ทำงานจริง จากนั้นทำข้อ 3

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

ก่อนเริ่มแต่ละรอบ ให้ย้ายชุดสำรอง `.previous` จากรอบก่อนออกไปเก็บเป็นชุดที่มีวันที่กำกับ ทั้งโฟลเดอร์โปรแกรม สคริปต์ และไฟล์ค่าตั้งด้านล่าง เพื่อไม่เขียนทับชุดที่ยังต้องใช้ถอยกลับ ไฟล์ environment มีรหัสผ่าน ต้องเก็บในที่ที่ root อ่านได้เท่านั้น

1. สร้างไฟล์โปรแกรมบนเครื่องที่มีโค้ดด้วยคำสั่ง build/publish ในข้อ 2.3
2. หยุดระบบ สำรองฐานข้อมูลตามข้อ 2.7 และสำรองค่าตั้งเดิมก่อนแก้ไข:

   ```bash
   sudo systemctl stop facility
   sudo /etc/cron.daily/facility-backup
   sudo install -m 600 /etc/systemd/system/facility.service /etc/systemd/system/facility.service.previous
   sudo install -m 600 /etc/facility/facility.env /etc/facility/facility.env.previous
   ```

   เก็บชื่อไฟล์ backup ฐานข้อมูลจากรอบนี้ไว้คู่กับชุด `.previous` เพื่อใช้คืนข้อมูลชุดเดียวกัน
3. ก่อนทับสคริปต์ ถ้าใช้ `/opt/facility/janitorial.sh` อยู่แล้ว ให้สำรองด้วย `sudo install -m 755 /opt/facility/janitorial.sh /opt/facility/janitorial.sh.previous` หากเป็นรุ่นเก่าที่ใช้ `/opt/facility/facility.sh` ให้เก็บไฟล์นั้นไว้ จากนั้นเก็บโฟลเดอร์เก่าไว้ถอยกลับด้วย `sudo mv /opt/facility/app /opt/facility/app.previous` แล้วคัดลอกโฟลเดอร์ publish ใหม่ไปเป็น `/opt/facility/app` และ `scripts/janitorial.sh` ไปเป็น `/opt/facility/janitorial.sh` ตั้ง `chown`/`chmod` ตามข้อ 2.3 อีกครั้ง โดยไม่ต้องสร้างผู้ใช้ `facility` ซ้ำ
4. สำหรับการอัปเกรดจากรุ่นที่ใช้ `FacilityRealtime.Api.dll` ให้แก้ไฟล์ `/etc/systemd/system/facility.service` ด้วย `sudo nano /etc/systemd/system/facility.service` ให้บรรทัดเริ่มโปรแกรมเป็น:

   ```ini
   ExecStart=/usr/bin/dotnet /opt/facility/app/JanitorialManagementDashboard.Api.dll
   ```

   แก้ไฟล์ `/etc/facility/facility.env` ด้วย `sudo nano /etc/facility/facility.env` ให้ตัวรันสคริปต์ใช้ DLL เดียวกัน:

   ```bash
   FACILITY_APP_DLL=/opt/facility/app/JanitorialManagementDashboard.Api.dll
   ```

   ตรวจว่ามี DLL นี้ใน `/opt/facility/app` จริง และคงค่าฐานข้อมูลกับ JWT เดิมไว้ ใช้ไฟล์ environment เดิมต่อ ไม่ต้องรัน `init` ใหม่ ไฟล์นี้ต้องมีสิทธิ์ `600` ตามข้อ 2.4; `FACILITY_APP_DLL` มีผลกับคำสั่ง `start`/`reset-admin` ของ `janitorial.sh` ส่วน systemd ใช้ `ExecStart` จึงต้องแก้ทั้งสองจุด หากเป็นรุ่นชื่อใหม่อยู่แล้ว ให้ตรวจว่าสองค่านี้ตรงกับไฟล์ที่จะรัน
5. โหลดค่าตั้ง service ใหม่ก่อนเริ่มระบบ แล้วตรวจสถานะและ log:

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl start facility
   sudo systemctl status facility
   sudo journalctl --namespace=facility -u facility -n 50
   ```

   ระบบ migrate ฐานข้อมูลเองตอนเริ่ม ตรวจว่าไม่มีข้อผิดพลาดและเปิดหน้าเข้าระบบผ่าน domain ตามข้อ 2.6 ได้

#### ถ้าต้องถอยกลับ

1. `sudo systemctl stop facility` แล้วย้ายโฟลเดอร์ `/opt/facility/app` รุ่นที่มีปัญหาออกไปเก็บแยก ย้าย `/opt/facility/app.previous` กลับเป็น `/opt/facility/app`
2. คืนไฟล์ค่าตั้งจากชุดเดียวกับโปรแกรมเก่า:

   ```bash
   sudo install -m 600 /etc/systemd/system/facility.service.previous /etc/systemd/system/facility.service
   sudo install -m 600 /etc/facility/facility.env.previous /etc/facility/facility.env
   ```

   ตรวจว่า `ExecStart` และ `FACILITY_APP_DLL` ชี้ไปที่ DLL ที่มีอยู่ในโฟลเดอร์ที่คืนแล้ว หากชุดเดิมใช้ `FacilityRealtime.Api.dll` ให้คืนชื่อนั้นด้วย หากมี `janitorial.sh.previous` จากรอบนี้ ให้คืนด้วย `sudo install -m 755 /opt/facility/janitorial.sh.previous /opt/facility/janitorial.sh` หากชุดเดิมใช้ `facility.sh` ให้ใช้ไฟล์เดิมที่เก็บไว้
3. กู้ฐานข้อมูลจาก backup ก่อนอัปเกรดของรอบเดียวกันตามข้อ 2.7 ขณะที่ service ยังหยุดอยู่
4. รัน `sudo systemctl daemon-reload` แล้ว `sudo systemctl start facility` ตรวจสถานะและ log ตามขั้นตอนอัปเกรดอีกครั้ง

### 2.9 ทดลองบนเครื่องตัวเองก่อน (ไม่มี server)

ทำข้อ 2.2 ก่อน โดยใช้ฐานข้อมูลทดลองที่ชื่อเข้ากับ `facility_*_test` และผู้ใช้ MySQL ของมันเอง

ทำ `npm --prefix frontend ci` และ `npm --prefix frontend run build:api` (สองคำสั่งแรกของข้อ 2.3) ก่อน เพื่อให้มีหน้าเว็บ แล้ว

```bash
scripts/janitorial.sh init ~/.facility/trial.env
scripts/janitorial.sh start ~/.facility/trial.env
```

ตอน `init` คำถามที่อยู่ dll ให้เว้นว่าง `start` รันค้างอยู่ในหน้าต่างนั้น จึงเปิด Terminal หน้าต่างที่สองแล้วรัน

```bash
cloudflared tunnel --url http://localhost:5001
```

ใช้ฐานข้อมูลทดลองและข้อมูลสมมติเท่านั้น ลิงก์ `trycloudflare.com` เปลี่ยนทุกครั้งที่เปิด ป้าย QR ที่พิมพ์จากลิงก์นี้ใช้ได้แค่ครั้งนั้น

## 3. สร้างแอดมินคนแรก (ครั้งเดียว ตอน deploy ครั้งแรก)

ฐานข้อมูลใหม่ยังไม่มีบัญชีใดเลย และบัญชีทุกใบเพิ่มได้จากหน้า "บัญชีผู้ใช้" ซึ่งต้องเข้าระบบเป็นแอดมินก่อน แอดมินคนแรกจึงสร้างจากค่าใน server

1. ค่า 3 ตัวนี้อยู่ในไฟล์ค่าแล้ว ถ้าสร้างด้วย `janitorial.sh init` (ข้อ 2.4)
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
   sudo /opt/facility/janitorial.sh reset-admin /etc/facility/facility.env <ชื่อผู้ใช้แอดมิน>
   ```
   สคริปต์ใช้ไฟล์ค่าเดียวกับระบบ จึงไปที่ฐานข้อมูลเดียวกันเสมอ ถ้าทดลองบนเครื่องตัวเอง ใช้ `scripts/janitorial.sh reset-admin ~/.facility/trial.env <ชื่อผู้ใช้แอดมิน>`
2. คำสั่งแสดง `คำสั่งนี้จะเปลี่ยนข้อมูลในฐานข้อมูล <server>:<port> / <database>` เช่น `localhost:3306 / facility` ตรวจว่าถูกตัว แล้วมันถามว่า `ถ้าถูกต้อง พิมพ์ yes แล้วกด Enter: ` พิมพ์ `yes` ถ้าพิมพ์อย่างอื่น คำสั่งพิมพ์ `ไม่ได้พิมพ์ yes ไม่ได้เปลี่ยนอะไร` แล้วจบด้วย exit code 1 โดยไม่แตะฐานข้อมูล
3. ถ้าชื่อนั้นเป็นรหัสพนักงานของแม่บ้านหรือหัวหน้า คำสั่งไม่ทำอะไรและบอกเหตุผล โดยไม่ถามรหัสผ่าน
4. คำสั่งถามรหัสผ่านใหม่สองครั้ง ตัวอักษรที่พิมพ์ไม่แสดงบนจอ คำสั่งไม่รับรหัสในบรรทัดคำสั่ง เพราะจะค้างใน history
   - ชื่อนั้นเป็นแอดมินอยู่แล้ว: ตั้งรหัสใหม่ เปิดบัญชีถ้าถูกปิด และทุกเครื่องที่เคยเข้าระบบด้วยบัญชีนี้ต้องเข้าใหม่
   - ยังไม่มีชื่อนี้: สร้างแอดมินใหม่ คำสั่งถามชื่อที่แสดงก่อน
5. คำสั่งจบด้วย exit code 0 เมื่อเสร็จ 1 เมื่อไม่ได้ทำ (ข้อความบอกเหตุผล) และ 2 เมื่อพิมพ์คำสั่งผิด ถ้าขึ้นข้อผิดพลาดว่าต่อฐานข้อมูลไม่ได้ ให้ตรวจ `ConnectionStrings__DefaultConnection` ในไฟล์ค่า
6. เข้าระบบแล้วเปลี่ยนรหัสของตัวเองที่หน้า "บัญชีผู้ใช้" ทันที

หมายเหตุ

- รันได้ขณะที่ระบบทำงานอยู่ ไม่ต้องหยุดระบบ
- ถ้าเพิ่งใส่รหัสผิด 5 ครั้งจนถูกล็อก การล็อกผูกกับ IP ที่ใช้ (facility-0104) ลองจากเน็ตอื่น (เช่น เปลี่ยนจาก Wi-Fi เป็นเน็ตมือถือ) ได้เลย หรือรอ 15 นาที หรือรีสตาร์ตระบบ เพราะการล็อกอยู่ในหน่วยความจำของระบบที่รันอยู่
- หน้า "ประวัติการแก้ไขของแอดมิน" มีแถวที่ผู้ทำคือ "ระบบ" ทุกครั้งที่สร้างหรือกู้คืนแอดมินด้วยวิธีในเอกสารนี้
- ใครเข้า server ได้ก็ใช้คำสั่งนี้ได้ จึงต้องจำกัดคนที่เข้า server ได้
- migration `SystemAuditActor` ย้อนกลับ (rollback) ไม่ได้ หลังจากมีแถวในประวัติที่ผู้ทำคือ "ระบบ" แล้ว ถ้าต้องถอยไปเวอร์ชันก่อนหน้า ให้กู้ฐานข้อมูลจาก backup แทน

## 5. ให้มีแอดมินที่เปิดใช้งานอย่างน้อย 2 คนเสมอ

ถ้าแอดมินคนหนึ่งลืมรหัสหรือลาออก อีกคนรีเซ็ตรหัสหรือปิดบัญชีให้ได้ในหน้า "บัญชีผู้ใช้" โดยไม่ต้องเข้า server คำสั่ง `reset-admin` จึงแทบไม่ต้องใช้

## 6. เทส end-to-end (`npm run e2e`)

เปิด Chromium ให้เห็นจริงบน Mac แล้วกดหน้าจอผ่าน API/MySQL จริง (facility-0094, amended by facility-0102) ค่าเริ่มต้นช้าลง350ms ต่อการกด

```bash
cd frontend
npm run e2e -- --dataset large
```

ต้องมี Python3, .NET10, Node/npm, MySQL ในเครื่อง และ desktop session ถ้า MySQL root มีรหัส ให้ตั้ง `E2E_MYSQL_PASSWORD` ใน shell ก่อนรัน ระบบสร้างฐานข้อมูลและผู้ใช้ใหม่เฉพาะรอบ ลบเฉพาะของรอบนั้นเมื่อจบ ไม่แตะฐานข้อมูลเดิมและไม่ลบ `wwwroot` ที่ใช้ทดสอบด้วยมือ

เลือก `baseline` (ค่าเริ่มต้น), `small`, `medium`, `large` ได้ รายงาน/รูป/trace อยู่ที่ `.run/e2e/<รอบ>/` และแสดง path หลังรัน ถ้าถูก SIGKILL/reboot ให้ตรวจว่าโปรเซสเดิมหยุดแล้ว และล้างด้วย `npm run e2e:clean -- /absolute/path/to/.run/e2e/<รอบ>` ระบุรอบให้ชัดเจน คำสั่งจะตรวจเจ้าของก่อนลบ

รายละเอียดคำสั่ง ข้อมูลจำลอง ข้อจำกัด และการเปิดรายงาน: [E2E run guide](testing/e2e.md)

### สิ่งที่เทสนี้จับไม่ได้

WebKit/Firefox รันได้ด้วย `E2E_BROWSER=webkit` หรือ `E2E_BROWSER=firefox` แต่ยังต้องเช็ก Safari บน iPhone จริง (รวมปฏิเสธแล้วเปิด Location กลับ), การเลื่อนหน้าที่ขึ้นกับแถบเครื่องมือมือถือ, GPS/สัญญาณเครือข่ายจริง, กล้องอ่านป้าย และเครื่องพิมพ์จริง ชุด E2E ตรวจหน้า D5/ขนาดกระดาษ/คำสั่งพิมพ์แล้ว แต่ไม่ได้สั่งเครื่องพิมพ์กระดาษจริง


## 7. หลังขึ้นระบบจริง

### ทดลองใช้ในพื้นที่เดียว

เริ่มจากพื้นที่เดียว เช่น ชั้น 1 ของตึกสำนักงาน มี QR ลงเวลา 1 ป้าย และ QR ที่ห้องทำความสะอาดอย่างน้อย 1 ห้อง ใช้แม่บ้านประจำพื้นที่และหัวหน้าที่ตรวจพื้นที่นั้น ถ้าพื้นที่ทำงานสองกะให้ทดลองทั้งสองกะ แอดมินกรอกข้อมูลผ่านหน้าเว็บได้ตามปกติ แล้วติดและบันทึกตำแหน่งของป้ายจริง ดู [ขอบเขตทดลองใช้](adr/facility-0043-pilot-scope-one-area.md)

- ใช้มือถือและเน็ตที่ใช้ทำงานจริง ลองสแกนป้ายที่ติดไว้และดูว่าระบบขึ้นเตือน **“ไกลป้าย”** หรือไม่เมื่อยืนถูกที่ บน iPhone ให้ลองปฏิเสธการอ่านตำแหน่ง แล้วเปิดสิทธิ์กลับและลองใหม่ด้วย
- ลองบันทึก **“เข้างาน” “ออกพัก” “กลับจากพัก” “เลิกงาน”** แล้วดูว่าแอดมินเห็นครบใน **“การเข้างาน”**
- ลองส่งงานให้หัวหน้าตรวจ สั่งแก้ ให้แม่บ้านส่งงานแก้ และตรวจซ้ำ ลองปล่อยให้งานเกินเวลาส่งเพื่อดูว่า **“สถานะจุด”** ขึ้น **“เลยเวลา”** ตรงตามเวลาที่ตั้งไว้

### ตรวจงานลบและสรุปข้อมูลเก่า

ระบบลบพิกัดจากรายการสแกนเมื่อครบ 90 วัน ลบรายการลงเวลาและสแกนที่ถูกปฏิเสธเมื่อครบ 1 ปี และสรุปงานส่งกับงานตรวจก่อนลบรายการรายครั้งตามเดือนที่ครบเกณฑ์ รวมงานนี้ใน PR #147 แล้ว ทีม IT ยังต้องตรวจรอบแรกและการทำงานตามเวลาบนเครื่องจริง ระบบทำงานเมื่อเริ่มโปรแกรมและทุกคืนเวลา 02:00 น. ตามเวลาไทย ถ้าล้มเหลวจะลองใหม่หลัง 15 นาที ส่วนบันทึกเหตุการณ์บนเครื่องและสำเนาฐานข้อมูลใช้ระยะเก็บแยกตามข้อ 2.5 และ 2.7 ดู [รายละเอียดการเก็บข้อมูล](data-retention.md)

ตรวจผลรอบแรกด้วย `sudo journalctl --namespace=facility -u facility | grep 'Retention'` จะเห็น `Retention completed` พร้อมจำนวนที่จัดการ ถ้าเห็น `Retention failed` ระบบยังให้บริการและจะลองใหม่ตาม `RetryMinutes` ให้ตรวจการเชื่อมต่อ/สิทธิ์ของฐานข้อมูลและ log ของ host งานนี้ต้องมีสิทธิ์แก้ GPS และลบข้อมูลหมดอายุรวมประวัติที่ผูกอยู่ แต่ไม่มีปุ่มหรือ endpoint ให้ผู้ใช้ลบประวัติเอง

ตามบันทึกวันที่ 3 พ.ย. 2026 เจ้าของโครงการนำงานประกาศ PDPA ใบยินยอม และการประสาน HR ออกจากขอบเขตส่งมอบ ระยะเก็บข้อมูลตามเดิม ส่วนคนดูแลและกำหนดลบไฟล์ Excel ที่ดาวน์โหลดยังไม่ได้ตกลง ดู [บันทึกการตัดสินใจเรื่องข้อมูล](adr/facility-0055-data-retention-and-pdpa-before-production.md)
