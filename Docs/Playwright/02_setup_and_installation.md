# บทที่ 2: การติดตั้งและการตั้งค่าพื้นฐาน

## 1. การติดตั้ง Node.js และ Playwright
ก่อนอื่นคุณต้องมี **Node.js** ติดตั้งอยู่ในเครื่อง (แนะนำเวอร์ชัน 18 ขึ้นไป) 

จากนั้นเปิด Terminal แล้วรันคำสั่งเพื่อสร้างโปรเจกต์ Playwright:
```bash
npm init playwright@latest
```
ตัวช่วยติดตั้งจะถามคำถามสองสามข้อ:
*   เลือกใช้ TypeScript หรือ JavaScript (แนะนำ TypeScript)
*   ชื่อโฟลเดอร์สำหรับเก็บไฟล์เทส (ค่าเริ่มต้นคือ `tests`)
*   ต้องการเพิ่ม GitHub Actions workflow ไหม? (กด Y ถ้าต้องการรันเทสบน CI)
*   ต้องการติดตั้ง Playwright browsers ไหม? (กด Y เพื่อโหลด Chrome, Firefox, Safari)

---

## 2. โครงสร้างโฟลเดอร์ของโปรเจกต์
หลังจากติดตั้งเสร็จ โปรเจกต์จะมีหน้าตาประมาณนี้:
```
my-playwright-project/
├── tests/                 # โฟลเดอร์สำหรับเก็บไฟล์สคริปต์ทดสอบ (เช่น example.spec.ts)
├── test-results/          # โฟลเดอร์สำหรับเก็บผลการทดสอบและไฟล์ล้มเหลว (เช่น ภาพหน้าจอ)
├── playwright-report/     # โฟลเดอร์สำหรับ HTML Report เมื่อเทสเสร็จ
├── playwright.config.ts   # ไฟล์ตั้งค่าหลักของ Playwright
├── package.json           # ไฟล์จัดการ Dependencies ของ Node.js
```

---

## 3. เข้าใจไฟล์ Configuration เบื้องต้น (`playwright.config.ts`)
ไฟล์นี้ใช้ควบคุมพฤติกรรมทั้งหมดของการรันเทส ตัวอย่างการตั้งค่าที่สำคัญ:
```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests', // ระบุโฟลเดอร์ที่เก็บเทส
  fullyParallel: true, // รันเทสแบบขนานกันเพื่อความรวดเร็ว
  retries: 1, // หากเทสพังให้รันซ้ำ 1 ครั้ง (ช่วยเรื่อง Flaky test)
  use: {
    baseURL: 'http://localhost:3000', // ตั้งค่า URL หลัก
    trace: 'on-first-retry', // เก็บ Trace (ประวัติการรัน) เมื่อเทสล้มเหลว
    video: 'retain-on-failure', // ถ่ายวิดีโอเฉพาะตอนที่เทสไม่ผ่าน
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }, // ตั้งค่าให้รันบน Chrome
    // สามารถเพิ่ม Firefox, Safari หรือ Mobile Viewport ได้ที่นี่
  ],
});
```

---

## 4. การรันเทสผ่าน Command Line
คำสั่งพื้นฐานในการรันเทสมีดังนี้:

*   **รันเทสทั้งหมด (Headless Mode):**
    ```bash
    npx playwright test
    ```
    *พฤติกรรม:* เบราว์เซอร์จะไม่แสดงขึ้นมาบนหน้าจอ (ทำงานอยู่เบื้องหลัง) เหมาะสำหรับการรันบน CI Server

*   **รันเทสแบบเห็นหน้าจอ (Headed Mode):**
    ```bash
    npx playwright test --headed
    ```
    *พฤติกรรม:* เปิดหน้าต่างเบราว์เซอร์ให้เห็นว่ากำลังคลิกอะไร เหมาะสำหรับตอนเขียนเทสใหม่ๆ

*   **รันผ่าน UI Mode (แนะนำสำหรับนักพัฒนา):**
    ```bash
    npx playwright test --ui
    ```
    *พฤติกรรม:* เปิดโปรแกรม Playwright UI ที่สามารถคลิกเลือกเทสที่จะรัน ดู Time-travel debugging, ดู Network และ Console log ได้ง่ายมากๆ

*   **รันเฉพาะไฟล์ที่ต้องการ:**
    ```bash
    npx playwright test tests/login.spec.ts
    ```
