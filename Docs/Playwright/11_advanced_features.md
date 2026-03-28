# บทที่ 11: ฟีเจอร์ขั้นสูง (Advanced Features)

สำหรับคนที่ต้องการดึงประสิทธิภาพ Playwright ให้สุด นี่คือระดับ Pro!

## 1. Global Setup / Authenticated State
**ปัญหา:** ถ้าคุณมี 50 เทสที่ต้องใช้สิทธิ Admin (เริ่มเทสมาต้องเข้าหน้า Login > กรอกข้อมูล > กดซับมิท > เข้าสู่ระบบ) การต้องทำแบบนี้ 50 ซ้ำครั้งเป็นการเสียเวลามากๆ!

**วิธีแก้ (Authenticated State):** 
เราจะรันเทสล็อกอิน "แค่ครั้งเดียว" แล้วให้ Playwright เซฟประวัติล็อกอิน (Cookies/Local Storage) ใส่ไฟล์ `state.json` จากนั้นให้เทสอีก 49 ตัวหยิบไฟล์นี้ไปใส่ในเบราว์เซอร์เลย! (ระบบจะมองว่าล็อกอินเรียบร้อยแล้วทันที)

**ขั้นตอนที่ 1:** สร้างไฟล์สำหรับทำ Auth setup เดี่ยวๆ (`tests/auth.setup.ts`)
```typescript
import { test as setup, expect } from '@playwright/test';

const authFile = 'playwright/.auth/user.json';

setup('authenticate', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('User').fill('admin');
  await page.getByLabel('Password').fill('secret');
  await page.getByRole('button', { name: 'Sign in' }).click();
  
  // รอให้เข้าระบบสำเร็จ
  await expect(page.getByText('Hello Admin')).toBeVisible();

  // ไฮไลต์*: ดูด Cookies และ Local Storage เก็บเป็นไฟล์
  await page.context().storageState({ path: authFile });
});
```

**ขั้นตอนที่ 2:** บอก Playwright ให้ใช้ไฟล์นี้ (ใน `playwright.config.ts`)
```typescript
// ใน config projects
projects: [
  { name: 'setup', testMatch: /.*\.setup\.ts/ },
  {
    name: 'chromium',
    use: { 
      ...devices['Desktop Chrome'],
      storageState: 'playwright/.auth/user.json', // สั่งให้ยัด Cookie นี้เสมอ
    },
    dependencies: ['setup'], // บังคับว่าโปรเจกต์ต้องรัน setup ให้เสร็จก่อน
  },
]
```
จบ! คราวนี้รันกี่ร้อยเทส ระบบก็จะมองว่าคุณ Login เสร็จเรียบร้อยตั้งแต่เสี้ยววินาทีแรก

---

## 2. การเทส Accessibility (การเข้าถึงได้ - a11y)
นอกจากเทสลอจิก Playwright ยังทำหน้าเป็นผู้ตรวจสอบว่าโครงสร้างหน้าเว็บของคุณ ผ่านมาตรฐานของคนพิการหรือไม่ (Accessibility) โดยทำงานร่วมกับ `@axe-core/playwright`

```bash
npm install -D @axe-core/playwright
```

```typescript
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('Homepage should not have any automatically detectable accessibility issues', async ({ page }) => {
  await page.goto('/');

  // สแกนหน้าด้วย Axe
  const accessibilityScanResults = await new AxeBuilder({ page }).analyze();

  // ตรวจสอบว่าไม่ควรมีช่องโหว่ (violations)
  expect(accessibilityScanResults.violations).toEqual([]);
});
```
ถ้าไม่ได้ใส่ `aria-label` ให้ปุ่ม หรือทำสี Contrast อักษรกลืนกับพื้นหลังมากๆ เทสตัวนี้จะฟ้องทันที!
