# บทที่ 4: การเขียนเทสและจัดการ Test Suite

เมื่อเราเข้าใจ Core Concepts แล้ว มาเริ่มเขียนโครงสร้างการเทสจริง ๆ กัน

## 1. โครงสร้างไฟล์เทส (`test()` และ `test.describe()`)
Playwright Test ใช้ฟังก์ชันทดสอบที่คุ้นเคยคล้าย Jest หรือ Mocha
*   `test()`: ใช้กำหนด 1 เคสทดสอบ (Test Case)
*   `test.describe()`: ใช้จัดกลุ่มของ test หลายๆ อันเข้าด้วยกัน (Test Suite)

```typescript
import { test, expect } from '@playwright/test';

// การจัดกลุ่มโปรไฟล์
test.describe('Profile Update Page', () => {

  test('User can update their first name', async ({ page }) => {
    await page.goto('/profile');
    await page.getByLabel('First Name').fill('John');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Profile updated successfully')).toBeVisible();
  });

  test('User cannot save with empty name', async ({ page }) => {
    // ... logic for empty name test
  });

});
```

---

## 2. การใช้ Hooks: ตั้งค่าและล้างข้อมูล
เราสามารถสั่งให้รันโค้ดบางอย่าง "ก่อน" หรือ "หลัง" เทสได้ เพื่อจัดเตรียมสถานะของหน้าเว็บ
*   `beforeAll`: รัน 1 ครั้ง "ก่อน" เข้าสู่กลุ่ม Test Suite (เหมาะกับการสร้างข้อมูลใน Database)
*   `afterAll`: รัน 1 ครั้ง "หลัง" จบกลุ่ม Test Suite
*   `beforeEach`: รัน "ก่อน" ขึ้นเทสแต่ละเตส (เหมาะกับการเปิดหน้าเว็บซ้ำๆ)
*   `afterEach`: รัน "หลัง" จบแต่ละเทส (เหมาะกับการลบข้อมูลที่เพิ่งสร้างตอนเทสเผื่อเคลียร์ให้ว่าง)

```typescript
test.describe('Dashboard', () => {

  test.beforeEach(async ({ page }) => {
    // ล็อกอินและไปยังหน้า Dashboard ก่อนเริ่มแต่ละเทสใน Block นี้
    await page.goto('/login');
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill('pass123');
    await page.getByRole('button', { name: 'Sign in' }).click();
  });

  test('Should show welcome message', async ({ page }) => {
    // โฟกัสแค่เทสสิ่งที่ต้องการ เพราะ Each ถูกจัดการให้แล้ว
    await expect(page.getByText('Welcome, admin')).toBeVisible();
  });
});
```

---

## 3. การเขียน Parameterized Tests (รันเทสเดียวกับข้อมูลหลายๆ ชุด)
หากมีเคสที่เป็นตารางข้อมูล (Data-driven) สามารถสร้าง Loop รันเทสได้เลย เช่น นำลิสต์สกุลเงินมาตรวจสอบ

```typescript
const currencies = ['USD', 'EUR', 'GBP', 'THB'];

for (const currency of currencies) {
  test(`Can switch currency to ${currency}`, async ({ page }) => {
    await page.goto('/pricing');
    await page.getByLabel('Select currency').selectOption(currency);
    await expect(page.locator('.price-display')).toContainText(currency);
  });
}
```

---

## 4. การจัดการสถานะของเทส (Skip, Only)
ตอนที่เรากำลังเขียนหรือแก้ไขบั๊ก เราสามารถบังคับให้ Playwright ข้ามหรือไม่ข้ามเทสบางตัวได้:

*   **`test.only(...)`** - บังคับให้รัน **เฉพาะ** เทสนี้ (และข้ามเทสอื่นทั้งหมดในไฟล์) เหมาะสำหรับตอนโฟกัสหาปัญหา
*   **`test.skip(...)`** - บังคับ **ข้าม** (ไม่รัน) เทสนี้ชั่วคราว (เช่น ฟีเจอร์นี้อาจจะพังจากทีม Backend กำลังซ่อมอยู่)
*   **`test.fixme(...)`** - คล้าย skip แต่สื่อความหมายชัดเจนว่าเทสนี้จงใจข้ามเพราะกำลังมีบั๊กและต้องการการแก้ไข

```typescript
test.only('Focus on this test when debugging', async ({ page }) => {
  // ...
});

test.skip('Skip because backend is down', async ({ page }) => {
  // ...
});
```
