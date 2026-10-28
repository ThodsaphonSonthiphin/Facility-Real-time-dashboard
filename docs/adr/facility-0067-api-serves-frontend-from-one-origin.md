# The API Serves the Built Frontend from One Origin — AllowAll CORS Removed

```mermaid
flowchart TD
    Q{"ระบบจริงใครเสิร์ฟ frontend ที่ build แล้ว<br/>และ cookie, CORS, URL บนป้าย QR เปลี่ยนอย่างไร"} -->|chosen| A["A: API เสิร์ฟจาก wwwroot ที่ domain เดียว<br/>path ที่ไม่ใช่ /api หรือ /hubs ได้ index.html<br/>cookie คง SameSite=Strict, hub อยู่ origin เดียวกัน, ลบ CORS AllowAll<br/>URL บนป้าย = PublicBaseUrl"]
    Q -->|rejected| B["B: static server แยก (nginx หรือ Cloudflare Pages):<br/>frontend กับ API อยู่สอง origin cookie ต้องเป็น SameSite=None<br/>API ต้องเปิด CORS ให้ origin ของ frontend และ hub ต้องข้าม origin<br/>สองอย่างต้องตั้งและดูแลให้ตรงกัน"]
```

- Decision map: [#34 deploy-origin](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/34) บนแผนที่ [#1](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/1) (milestone `frontend-restructure`)
- วันที่: 2026-10-08
- ที่มา: เจ้าของโปรเจกต์ตัดสิน 2026-10-08 ("go with recommend")
- เกี่ยวข้อง: facility-0049 (HTTPS ที่ domain คงที่ เครื่องรอฝ่าย IT), facility-0026 (router ใช้ path ปกติ `/scan/:token` ไม่ใช่ hash), facility-0013 (refresh token เป็น HttpOnly cookie), facility-0018 (ทุก endpoint และ hub ต้อง login), facility-0021 (`PublicBaseUrl`)

## Context & Decision

วัดบน `master` df02936: `Program.cs` เปิด CORS policy `AllowAll` (`SetIsOriginAllowed(_ => true)`) และ API ยังไม่เสิร์ฟไฟล์ static cookie ของ refresh token ตั้ง `HttpOnly`, `SameSite=Strict` และ `Secure` ตาม config ตอนพัฒนา Vite proxy `/api` กับ `/hubs` ไปที่ API browser จึงเห็น origin เดียวอยู่แล้ว router ของ facility-0026 ใช้ path ปกติ และป้าย QR Sign ที่พิมพ์ชี้ `/scan/<token>` ตรง ๆ ตัวเสิร์ฟจึงต้องส่ง `index.html` ให้ทุก path ของหน้า

จึงตัดสินใจว่า **API เสิร์ฟ frontend ที่ build แล้วจาก origin เดียว** ไม่ว่าฝ่าย IT จะเลือกเครื่องในโรงงานหรือ cloud:

1. ผล `npm run build` (`frontend/dist`) ไปอยู่ใน `wwwroot` ของ `FacilityRealtime.Api` ตอน publish
2. API ใช้ `UseDefaultFiles` + `UseStaticFiles` และ `MapFallbackToFile("index.html")` สำหรับ path ที่ไม่ขึ้นต้นด้วย `/api` หรือ `/hubs` (path ที่ขึ้นต้นด้วยสองตัวนี้แต่ไม่มี endpoint ได้ 404 ไม่ใช่ `index.html`)
3. cookie ของ refresh token คง `SameSite=Strict` และ hub อยู่ origin เดียวกัน
4. ลบ CORS policy `AllowAll` ไม่มีอะไรเรียก API จาก origin อื่น ทั้งระบบจริงและตอนพัฒนา (Vite proxy)
5. URL บนป้าย QR Sign มาจาก `PublicBaseUrl` ซึ่งคือ domain นั้น

## Consequences

- มีเครื่องเดียว ใบรับรองเดียว และ deploy ชิ้นเดียว ใช้ได้ทั้งกับ Cloudflare Tunnel ระหว่างรอฝ่าย IT และกับเครื่องจริง
- การเปลี่ยนอยู่ที่การตั้งค่าตอนเริ่มของ API project (เสิร์ฟไฟล์, ลบ `AllowAll`) ไม่เปลี่ยน endpoint, DTO หรือข้อความของ hub จึงไม่ขัดกับ out-of-scope ของ milestone `frontend-restructure`
- ถ้าวันหนึ่งต้องแยก frontend ไปอีก origin ต้องตัดสินใหม่เรื่อง cookie `SameSite=None`, CORS ที่จำกัด origin และ hub ข้าม origin
- การ build frontend เข้า `wwwroot` ต้องอยู่ในขั้นตอน deploy ซึ่งยังไม่มี CI (facility-0065)

## Amendment (2026-10-28): ไฟล์ที่บีบไว้ตั้งแต่ตอน build และ SignalR แยกไฟล์

วัดด้วย Lighthouse 13.6.0 (จำลองมือถือ, 4G ช้า, CPU ช้าลง 4 เท่า, cache ว่าง, มัธยฐาน 3 รอบ) บน production build: การบีบอัดสด (`UseResponseCompression`) ใช้ระดับเร็วที่สุด จึงส่ง JS หลัก 212 KB ขณะที่ brotli ระดับ 11 ได้ 138 KB และ `favicon.svg` ไม่ถูกบีบเลย (`image/svg+xml` ไม่อยู่ในชนิดตั้งต้น)

1. ตอน build, plugin `frontend/precompress.ts` เขียน `ชื่อไฟล์.br` (brotli 11) และ `ชื่อไฟล์.gz` (gzip 9) ข้างไฟล์ข้อความทุกไฟล์ (เก็บเฉพาะเมื่อเล็กกว่าไฟล์จริง)
2. API (`PrecompressedFiles.cs`) ส่งไฟล์ที่บีบไว้แล้วตามที่ browser รับ (br ก่อน gzip) ด้วย `Content-Type` ของไฟล์จริง และ `Vary: Accept-Encoding` การบีบสดจะข้ามคำตอบที่มี `Content-Encoding` อยู่แล้ว ไฟล์ที่ไม่มีไฟล์บีบไว้ และ `index.html` ที่ส่งแทน path ของหน้า ยังใช้การบีบสดแบบเดิม ส่วน `/api` และ `/hubs` ยังไม่บีบเหมือนเดิม (BREACH)
3. `@microsoft/signalr` ถูกโหลดด้วย dynamic import ใน `followScanHub` เริ่มโหลดพร้อมกับตอนรอคำตอบแรก หน้าก่อน login จึงไม่โหลดเลย ถ้าไฟล์โหลดไม่ได้ สถานะจะเป็น `reconnecting` และหน้ายังทำงานต่อด้วยการถามซ้ำตามเดิม

ผล: ข้อมูลที่โหลดตอนเปิดหน้าก่อน login ลดจาก 226 เหลือ 135 KB, D1 ลดจาก 235 เหลือ 156 KB, LCP ของ D1 ลดจาก 2.54 เหลือ 2.34 วินาที และของ My Work ลดจาก 2.49 เหลือ 2.34 วินาที ส่วน CLS ไม่เปลี่ยน
