MODE = 'diagram';
const { setComp, setArrow, setLabel, setText } = modeRenderers[MODE];

const GLOSSARY = {
  'persistent-session': {
    term: 'Persistent Session',
    short: 'สถานะการเข้าสู่ระบบที่ค้างไว้ในเบราว์เซอร์ของผู้ใช้ (เช่น มือถือผู้สแกน) ทำให้ใช้งานต่อเนื่องได้โดยไม่ต้องกรอกรหัสผ่านซ้ำ จนกว่าจะกด Logout, ไม่ได้ใช้งานนาน 30 วัน หรือบัญชีถูกปิด',
    seeAlso: ['access-token', 'refresh-token'],
    source: 'CONTEXT.md'
  },
  'cleaner-account': {
    term: 'Cleaner Account',
    short: 'บัญชีผู้ใช้งานระดับพนักงานทำความสะอาดที่สร้างขึ้นล่วงหน้าโดยผู้ดูแลระบบ (Admin) ประกอบด้วย Username, Password Hash, Display Name และสิทธิ์การใช้งาน',
    seeAlso: ['admin-account', 'deactivated-account'],
    source: 'CONTEXT.md'
  },
  'admin-account': {
    term: 'Admin Account',
    short: 'บัญชีผู้ใช้งานระดับผู้ดูแลระบบ มีสิทธิ์จัดการ Service Point, ออก QR Token ใหม่ และจัดการบัญชีผู้ใช้ ซึ่ง Cleaner Account ทำไม่ได้',
    seeAlso: ['cleaner-account', 'deactivated-account'],
    source: 'CONTEXT.md'
  },
  'deactivated-account': {
    term: 'Deactivated Account',
    short: 'บัญชี (Cleaner หรือ Admin) ที่ Admin ปิดใช้งาน login ไม่ได้และ Session ที่ค้างอยู่หลุดภายในไม่กี่นาที แต่ประวัติ Scan Record และชื่อยังคงอยู่ เพราะระบบไม่ลบบัญชี Admin เปิดใช้งานกลับได้',
    seeAlso: ['persistent-session'],
    source: 'CONTEXT.md'
  },
  'access-token': {
    term: 'Access Token (JWT)',
    short: 'JSON Web Token อายุสั้น (5 นาที) เก็บใน Memory ของหน้าเว็บเท่านั้น ใช้แนบใน Authorization Header สำหรับเรียก API และเชื่อมต่อ SignalR',
    seeAlso: ['refresh-token', 'httponly-cookie'],
    source: 'authored'
  },
  'refresh-token': {
    term: 'Refresh Token',
    short: 'Token สุ่ม 256 บิต (Base64Url) เก็บใน HttpOnly Cookie มีอายุ 30 วัน ใช้สำหรับขอ Access Token ใบใหม่เมื่อใบเดิมหมดอายุ',
    seeAlso: ['token-rotation', 'reuse-detection', 'httponly-cookie'],
    source: 'authored'
  },
  'httponly-cookie': {
    term: 'HttpOnly Cookie',
    short: 'คุกกี้ที่ JavaScript บนหน้าเว็บไม่สามารถเข้าถึงหรือขโมยอ่านได้ผ่าน document.cookie ช่วยป้องกันภัยคุกคามจากการโจมตีประเภท XSS',
    seeAlso: ['refresh-token', 'access-token'],
    source: 'authored'
  },
  'token-rotation': {
    term: 'Token Rotation',
    short: 'เทคนิคที่ทุกครั้งที่มีการใช้ Refresh Token ขอ Access Token ใหม่ เซิร์ฟเวอร์จะยกเลิก Refresh Token เดิมและออกใบใหม่ให้เสมอ (1 Token ใช้ได้ 1 ครั้ง)',
    seeAlso: ['reuse-detection', 'refresh-token'],
    source: 'authored'
  },
  'reuse-detection': {
    term: 'Reuse Detection',
    short: 'กลไกตรวจจับเมื่อมีการนำ Refresh Token ที่ถูกหมุนไปแล้วกลับมาใช้ซ้ำเกิน Grace Window (30 วิ) ระบบจะยกเลิกทุก Token ใน Session ทันทีเพื่อป้องกันการขโมย',
    seeAlso: ['token-rotation', 'refresh-token'],
    source: 'authored'
  }
};

const scenes = [
  // Scene 0: Overview
  () => {
    setNarration(
      '',
      'เริ่มต้น — สถาปัตยกรรมระบบ Login & Persistent Session',
      'ระบบแบ่งออกเป็น 3 ฝั่งหลัก: <strong>1) Client (Browser & React App)</strong> มี Mock หน้าจอ Login และ Storage 3 ชั้น, <strong>2) Backend (ASP.NET Core)</strong> มี AuthEndpoints, PBKDF2 Hasher, Token Issuer, และ Reuse Detector, <strong>3) Database</strong> มีตาราง users และ refresh_tokens. กด <strong>ถัดไป →</strong> เพื่อดูการทำงานทีละสเต็ป'
    );
    show('panelIntro');
  },

  // Scene 1: User submits form
  () => {
    setNarration(
      'warn',
      'Step 1 — ผู้ใช้กรอกฟอร์ม Somchai และกดปุ่มเข้าสู่ระบบ',
      'พนักงานทำความสะอาด (<span class="term" data-term="cleaner-account">Cleaner Account</span>) กรอกชื่อ <code>somchai</code> และรหัสผ่าน <code>password123</code> แล้วกดปุ่ม Submit ฟอร์มจะเข้าสู่สถานะ <code>isSubmitting: true</code> ป้องกันการกดซ้ำ และเรียก <code>login()</code> ใน <code>AuthContext.tsx</code>'
    );
    setComp('compMockUser', 'active');
    setComp('compMockPass', 'active');
    setComp('compMockBtn', 'firing');
    setText('mockBtnText', 'กำลังตรวจสอบ...');
    show('panelCodeLogin');
  },

  // Scene 2: Client sends POST /api/auth/login
  () => {
    setNarration(
      '',
      'Step 2 — Client ส่งคำขอ HTTP POST /api/auth/login',
      '<code>authSession.ts</code> ยิง Fetch API ด้วย <code>credentials: "same-origin"</code> นำส่ง JSON Payload <code>{ username, password }</code> ตรงไปยัง Backend เพื่อร้องขอการพิสูจน์ตัวตน'
    );
    setComp('compMockBtn', 'active');
    setArrow('arrFormToApi', 'active');
    setLabel('lblLoginReq', true);
    setComp('compEndpoint', 'active');
    setText('endpointState', 'POST /login (somchai)');
    show('panelCodeLogin');
  },

  // Scene 3: Backend DB query & PBKDF2 verification
  () => {
    setNarration(
      'warn',
      'Step 3 — Backend ค้นหา User และ Verify Hash ด้วย PBKDF2 (100k รอบ)',
      'Backend ค้นหาผู้ใช้ใน DB หากผู้ใช้ไม่มีอยู่จริงหรือเป็น <span class="term" data-term="deactivated-account">Deactivated Account</span> ระบบจะตรวจสอบรหัสผ่านกับ <code>DummyPasswordHash</code> เสมอ เพื่อให้ใช้เวลาประมวลผลเท่ากัน ป้องกันการแฮกด้วย <strong>Timing Attack</strong>'
    );
    setComp('compEndpoint', 'active');
    setArrow('arrApiToDb', 'active');
    setLabel('lblDbUserQuery', true);
    setComp('compDbUsers', 'active');
    setText('dbUserState', 'SELECT user somchai');
    setArrow('arrDbToApi', 'done');
    setLabel('lblDbUserFound', true);
    setArrow('arrApiToHasher', 'active');
    setLabel('lblVerifyPass', true);
    setComp('compHasher', 'firing');
    setText('hasherState', 'PBKDF2 100,000 iters');
    show('panelCodeBackendLogin');
  },

  // Scene 4: Password matches -> Issue Tokens & Save to DB
  () => {
    setNarration(
      'success',
      'Step 4 — รหัสผ่านถูกต้อง! Backend สร้าง Access Token และ Refresh Token',
      'เมื่อรหัสผ่านตรงกัน Backend จะออก <span class="term" data-term="access-token">Access Token</span> เป็น JWT อายุ 5 นาที และสุ่ม <span class="term" data-term="refresh-token">Refresh Token</span> ขนาด 256 บิต บันทึกค่า SHA-256 Hash ลงตาราง <code>refresh_tokens</code> โดยตั้ง <code>ExpiresAt = now + 30 วัน</code> เพื่อรองรับ <span class="term" data-term="persistent-session">Persistent Session</span>'
    );
    setComp('compHasher', 'done');
    setText('hasherState', '✓ Hash Matched');
    setArrow('arrHasherToApi', 'done');
    setLabel('lblHashMatch', true);
    setComp('compEndpoint', 'active');
    setArrow('arrApiToIssuer', 'active');
    setLabel('lblIssueTokens', true);
    setComp('compIssuer', 'firing');
    setText('issuerState', 'JWT 5m + Token-A');
    setArrow('arrIssuerToDb', 'active');
    setLabel('lblSaveToken', true);
    setComp('compDbTokens', 'done');
    setText('dbTokenState', 'Session S1: Token-A Hash');
  },

  // Scene 5: Response 200 OK + Set-Cookie + In-Memory Store
  () => {
    setNarration(
      'magic',
      'Step 5 — ตอบกลับ 200 OK: คืน JWT ใน Body และฝัง Refresh Token ใน HttpOnly Cookie',
      'หัวใจของ <strong>ADR facility-0013</strong>: Refresh Token ถูกส่งกลับผ่าน <span class="term" data-term="httponly-cookie">HttpOnly Cookie</span> (JavaScript เข้าถึงไม่ได้ ป้องกัน XSS) ขณะที่ Access Token ส่งกลับใน JSON Body เพื่อเก็บใน In-Memory ของ <code>authSession.ts</code> เท่านั้น (ไม่แตะ localStorage)'
    );
    setComp('compEndpoint', 'done');
    setText('endpointState', '200 OK (JWT + Cookie)');
    setArrow('arrApiToCookie', 'magic');
    setLabel('lblSetCookie', true, 'magic');
    setComp('compCookie', 'locked');
    setText('cookieVal', 'facility_refresh: Token-A (30d)');
    setArrow('arrApiToMem', 'active');
    setLabel('lblSetMem', true);
    setComp('compMemory', 'done');
    setText('memVal', 'JWT (somchai, expires: 5m)');
    setComp('compMockBtn', 'done');
    setText('mockBtnText', '✓ เข้าสู่ระบบสำเร็จ');
    setComp('compLocal', 'blocked');
    setText('localVal', '❌ localStorage ปลอดภัย (ว่างเปล่า)');
    show('panelCodeSessionStorage');
  },

  // Scene 6: Normal authenticated API calls
  () => {
    setNarration(
      '',
      'Step 6 — การทำงานปกติ: แนบ Access Token ผ่าน Authorization: Bearer Header',
      'เมื่อพนักงานเปิดหน้าสแกน QR หรือดู Dashboard แอปจะเรียก <code>apiFetch()</code> ใน <code>authSession.ts</code> ซึ่งจะดึง Access Token จาก Memory แนบไปใน Header <code>Authorization: Bearer &lt;token&gt;</code> โดยเบราว์เซอร์จะไม่ส่ง Cookie ไปด้วยเพราะ Path จำกัดไว้เฉพาะ <code>/api/auth</code>'
    );
    setComp('compMemory', 'active');
    setText('memVal', 'JWT: แนบ Authorization Header');
    setComp('compCookie', 'locked');
    setComp('compEndpoint', 'active');
    setText('endpointState', 'GET /api/service-points (Bearer)');
  },

  // Scene 7: Inflight refresh trigger
  () => {
    setNarration(
      'warn',
      'Step 7 — การต่ออายุอัตโนมัติ: ก่อน Access Token หมดอายุ 30 วินาที หรือเมื่อ Reload หน้า',
      'เมื่อผู้ใช้รีโหลดเบราว์เซอร์ (Memory หาย) หรือ Access Token มีอายุเหลือน้อยกว่า 30 วินาที <code>getValidAccessToken()</code> จะยิง <code>POST /api/auth/refresh</code> โดยเบราว์เซอร์จะแนบคุกกี้ <code>facility_refresh</code> ให้อัตโนมัติ หากมีหลายคำขอยิงชนกัน จะแชร์ Promise <code>inflightRefresh</code> เดียวกัน'
    );
    setComp('compMemory', 'active');
    setText('memVal', 'JWT ใกล้หมดอายุ (&lt; 30s)');
    setArrow('arrCookieToApi', 'active');
    setLabel('lblRefreshReq', true);
    setComp('compEndpoint', 'active');
    setText('endpointState', 'POST /refresh (Cookie Token-A)');
    show('panelCodeRefresh');
  },

  // Scene 8: Token Rotation
  () => {
    setNarration(
      'success',
      'Step 8 — Token Rotation: หมุน Refresh Token เก่าเป็น Token ใหม่ทุกครั้งที่ Refresh',
      'Backend ตรวจสอบ Token-A ใน DB พบว่ายังไม่เคยถูกหมุน จึงทำ <span class="term" data-term="token-rotation">Token Rotation</span>: มาร์ก <code>RotatedAt = now</code> ให้ Token-A และออก Token-B ใบใหม่พร้อม Access Token ใหม่ให้ทันที โดยขยายเวลาออกไปอีก 30 วัน (Sliding Window)'
    );
    setComp('compEndpoint', 'active');
    setArrow('arrApiToIssuer', 'active');
    setLabel('lblRotateToken', true);
    setComp('compIssuer', 'firing');
    setText('issuerState', 'Rotate: Token-A -&gt; Token-B');
    setArrow('arrIssuerToDb', 'active');
    setComp('compDbTokens', 'done');
    setText('dbTokenState', 'Token-A (Rotated) | Token-B (active)');
    setArrow('arrApiToCookie', 'magic');
    setComp('compCookie', 'locked');
    setText('cookieVal', 'facility_refresh: Token-B');
    setArrow('arrApiToMem', 'active');
    setComp('compMemory', 'done');
    setText('memVal', 'JWT ใหม่ (อายุ 5 นาที)');
    show('panelCodeRotation');
  },

  // Scene 9: Key Question
  () => {
    setNarration(
      'magic',
      'Step 9 — 💡 คำถามสำคัญ: ถ้ามีคนร้ายแอบขโมย Token-A ไปใช้ซ้อน จะเกิดอะไรขึ้น?',
      'สมมติว่า Token-A ถูกเบราว์เซอร์หมุนเป็น Token-B ไปเรียบร้อยแล้ว แต่อีก 2 นาทีถัดมา มีผู้ไม่หวังดีที่เคยดักจับ Token-A ได้ นำ Token-A ใบเดิมส่งมาที่ <code>/api/auth/refresh</code> อีกครั้ง... ระบบควรทำอย่างไร? กด <strong>ถัดไป →</strong> เพื่อดูผลลัพธ์'
    );
    setComp('compCookie', 'locked');
    setComp('compDbTokens', 'done');
    setComp('compReuse', 'active');
    setText('reuseState', 'รอตรวจสอบ Token...');
    show('panelKeyQuestion');
  },

  // Scene 10: Reuse Detection & Revocation
  () => {
    setNarration(
      'error',
      'Step 10 — ตรวจพบ Reuse เกิน 30 วินาที → ตัดวงจร สั่ง Revoke ทุก Token ใน Session ทันที!',
      'ตามมาตรฐาน <strong>RFC 9700 และ ADR facility-0015</strong>: เมื่อมีคนส่ง Token ที่เคยหมุนไปแล้วเกิน 30 วินาที (<span class="term" data-term="reuse-detection">Reuse Detection</span>) เซิร์ฟเวอร์จะฟันธงว่าเกิดการขโมย Token! ระบบจะสั่งลบและ <strong>Revoke ทุก Token ใน Session S1 ทันที</strong> บังคับให้ทั้งคนร้ายและผู้ใช้จริงหลุดจากระบบ'
    );
    setComp('compCookie', 'error');
    setText('cookieVal', 'Token-A Replayed! (ขโมย)');
    setArrow('arrCookieToApi', 'error');
    setComp('compEndpoint', 'error');
    setText('endpointState', '401 Unauthorized (Reuse)');
    setComp('compReuse', 'error');
    setText('reuseState', '🚨 Reuse Detected &gt; 30s');
    setArrow('arrReuseToDb', 'error');
    setLabel('lblReuseAlert', true, 'error');
    setComp('compDbTokens', 'error');
    setText('dbTokenState', 'Session S1: ALL REVOKED!');
    setComp('compMemory', 'error');
    setText('memVal', 'session = null (หลุดจากระบบ)');
    show('panelCodeReuse');
  },

  // Scene 11: Logout Flow
  () => {
    setNarration(
      'warn',
      'Step 11 — การออกจากระบบ (Logout): เพิกถอน Session ใน DB และสั่งเบราว์เซอร์ลบ Cookie',
      'เมื่อผู้ใช้กดปุ่ม "ออก" ในแอป Frontend จะเรียก <code>POST /api/auth/logout</code> เซิร์ฟเวอร์จะสั่ง <code>RevokeSessionAsync</code> ในฐานข้อมูล และส่งคำสั่ง <code>Set-Cookie: facility_refresh=; Max-Age=0</code> ให้เบราว์เซอร์ทำลายคุกกี้ทิ้ง พร้อมล้างหน่วยความจำใน <code>authSession.ts</code>'
    );
    setComp('compMockBtn', 'active');
    setText('mockBtnText', 'ออกจากระบบ');
    setArrow('arrFormToApi', 'active');
    setComp('compEndpoint', 'active');
    setText('endpointState', 'POST /logout (204 No Content)');
    setComp('compDbTokens', 'done');
    setText('dbTokenState', 'Session S1: RevokedAt = now');
    setComp('compCookie', 'dimmed');
    setText('cookieVal', 'cookie deleted (expired)');
    setComp('compMemory', 'dimmed');
    setText('memVal', 'session = null');
    show('panelCodeLogout');
  },

  // Scene 12: Summary & Trade-offs
  () => {
    setNarration(
      'success',
      'Step 12 — สรุปความปลอดภัยและข้อแลกเปลี่ยน (Architectural Trade-offs)',
      'ระบบออกแบบให้มีความปลอดภัยสูงสุดตามมาตรฐานระดับอุตสาหกรรม โดยมี Access Token สั้น 5 นาที, Persistent Session 30 วันผ่าน HttpOnly Cookie, Token Rotation และ Automatic Reuse Revocation เพื่อปกป้องทั้งสิทธิ์ของพนักงานและข้อมูลการทำความสะอาดในโรงงาน'
    );
    setComp('compMemory', 'done');
    setComp('compCookie', 'locked');
    setComp('compLocal', 'blocked');
    setComp('compEndpoint', 'done');
    setComp('compHasher', 'done');
    setComp('compIssuer', 'done');
    setComp('compReuse', 'done');
    setComp('compDbUsers', 'done');
    setComp('compDbTokens', 'done');
    show('panelSummary');
  }
];

TOTAL = scenes.length - 1;
modeRenderers[MODE].assertRegistryComplete();
buildProgressDots();
render(0);
