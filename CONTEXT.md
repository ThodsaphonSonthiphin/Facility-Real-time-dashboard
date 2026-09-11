# Facility Real-time Dashboard Context

ระบบติดตามและแสดงผลการทำความสะอาดและดูแลสิ่งอำนวยความสะดวกในโรงงานแบบ Real-time ผ่านการสแกน QR Code

## Language

**Scan Record**:
รายการบันทึกธุรกรรมการสแกน QR ณ จุดบริการ 1 ครั้ง ประกอบด้วย เวลาที่สแกน, รหัสจุดบริการ, ผู้สแกน, สถานะการทำความสะอาด (Normal หรือ Issue), แท็กประเภทปัญหา และหมายเหตุ
_Avoid_: Check-in, Stamp, Log

**Service Point**:
จุดบริการหรือตำแหน่งทางกายภาพที่มีป้าย QR ติดตั้งอยู่สำหรับงานทำความสะอาด เช่น แคนทีน โซน A หรือห้องน้ำชั้น 1
_Avoid_: Checkpoint, Station, Location ID

**Cleaning Status**:
สถานะผลการทำความสะอาดของจุดบริการ มีค่าหลักเป็น Normal (เรียบร้อย/ปกติ) และ Issue (พบปัญหาที่ต้องแจ้งเตือนไปยัง Dashboard)
_Avoid_: State, Flag, Inspection Result

**Cleaner Account**:
บัญชีผู้ใช้งานระดับพนักงานทำความสะอาดที่สร้างขึ้นล่วงหน้าโดยผู้ดูแลระบบ (Admin) ประกอบด้วย Username, Password Hash, Display Name และสิทธิ์การใช้งาน
_Avoid_: User Profile, Member, Employee Profile

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
สถานะการเข้าสู่ระบบที่บันทึกไว้ในเบราว์เซอร์ของมือถือผู้สแกน ทำให้สามารถสแกนจุดบริการต่างๆ ได้ต่อเนื่องโดยไม่ต้องกรอกรหัสผ่านซ้ำจนกว่าจะกด Logout
_Avoid_: Cookie, Remember Token, Keep-Alive
