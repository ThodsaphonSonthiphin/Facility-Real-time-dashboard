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

**Persistent Session**:
สถานะการเข้าสู่ระบบที่บันทึกไว้ในเบราว์เซอร์ของมือถือผู้สแกน ทำให้สามารถสแกนจุดบริการต่างๆ ได้ต่อเนื่องโดยไม่ต้องกรอกรหัสผ่านซ้ำจนกว่าจะกด Logout
_Avoid_: Cookie, Remember Token, Keep-Alive
