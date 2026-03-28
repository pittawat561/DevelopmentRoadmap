# บทที่ 7: Design Patterns - Page Object Model (POM)

เมื่อชุดเทสโปรเจกต์คุณใหญ่ขึ้น การเขียน `await page.locator('.login-btn').click()` เลอะเทอะเต็มร้อยๆ ไฟล์จะเป็นฝันร้าย ทันทีที่ UI เปลี่ยน (ตัวอย่างเช่น เปลี่ยนชื่อคลาส) คุณต้องไล่หาไฟล์เทสทุกไฟล์ที่คลิกปุ่มนี้เพื่อตามแก้... โคตรเหนื่อย! 😭

**Page Object Model (POM)** เป็น Pattern เพื่อแก้ปัญหานี้ โดยการสร้าง Class ขึ้นมาเป็น "ตัวแทน" ของหน้านั้นๆ แล้วเอาไปเรียกต่อ

## 1. การสร้างคลาส Page Object (ตัวอย่าง: หน้า Login)

เราแยก Locator และ Action ของหน้านั้นมาครอบไว้ในคลาส (เช่น โฟลเดอร์ `pages/loginPage.ts`):

```typescript
// pages/loginPage.ts
import { Page, Locator, expect } from '@playwright/test';

export class LoginPage {
  // สมาชิกของคลาสจะเป็นตัวเก็บหน้าและการหาตัว (Locator) ต่างๆ
  readonly page: Page;
  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly loginButton: Locator;

  // Constructor เซ็ตค่าเริ่มต้น
  constructor(page: Page) {
    this.page = page;
    this.usernameInput = page.getByLabel('Username');
    this.passwordInput = page.getByLabel('Password');
    this.loginButton = page.getByRole('button', { name: 'Sign in' });
  }

  // สร้างฟังก์ชันครอบลอจิกซ้ำซ้อน (Action Methods)
  async goto() {
    await this.page.goto('https://example.com/login');
  }

  async login(username: string, pass: string) {
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(pass);
    await this.loginButton.click();
  }
}
```

## 2. การเรียกใช้ POM ในไฟล์เทส (`tests/login.spec.ts`)

ทีนี้เวลาเขียนเทสจริง โค้ดจะอ่านรู้เรื่องเหมือนอ่านนิทาน:

```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/loginPage';

test('User can login properly', async ({ page }) => {
  // เรียกอินสแตนซ์ของหน้า Login จัดเตรียมไว้
  const loginPage = new LoginPage(page);
  
  // เรียกใช้งาน Method ที่สร้างไว้
  await loginPage.goto();
  await loginPage.login('admin', 'password123');
  
  // ตรงนี้ก็อาจจะเรียก DashboardPage.ts สำหรับเทสต่อ เป็นต้น
  await expect(page.getByText('Welcome Admin')).toBeVisible();
});
```

*   **ข้อดี:** ถ้าอนาคตปุ่มล็อกอินเปลี่ยนจาก `getByRole('button', { name: 'Sign in' })` ไปเป็นอย่างอื่น ไปตามแก้ **แค่ที่เดียว** คือไฟล์ `loginPage.ts` แล้วเทสทุกตัวบนระบบจะรันผ่านปกติ!
