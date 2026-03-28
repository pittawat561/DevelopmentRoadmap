# บทที่ 8: การดีบักและค้นหาข้อผิดพลาด (Debugging & Troubleshooting)

สิ่งที่ทำให้ Playwright ครองใจโปรแกรมเมอร์ คือเครื่องมือที่ใช้ตอนหาบั๊กว่าเทสมันพังตรงไหน

## 1. UI Mode (พระเอกของงาน)
นี่คือฟีเจอร์ที่ดีที่สุดของ Playwright ให้เปิดเทสด้วยคำสั่ง:
```bash
npx playwright test --ui
```
*   ระบบจะเปิดหน้าต่าง Playwright ขึ้นมา
*   ด้านซ้ายจะแสดงไฟล์เทสทั้งหมดในโฟลเดอร์ให้เลือกคลิกรันได้อิสระ
*   ตรงกลางจะเห็น Timeline (ลากไทม์ไลน์ไปมาได้เลยว่าเกิดการคลิกอะไร ข้อมูลขึ้นตอนไหน เปรียบเสมือนดูวิดีโอ Replay กลับไป-กลับมา (Time-traveling))
*   ด้านขวาจะมี Console, Network Monitor ครบถ้วน!

## 2. Playwright Inspector
เครื่องมือคลาสสิค หากไม่ได้เปิดแบบ --ui 
```bash
npx playwright test --debug
```
*   เทสจะถูกสตัฟฟ์ (Pause) เอาไว้ แล้วมีหน้าต่างรันคำสั่งโผล่มา
*   คุณสามารถกดเช็คโค้ดทีละบรรทัด (Step Over) คล้ายคลึงกับการใช้ Debugger ใน VS Code และสามารถเอาเมาส์จิ้ม Element ในเบราว์เซอร์เพื่อแอบส่องว่าควรใช้ Locator โค้ดไหนมาจับ

นอกจากสั่งรันผ่าน Command line ยังสามารถสั่งพักโค้ดได้โดยจับ `await page.pause()` ยัดไว้ในจุดที่ต้องการ

```typescript
test('Debugging test', async ({ page }) => {
  await page.goto('/login');
  
  // เบราว์เซอร์จะชะงักหยุดรอตรงนี้ เพื่อให้เรานั่งไล่เช็คหรือเปิด DevTools ตรวจสภาพได้
  await page.pause(); 
  
  await page.getByLabel('User').fill('test');
});
```

## 3. Trace Viewer (ใช้วิเคราะห์บั๊กบนฝั่ง CI/CD)
ถ้าเราเปิดโหมดเก็บบันทึกประวัติ (Trace recording) เอาไว้ หากโค้ดขึ้นไปพังที่ CI บน GitHub
Playwright จะบรรจุไฟล์ Zip ที่รวม Log ทุกอย่างไว้ โหลดกลับลงมาให้เราเปิดที่เครื่อง 

วิธีเปิด Trace Viewer (ระบุพาธไปที่ไฟล์ zip ท้องถิ่น):
```bash
npx playwright show-trace trace.zip
```
หรือลากไฟล์ .zip ไปใส่ในหน้าเว็บ [Playwright Trace Viewer (trace.playwright.dev)](https://trace.playwright.dev/) ได้เลย

## 4. Mobile Emulation (หน้าจอจำลองมือถือ)
เราสามารถกำหนดค่า Viewport มือถือ เพื่อดูปัญหาหรือเทสว่าบนมือถือปังไหม

ในรูปแบบผ่านตัวเลือก:
```typescript
import { test, devices } from '@playwright/test';

test.use({
  ...devices['iPhone 14 Pro'], // ให้เบราว์เซอร์ปลอมตัวเป็น iPhone
  locale: 'th-TH', // ตั้งค่าภาษาให้ด้วย
  geolocation: { longitude: 100.5, latitude: 13.75 }, // จำลอง GPS ในกรุงเทพฯ
  permissions: ['geolocation'], // ให้สิทธิรันไม่ต้องถาม
});

test('Mobile layout test', async ({ page }) => {
  // ...
});
```
