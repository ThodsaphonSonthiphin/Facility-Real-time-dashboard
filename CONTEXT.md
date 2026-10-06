# Facility Real-time Dashboard Context

ระบบติดตามและแสดงผลการทำความสะอาดและดูแลสิ่งอำนวยความสะดวกในโรงงานแบบ Real-time ผ่านการสแกน QR Code

## Language

**Scan Record**:
รายการบันทึกธุรกรรมการสแกน QR ณ จุดบริการ 1 ครั้ง ประกอบด้วย เวลาที่สแกน, รหัสจุดบริการ, ผู้สแกน, สถานะการทำความสะอาด (Normal หรือ Issue), แท็กประเภทปัญหา และหมายเหตุ
_Avoid_: Check-in, Stamp, Log

**Service Point**:
จุดบริการหรือตำแหน่งทางกายภาพที่มีป้าย QR ติดตั้งอยู่สำหรับงานทำความสะอาด เช่น แคนทีน โซน A หรือห้องน้ำชั้น 1
_Avoid_: Checkpoint, Station, Location ID

**QR Sign**:
ป้ายกระดาษที่พิมพ์และติดไว้ที่ Service Point มี QR Code ของ QR Token ปัจจุบันของจุดนั้น และมีพิกัดกับรัศมีของตัวเองไว้เทียบกับพิกัดมือถือตอนสแกน เมื่อ Admin ออก QR Token ใหม่ ป้ายเดิมสแกนไม่ได้ทันทีและต้องพิมพ์ป้ายใหม่ไปติดแทน
_Avoid_: Sticker, QR Image, Label

**Flagged Scan Record**:
Scan Record ที่ระบบรับไว้แต่ติดธงว่าอาจไม่ได้สแกนที่ Service Point จริง เช่น พิกัดมือถืออยู่นอกรัศมีของ QR Sign หรือคลาดเคลื่อนเกินรัศมี หรือเวลาสแกนผิดปกติ
_Avoid_: Fake Scan, Rejected Scan, Suspicious Scan

**Deactivated Service Point**:
Service Point ที่ Admin ปิดใช้งาน หายจาก Dashboard และ QR Sign ของจุดนั้นสแกนไม่ได้ แต่ประวัติ Scan Record ยังอยู่ เพราะระบบไม่ลบจุด Admin เปิดใช้งานกลับได้
_Avoid_: Deleted Point, Archived Point, Hidden Point

**Cleaning Status**:
สถานะผลการทำความสะอาดของจุดบริการ มีค่าหลักเป็น Normal (เรียบร้อย/ปกติ) และ Issue (พบปัญหาที่ต้องแจ้งเตือนไปยัง Dashboard)
_Avoid_: State, Flag, Inspection Result

**Cleaner Account**:
บัญชีผู้ใช้งานระดับพนักงานทำความสะอาดที่สร้างขึ้นล่วงหน้าโดยผู้ดูแลระบบ (Admin) ประกอบด้วย Username, Password Hash, Display Name และสิทธิ์การใช้งาน
_Avoid_: User Profile, Member, Employee Profile

**Admin Account**:
บัญชีผู้ใช้งานระดับผู้ดูแลระบบ มีสิทธิ์จัดการ Service Point, ออก QR Token ใหม่ และจัดการบัญชีผู้ใช้ ซึ่ง Cleaner Account ทำไม่ได้
_Avoid_: Superuser, Root, Manager

**Deactivated Account**:
บัญชี (Cleaner หรือ Admin) ที่ Admin ปิดใช้งาน login ไม่ได้และ Session ที่ค้างอยู่หลุดภายในไม่กี่นาที แต่ประวัติ Scan Record และชื่อยังคงอยู่ เพราะระบบไม่ลบบัญชี Admin เปิดใช้งานกลับได้
_Avoid_: Deleted User, Banned, Archived

**Point Status**:
สถานะของ Service Point ที่แสดงบนการ์ดใน Dashboard คำนวณสดจาก Round Time ของจุด, Shift Pattern ของ Area, Scan Record และ Inspection Record ของรอบปัจจุบัน ตามลำดับความสำคัญ: ต้องแก้ไข, Overdue (เลยรอบ), รอตรวจ, ตรวจผ่าน, ยังไม่ทำ, Off Hours (นอกเวลา) ส่วน Issue ที่แจ้งตอนสแกนยังแสดงตาม facility-0005 เป็นคนละอย่างกับ Cleaning Status ซึ่งเป็นค่าของการสแกนแต่ละครั้ง (facility-0042)
_Avoid_: Card Color, Point State, Alert Level

**Round Time**:
เวลารอบทำความสะอาดตามนาฬิกาของ Service Point หนึ่งจุด ตั้งแยกแต่ละจุดและแยกกะเช้า/กะดึก เช่น 07:00 10:00 13:00 16:00 การส่งงานหลังเวลารอบนับเป็นงานของรอบนั้น (facility-0042)
_Avoid_: Cleaning Interval, Point Schedule Type, Frequency, SLA

**Overdue**:
Point Status ที่เวลาปัจจุบันเกิน Round Time ล่าสุด + ค่าผ่อนผัน แล้วรอบนั้นยังไม่มีการส่งงาน ค่าผ่อนผันเป็นค่าเดียวทั้งระบบ (ค่าเริ่มต้น 30 นาที รอพี่เลี้ยงยืนยัน)
_Avoid_: Late, Missed, Expired

**Shift Pattern**:
รูปแบบกะของ Area มี 2 แบบ: ทำ 2 กะ (เช้า 07:00–19:00 และดึก 19:00–07:00) หรือทำเฉพาะกะเช้า เช่น Office จุดของ Area ที่ทำเฉพาะกะเช้าเป็น Off Hours ตลอดกะดึก (facility-0040, 0042)
_Avoid_: Working Hours, Opening Time, Business Hours

**Persistent Session**:
สถานะการเข้าสู่ระบบที่ค้างไว้ในเบราว์เซอร์ของผู้ใช้ (เช่น มือถือผู้สแกน) ทำให้ใช้งานต่อเนื่องได้โดยไม่ต้องกรอกรหัสผ่านซ้ำ จนกว่าจะกด Logout, ไม่ได้ใช้งานนาน 30 วัน หรือบัญชีถูกปิด
_Avoid_: Cookie, Remember Token, Keep-Alive

**QR Token**:
ค่ารหัสเฉพาะ (UUID) ประจำจุดบริการที่ฝังอยู่ใน QR Code เพื่อใช้เปิดหน้าเว็บสแกน สามารถกดสร้างใหม่ได้เมื่อป้ายชำรุด โดยไม่ต้องเปลี่ยนรหัสจุดเดิม
_Avoid_: Secret Key, QR String, Barcode Value

**Shift Check-In**:
การบันทึกเวลาเข้างานของ Cleaner Account ในแต่ละกะการทำงาน (Day: 07:00–19:00, Night: 19:00–07:00) สแกนที่ Check-In Sign ก่อนเริ่มงาน เป็นเงื่อนไขจำเป็นก่อนสแกน Service Point ได้ สแกนซ้ำในกะเดียวยึดเวลาแรก (First-in wins)
_Avoid_: Time Attendance, Clock In, Scan Record

**Check-In Sign**:
ป้ายเฉพาะสำหรับลงเวลา ติดที่ Area, Area ละ 1 ป้าย แม่บ้านลงเวลาที่ป้ายของ Area ตัวเอง แยกจาก QR Sign ของ Service Point (facility-0040)
_Avoid_: Gate QR, Station Sign, Attendance QR

**Attendance Status**:
สถานะการเข้างานของ Cleaner Account ในกะปัจจุบัน มี 2 ค่าหลักคือ เข้างานแล้ว (Present) และ ยังไม่เข้างาน (Absent / Not Checked In) แสดงบน Attendance Board ซึ่ง Admin เท่านั้นที่เปิดได้
_Avoid_: Worker Status, Shift State, Member Attendance

**Attendance Board**:
หน้าจอแสดงรายงานและรายชื่อการเข้างานของ Cleaner Account ทั้ง 170 คนแบบเรียลไทม์ เป็นแท็บย่อยบน Dashboard ให้ Admin Account ตรวจสอบอัตรากำลังและคนขาดในแต่ละกะ
_Avoid_: Check-In Page, Attendance View, Staff Table

**GPS Geofencing**:
การเทียบพิกัดมือถือตอนสแกนทุกป้าย (Check-In Sign และ QR Sign ของ Service Point) กับพิกัดของป้ายนั้น ถ้าอยู่นอกรัศมีจะได้ Geofence Flag ไม่ใช่การปฏิเสธ (facility-0035, 0037)
_Avoid_: Location Spoofing, Area Lock, GPS Tracking

**Geofence Flag**:
ธงบนการลงเวลา การส่งงาน หรือการตรวจงานที่ระบบรับไว้แล้ว แต่พิกัดอยู่นอกรัศมีหรือคลาดเคลื่อนเกินรัศมีของป้าย (ถ้าปิด Location จะสแกนไม่ได้เลย จึงไม่เกิดธง) แสดงบน Dashboard ให้ Admin เรียกคุย การสแกนป้ายของ Area อื่นไม่ได้ธง แต่เป็น Blocked Scan
_Avoid_: GPS Error, Location Violation, Rejected Check-In

**Supervisor Account**:
บัญชีผู้ใช้งานระดับหัวหน้างาน มีสิทธิ์สแกนตรวจรับพื้นที่ ประเมินผลความสะอาด (สะอาด หรือ ต้องแก้ไข) และระบุข้อบกพร่อง ซึ่งแยกจากสิทธิ์ของ Cleaner Account และ Admin Account
_Avoid_: Inspector, Auditor, Team Lead

**Inspection Record**:
รายการบันทึกการตรวจพื้นที่โดย Supervisor ณ จุดบริการ ประกอบด้วย เวลาที่ตรวจ, ผู้ตรวจ, ผลการประเมิน (Cleaned/Passed หรือ Rework), และข้อความระบุข้อบกพร่อง
_Avoid_: Audit Log, Evaluation Record, Check Result

**Inspection Status**:
สถานะผลการตรวจรับงานของจุดบริการ มีค่า: Pending Inspection (รอตรวจ), Passed (สะอาด/ผ่าน), และ Rework (ต้องแก้ไข)
_Avoid_: Review State, Grade, Score

**Area**:
พื้นที่ที่แม่บ้านรับผิดชอบประจำทุกวัน กะละ 1 คน (Area ที่ทำ 2 กะมีแม่บ้านประจำ 2 คน, Area ที่ทำเฉพาะกะเช้ามี 1 คน) อยู่ในตึก 1 ตึก ทั้งโรงงานมี 70–72 Area แต่ละ Area มี Check-In Sign 1 ป้าย กับ Service Point N จุด และมี Shift Pattern ของตัวเอง Admin เป็นคนสร้างและแก้ไข (facility-0040, 0045)
_Avoid_: Assigned Zone, Zone, Work Area, Duty Location

**Blocked Scan**:
การสแกนป้ายของ Area ที่ไม่ใช่ Area ของตน และไม่มี Cover Assignment ระบบไม่บันทึกงาน แจ้งบนมือถือ และเก็บไว้ให้ Admin เห็นในรายการที่ขึ้นเตือน ตัดสินจาก QR Token ไม่ใช่ GPS (facility-0041)
_Avoid_: Wrong-Zone Flag, Unauthorized Scan, Rejected Scan

**Cover Assignment**:
การที่ Admin มอบหมายให้แม่บ้านทำแทน Area อื่นเฉพาะกะนั้น เช่น วันที่มีคนลา หมดกะแล้วหมดสิทธิ์เอง Scan Record ระหว่างนั้นบันทึกว่าเป็นการทำแทน (facility-0041)
_Avoid_: Reassignment, Transfer, Swap

**Attendance Event**:
รายการลงเวลาเข้า-ออกของ Cleaner Account ในรอบวัน มี 4 จังหวะ: เข้างาน (Shift-In), ออกพักเบรก (Break-Out), กลับจากเบรก (Break-In), และ เลิกงาน (Shift-Out) รวมเฉลี่ยประมาณ 4 ครั้ง/คน/วัน (รวม ~680 เรคคอร์ด/วัน สำหรับแม่บ้าน 170 คน)
_Avoid_: Punch Clock, Time Card, Clock Event

**Pilot Scope**:
ขอบเขตการนำร่องในเฟสแรก คือ 1 Area มี Check-In Sign 1 ป้าย และ Service Point อย่างน้อย 1 จุด ผู้ใช้คือแม่บ้านประจำ Area นั้น (ทั้ง 2 กะถ้า Area ทำ 2 กะ) กับหัวหน้างานที่ตรวจ เพื่อพิสูจน์ระบบจริงก่อนขยายไปครบ 70–72 Area (facility-0043)
_Avoid_: Test Phase, Trial Run, MVP Boundary
