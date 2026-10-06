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
สถานะของ Service Point ที่แสดงบนการ์ดใน Dashboard คำนวณสดจาก Scan Record ล่าสุด, Cleaning Interval และ Working Hours มี 4 ค่า ตามลำดับความสำคัญ: Issue (แดง), Off Hours (เทา), Overdue (ส้ม), Normal (เขียว) เป็นคนละอย่างกับ Cleaning Status ซึ่งเป็นค่าของการสแกนแต่ละครั้ง
_Avoid_: Card Color, Point State, Alert Level

**Cleaning Interval**:
รอบทำความสะอาดของ Service Point หนึ่งจุด หน่วยเป็นนาที ตั้งแยกแต่ละจุด ใช้คำนวณเวลาครบรอบ = max(สแกนล่าสุด, เวลาเปิดของวันนี้) + Cleaning Interval
_Avoid_: Frequency, SLA, Schedule

**Overdue**:
Point Status ที่เวลาปัจจุบันเลยเวลาครบรอบของจุดนั้น ไม่มีเวลาผ่อนผัน
_Avoid_: Late, Missed, Expired

**Working Hours**:
ช่วงเวลาทำงานค่าเดียวทั้งระบบ (ค่าเริ่มต้น 08:00–17:00, Asia/Bangkok) ใช้กำหนดว่านับรอบเมื่อไหร่ นอกช่วงนี้ Point Status เป็น Off Hours ยกเว้นจุดที่มี Issue
_Avoid_: Shift, Opening Time, Business Hours

**Persistent Session**:
สถานะการเข้าสู่ระบบที่ค้างไว้ในเบราว์เซอร์ของผู้ใช้ (เช่น มือถือผู้สแกน) ทำให้ใช้งานต่อเนื่องได้โดยไม่ต้องกรอกรหัสผ่านซ้ำ จนกว่าจะกด Logout, ไม่ได้ใช้งานนาน 30 วัน หรือบัญชีถูกปิด
_Avoid_: Cookie, Remember Token, Keep-Alive

**Bound Device**:
เบราว์เซอร์บนมือถือเครื่องเดียวที่ Cleaner Account หรือ Supervisor Account ผูกไว้ตอน login ครั้งแรก เครื่องอื่น login บัญชีนั้นไม่ได้จนกว่า Admin จะปลดเครื่อง
_Avoid_: Registered Phone, Device Lock, Trusted Device

**QR Token**:
ค่ารหัสเฉพาะ (UUID) ประจำจุดบริการที่ฝังอยู่ใน QR Code เพื่อใช้เปิดหน้าเว็บสแกน สามารถกดสร้างใหม่ได้เมื่อป้ายชำรุด โดยไม่ต้องเปลี่ยนรหัสจุดเดิม
_Avoid_: Secret Key, QR String, Barcode Value

**Shift Check-In**:
การบันทึกเวลาเข้างานของ Cleaner Account ในแต่ละกะการทำงาน (Day: 07:00–19:00, Night: 19:00–07:00) สแกนที่ Check-In Sign ก่อนเริ่มงาน เป็นเงื่อนไขจำเป็นก่อนสแกน Service Point ได้ สแกนซ้ำในกะเดียวยึดเวลาแรก (First-in wins)
_Avoid_: Time Attendance, Clock In, Scan Record

**Check-In Sign**:
ป้ายเฉพาะสำหรับสแกนเข้างาน ติดหน้าประตูทางเข้าของแต่ละตึก ตึกละป้าย แม่บ้านสแกนที่ป้ายของตึกที่ตนรับผิดชอบ แยกจาก QR Sign ของ Service Point
_Avoid_: Gate QR, Station Sign, Attendance QR

**Attendance Status**:
สถานะการเข้างานของ Cleaner Account ในกะปัจจุบัน มี 2 ค่าหลักคือ เข้างานแล้ว (Present) และ ยังไม่เข้างาน (Absent / Not Checked In) แสดงบน Dashboard ของหัวหน้า
_Avoid_: Worker Status, Shift State, Member Attendance

**Attendance Board**:
หน้าจอแสดงรายงานและรายชื่อการเข้างานของ Cleaner Account ทั้ง 170 คนแบบเรียลไทม์ เป็นแท็บย่อยบน Dashboard ให้ Admin Account ตรวจสอบอัตรากำลังและคนขาดในแต่ละกะ
_Avoid_: Check-In Page, Attendance View, Staff Table

**Area Capacity**:
จำนวน Cleaner Account ที่ได้รับมอบหมายประจำ ณ จุดบริการหนึ่งจุด พร้อมสถานะว่าเข้างานแล้วกี่คนและขาดกี่คน (เช่น จุดที่ 1 ประจำ 10 คน เข้างาน 7 คน)
_Avoid_: Staffing Level, Headcount Quota, Worker Ratio

**GPS Geofencing**:
การเทียบพิกัดมือถือตอนสแกน Check-In Sign กับพิกัดของป้ายนั้น ถ้าอยู่นอกรัศมีจะได้ Geofence Flag ไม่ใช่การปฏิเสธ
_Avoid_: Location Spoofing, Area Lock, GPS Tracking

**Geofence Flag**:
ธงบนการลงเวลาที่ระบบรับไว้แล้ว แต่พิกัดอยู่นอกรัศมีหรือคลาดเคลื่อนเกินรัศมีของ Check-In Sign หรือเป็นป้ายของตึกที่ไม่ใช่ตึกที่ตนรับผิดชอบ (ถ้าปิด Location จะลงเวลาไม่ได้เลย จึงไม่เกิดธง) แสดงบน Dashboard ให้หัวหน้าเรียกคุย
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

**Assigned Zone**:
พื้นที่เป้าหมาย (ระดับ Building หรือ Floor) ที่ Cleaner Account ได้รับมอบหมายให้ปฏิบัติงานประจำ
_Avoid_: Work Area, Target Station, Duty Location

**Wrong-Zone Flag**:
ธงแจ้งเตือนบน Scan Record เมื่อ Cleaner Account สแกน ณ จุดบริการที่อยู่นอกพื้นที่รับผิดชอบของตน โดยระบบยอมรับการบันทึกเพื่อความยืดหยุ่นหน้างาน แต่ขึ้นเตือนบน Dashboard ให้หัวหน้างานรับทราบ
_Avoid_: Unauthorized Scan, Cross-Building Error, Invalid Location

**Attendance Event**:
รายการลงเวลาเข้า-ออกของ Cleaner Account ในรอบวัน มี 4 จังหวะ: เข้างาน (Shift-In), ออกพักเบรก (Break-Out), กลับจากเบรก (Break-In), และ เลิกงาน (Shift-Out) รวมเฉลี่ยประมาณ 4 ครั้ง/คน/วัน (รวม ~680 เรคคอร์ด/วัน สำหรับแม่บ้าน 170 คน)
_Avoid_: Punch Clock, Time Card, Clock Event

**Point Schedule Type**:
รูปแบบรอบเวลาการดูแลของ Service Point แบ่งเป็น 2 แบบ: Interval-based (นับถอยหลังตาม Cleaning Interval เหมาะกับห้องน้ำ/แคนทีน) และ Shift-based (ทำ 1 ครั้งต่อกะ รีเซ็ตสถานะอัตโนมัติเมื่อตัดกะ 07:00 และ 19:00 เหมาะกับทางเดิน/ออฟฟิศ)
_Avoid_: Reset Mode, Schedule Mode, Frequency Type

**Pilot Scope**:
ขอบเขตการนำร่องในเฟสแรก ประกอบด้วย อาคาร A (2 ชั้น), จุดบริการตัวแทน 10–12 จุด (ผสมห้องน้ำและทางเดิน), แม่บ้านกลุ่มนำร่อง 10–15 คน และหัวหน้างาน 1–2 คน เพื่อพิสูจน์ Paper Flow และระบบจริง ก่อนขยายเต็มรูปแบบ 6 อาคาร (170 คน)
_Avoid_: Test Phase, Trial Run, MVP Boundary
