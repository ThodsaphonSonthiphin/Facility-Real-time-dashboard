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
Scan Record ที่ระบบรับไว้แต่ติดธงว่าอาจไม่ได้สแกนที่ Service Point จริง คือพิกัดมือถืออยู่นอกรัศมีของ QR Sign หรือคลาดเคลื่อนเกินรัศมี (ไม่มีธงจากเวลาสแกน พี่เลี้ยงไม่เลือกกฎนี้)
_Avoid_: Fake Scan, Rejected Scan, Suspicious Scan

**Deactivated Service Point**:
Service Point ที่ Admin ปิดใช้งาน หายจาก Dashboard และ QR Sign ของจุดนั้นสแกนไม่ได้ แต่ประวัติ Scan Record ยังอยู่ เพราะระบบไม่ลบจุด Admin เปิดใช้งานกลับได้
_Avoid_: Deleted Point, Archived Point, Hidden Point

**Cleaning Status**:
สถานะผลการทำความสะอาดที่แม่บ้านเลือกตอนส่งงานแต่ละครั้ง มีค่าหลักเป็น Normal (เรียบร้อย/ปกติ) และ Issue (พบปัญหา เช่น ก๊อกรั่ว ของชำรุด) Issue แสดงเป็นป้ายแดงบนการ์ด ไม่ใช่ Point Status และหายเมื่อการส่งงานครั้งถัดไปเป็น Normal (facility-0046)
_Avoid_: State, Flag, Inspection Result

**Cleaner Account**:
บัญชีผู้ใช้งานระดับพนักงานทำความสะอาดที่สร้างขึ้นล่วงหน้าโดยผู้ดูแลระบบ (Admin) ประกอบด้วย รหัสพนักงาน, hash ของเบอร์โทรศัพท์, Display Name และสิทธิ์การใช้งาน login ด้วยรหัสพนักงาน + เบอร์โทรศัพท์ (facility-0054)
_Avoid_: User Profile, Member, Employee Profile

**Admin Account**:
บัญชีผู้ใช้งานระดับผู้ดูแลระบบ มีสิทธิ์จัดการ Service Point, ออก QR Token ใหม่ และจัดการบัญชีผู้ใช้ ซึ่ง Cleaner Account ทำไม่ได้ login ด้วย username + รหัสผ่าน (facility-0002, 0054)
_Avoid_: Superuser, Root, Manager

**Deactivated Account**:
บัญชี (Cleaner หรือ Admin) ที่ Admin ปิดใช้งาน login ไม่ได้และ Session ที่ค้างอยู่หลุดภายในไม่กี่นาที แต่ประวัติ Scan Record และชื่อยังคงอยู่ เพราะระบบไม่ลบบัญชี Admin เปิดใช้งานกลับได้
_Avoid_: Deleted User, Banned, Archived

**Point Status**:
สถานะของ Service Point ที่แสดงบนการ์ดใน Dashboard คำนวณสดจาก Round Window ปัจจุบันของจุด, Shift Pattern ของ Area, Scan Record และ Inspection Record ของรอบนั้น ตามลำดับความสำคัญ: ต้องแก้ไข, Overdue (เลยรอบ), รอตรวจ, ตรวจผ่าน, ยังไม่ทำ, ยังไม่ถึงรอบ (กะเริ่มแล้วแต่รอบแรกยังไม่เริ่ม), Off Hours (นอกเวลา) รอบใหม่เริ่มแล้วการ์ดเริ่มใหม่เสมอ ผลของรอบเก่า (ไม่ได้ตรวจ, ไม่ได้แก้, ไม่ได้ทำ) เก็บในประวัติ Issue ไม่ใช่ Point Status แต่เป็นป้ายแยก (facility-0046, 0047)
_Avoid_: Card Color, Point State, Alert Level

**Round Window**:
ช่วงเวลารอบทำความสะอาดของ Service Point หนึ่งจุด มีเวลาเริ่มและเวลาสิ้นสุด ตั้งแยกแต่ละจุดและแยกกะเช้า/กะดึก เช่น 07:00–09:00 และ 16:00–18:00 การส่งงานในช่วงนับเป็นงานของรอบนั้น (facility-0047)
_Avoid_: Round Time, Cleaning Interval, Point Schedule Type, Frequency, SLA

**Late Submission**:
การส่งงานหลัง Round Window จบ ของรอบที่ยังไม่มีการส่งงาน นับเป็นงานของรอบนั้นแต่บันทึกว่าส่งช้ากี่นาที (facility-0047)
_Avoid_: Overdue Scan, Missed Round

**Off-Round Submission**:
การส่งงานนอก Round Window ตอนที่รอบล่าสุดส่งไปแล้ว เช่น ส่ง 15:30 ก่อนรอบ 16:00–18:00 ระบบบันทึกไว้ แต่ไม่นับเป็นรอบใดและการ์ดไม่เปลี่ยน (facility-0047)
_Avoid_: Extra Scan, Early Scan

**Overdue**:
Point Status ที่เวลาปัจจุบันพ้นเวลาสิ้นสุดของ Round Window ปัจจุบันแล้ว และรอบนั้นยังไม่มีการส่งงาน ไม่มีค่าผ่อนผัน (facility-0047)
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
การยืนยันว่า Cleaner Account เข้าพื้นที่ (Area) ของตัวเองแล้วในกะนั้น (Day: 07:00–19:00, Night: 19:00–07:00) สแกนที่ Check-In Sign ก่อนเริ่มงาน เป็นเงื่อนไขจำเป็นก่อนสแกน Service Point ได้ สแกนซ้ำในกะเดียวยึดเวลาแรก (First-in wins) ไม่ใช่การลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน (facility-0050)
_Avoid_: Time Attendance, Clock In, Scan Record

**Check-In Sign**:
ป้ายเฉพาะสำหรับลงเวลา ติดที่ Area, Area ละ 1 ป้าย แม่บ้านลงเวลาที่ป้ายของ Area ตัวเอง แยกจาก QR Sign ของ Service Point (facility-0040)
_Avoid_: Gate QR, Station Sign, Attendance QR

**Attendance Status**:
สถานะการเข้างานของ Cleaner Account ในกะปัจจุบัน มี 2 ค่าหลักคือ เข้างานแล้ว (Present) และ ยังไม่เข้างาน (Absent / Not Checked In) แสดงบน Attendance Board ซึ่ง Admin เท่านั้นที่เปิดได้
_Avoid_: Worker Status, Shift State, Member Attendance

**Attendance Board**:
หน้าจอแสดงรายงานและรายชื่อการเข้างานของ Cleaner Account ทั้งหมด (ประมาณ 170 คน) แบบเรียลไทม์ เป็นแท็บย่อยบน Dashboard ให้ Admin Account ตรวจสอบอัตรากำลังและคนขาดในแต่ละกะ
_Avoid_: Check-In Page, Attendance View, Staff Table

**GPS Geofencing**:
การเทียบพิกัดมือถือตอนสแกนทุกป้าย (Check-In Sign และ QR Sign ของ Service Point) กับพิกัดของป้ายนั้น ถ้าอยู่นอกรัศมีจะได้ Geofence Flag ไม่ใช่การปฏิเสธ (facility-0035, 0037)
_Avoid_: Location Spoofing, Area Lock, GPS Tracking

**Geofence Flag**:
ธงบนการลงเวลา การส่งงาน หรือการตรวจงานที่ระบบรับไว้แล้ว แต่พิกัดอยู่นอกรัศมีหรือคลาดเคลื่อนเกินรัศมีของป้าย (ถ้าปิด Location จะสแกนไม่ได้เลย จึงไม่เกิดธง) แสดงบน Dashboard ให้ Admin เรียกคุย การสแกนป้ายของ Area อื่นไม่ได้ธง แต่เป็น Blocked Scan
_Avoid_: GPS Error, Location Violation, Rejected Check-In

**Supervisor Account**:
บัญชีผู้ใช้งานระดับหัวหน้างาน มีสิทธิ์สแกนตรวจรับพื้นที่ ประเมินผลความสะอาด (สะอาด หรือ ต้องแก้ไข) และระบุข้อบกพร่อง ซึ่งแยกจากสิทธิ์ของ Cleaner Account และ Admin Account หัวหน้าประจำตึก + กะ (ตึกละ 2 คน: เช้า 1 คน ดึก 1 คน) ตรวจได้ทุก Area ในตึกตัวเอง สแกนจุดในตึกอื่นเป็น Blocked Scan ไม่ต้องลงเวลา ตรวจกี่ครั้งก็ได้ แต่ตรวจได้เฉพาะงานที่ส่งในรอบปัจจุบัน login ด้วยรหัสพนักงาน + เบอร์โทรเหมือนแม่บ้าน (facility-0047, 0048, 0054)
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
การสแกนป้ายของ Area ที่ไม่ใช่ Area ของตน (แม่บ้าน) หรือจุดในตึกที่ไม่ใช่ตึกของตน (หัวหน้า) และไม่มี Cover Assignment ระบบไม่บันทึก แจ้งบนมือถือ และเก็บไว้ให้ Admin เห็นในรายการที่ขึ้นเตือน ตัดสินจาก QR Token ไม่ใช่ GPS (facility-0041, 0048)
_Avoid_: Wrong-Zone Flag, Unauthorized Scan, Rejected Scan

**Cover Assignment**:
การที่ Admin มอบหมายให้แม่บ้านทำแทน Area อื่นเฉพาะกะนั้น เช่น วันที่มีคนลา หมดกะแล้วหมดสิทธิ์เอง ไม่มีแม่บ้านสำรอง คนทำแทนจึงทำทั้ง Area ตัวเองและ Area ที่ได้รับมอบหมาย Scan Record ระหว่างนั้นบันทึกว่าเป็นการทำแทน ใช้กับหัวหน้าที่ตรวจแทนตึกอื่นด้วย (facility-0041, 0048)
_Avoid_: Reassignment, Transfer, Swap

**Attendance Event**:
รายการลงเวลาเข้า-ออกของ Cleaner Account ในรอบวัน มี 4 จังหวะ: เข้างาน (Shift-In), ออกพักเบรก (Break-Out), กลับจากเบรก (Break-In), และ เลิกงาน (Shift-Out) รวมเฉลี่ยประมาณ 4 ครั้ง/คน/วัน (รวม ~680 เรคคอร์ด/วัน สำหรับแม่บ้าน 170 คน)
_Avoid_: Punch Clock, Time Card, Clock Event

**Pilot Scope**:
ขอบเขตการนำร่องในเฟสแรก คือ 1 Area มี Check-In Sign 1 ป้าย และ Service Point อย่างน้อย 1 จุด ผู้ใช้คือแม่บ้านประจำ Area นั้น (ทั้ง 2 กะถ้า Area ทำ 2 กะ) กับหัวหน้างานที่ตรวจ เพื่อพิสูจน์ระบบจริงก่อนขยายไปครบ 70–72 Area (facility-0043)
_Avoid_: Test Phase, Trial Run, MVP Boundary

**Inspection Summary**:
หน้าบน Dashboard สำหรับ Admin เท่านั้น สรุปการตรวจของหัวหน้าแต่ละคนในช่วงเวลาที่เลือก: ตึกที่ประจำ, จำนวนครั้งที่ตรวจ, งานที่ส่งแล้วไม่ได้ตรวจ, จำนวนครั้งที่ให้แก้ไข และการตรวจที่ติดธง GPS และจุดที่ไม่ได้ตรวจเลย ระบบไม่ประเมินผลเอง แค่แสดงให้ Admin เห็น (facility-0048, 0055)
_Avoid_: Supervisor KPI, Audit Report, Scorecard

**Attendance Correction**:
การที่ Admin เพิ่มหรือแก้ Attendance Event ให้แม่บ้านที่ลงเวลาไม่ได้หรือลืมลงเวลา ต้องใส่เหตุผล ระบบเก็บค่าเดิมไว้และติดป้าย "แก้โดย Admin" แม่บ้านและหัวหน้าแก้เองไม่ได้ (facility-0051)
_Avoid_: Manual Punch, Override, Adjustment

**Audit Log**:
ประวัติทุกการเปลี่ยนแปลงที่ Admin ทำ: ใคร, เมื่อไร, เปลี่ยนอะไร, ค่าก่อนและหลัง เช่น แก้เวลา, มอบหมายทำแทน, แก้เวลารอบ, ออก QR ใหม่, จัดการบัญชี (facility-0051)
_Avoid_: History, Activity Feed, Change Log

**My Work**:
หน้า "งานของฉัน" บนมือถือแม่บ้าน แสดงทุกจุดใน Area ของตัวเองและ Area ที่ทำแทนในกะนี้: รอบปัจจุบัน, รอบถัดไป, จุดที่เลยรอบ และจุดที่หัวหน้าให้แก้ ใช้แทนการแจ้งเตือนเด้งขึ้น (facility-0052)
_Avoid_: Task List, Notifications, Inbox
