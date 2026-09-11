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
