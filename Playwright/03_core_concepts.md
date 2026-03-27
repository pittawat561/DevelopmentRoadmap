# บทที่ 3: คอนเซ็ปต์หลักที่ต้องรู้ (Core Concepts)

## 1. Browser, Context, และ Page
Playwright มีโครงสร้างการจัดการเบราว์เซอร์เป็นลำดับชั้น 3 ระดับ:
*   **Browser:** คือตัวโปรแกรมเบราว์เซอร์จริงๆ (เช่น Chromium) การเปิด Browser ใช้เวลาและทรัพยากรมาก Playwright จึงเปิดครั้งเดียว
*   **Browser Context:** เปรียบเสมือน "โหมดไม่ระบุตัวตน (Incognito)" แต่ละ Context จะแยก Cache, Cookies และ Local Storage ออกจากกันอย่างเด็ดขาด Playwright จะสร้าง Context ใหม่ให้ทุกเทส ทำให้ **Test Isolation** ทำงานได้สมบูรณ์ (แชร์ Browser เดียวกัน แต่ไม่กวนกัน)
*   **Page:** คือแท็บ (Tab) ในเบราว์เซอร์ แต่ละ Context สามารถมีได้หลาย Page

---

## 2. Locators (การหา Element)
Locator คือเครื่องมือสำหรับช่วยค้นหาปุ่ม ข้อความ หรือช่องกรอกข้อมูลในหน้าเว็บ
Playwright แนะนำให้ใช้ Locator ที่เน้นมุมมองของ User (User-facing locators):

*   `page.getByRole('button', { name: 'Submit' })` - หาตามหน้าที่ (ดีที่สุดสำหรับดึงปุ่ม, ลิงก์, แท็บ)
*   `page.getByText('Welcome back')` - หาตามข้อความที่ปรากฏบนหน้าโปรแกรม
*   `page.getByLabel('Password')` - หาช่อง Input จากป้าย Label
*   `page.getByPlaceholder('Search...')` - หาจาก Placeholder
*   `page.getByTestId('submit-btn')` - หาจาก data-testid (เหมาะสำหรับตอนที่ Element ไม่มีข้อความหรือ Role ชัดเจน)
*   `page.locator('.css-class')` หรือ `page.locator('//xpath')` - ใช้ CSS/XPath (ใช้ยามจำเป็น เพราะเปราะบางกว่า)

---

## 3. Actions (การกระทำ)
เมื่อดึง Element มาได้แล้ว สิ่งที่ทำต่อคือการมีปฏิสัมพันธ์กับมัน Playwright รองรับ Action แบบ Auto-wait:
```typescript
// นำทางไปยัง URL
await page.goto('https://example.com');

// คลิกปุ่ม
await page.getByRole('button', { name: 'Login' }).click();

// พิมพ์ข้อมูลลงฟอร์ม
await page.getByLabel('User Name').fill('admin');

// กดปุ่มคีย์บอร์ด
await page.getByPlaceholder('Search').press('Enter');

// เอาเมาส์ไปชี้ (Hover)
await page.getByText('Menu').hover();

// เลือก Dropdown (Select)
await page.locator('select').selectOption('value2');

// อัปโหลดไฟล์
await page.getByLabel('Upload file').setInputFiles('file.pdf');
```

---

## 4. Assertions (การตรวจสอบผลลัพธ์)
ใช้ฟังก์ชัน `expect()` ของ Playwright Test เพื่อตรวจสอบความถูกต้อง ซึ่งฟังก์ชันพวกนี้จะมาพร้อมกับ **Auto-retrying** (รอจนกว่าเงื่อนไขจะเป็นจริงภายในเวลาที่กำหนด):

```typescript
// ตรวจสอบว่ามี Element ปรากฏบนจอ (Visible)
await expect(page.getByText('Login Success')).toBeVisible();

// ตรวจสอบข้อความ (Text Match)
await expect(page.locator('.status')).toHaveText('Active');

// ตรวจสอบ Attribute ต่างๆ เช่น ลิงก์ (href)
const link = page.getByRole('link', { name: 'Profile' });
await expect(link).toHaveAttribute('href', '/profile');

// ตรวจสอบสถานะการถูกติ๊ก (Checkbox)
await expect(page.getByLabel('Subscribe')).toBeChecked();

// ตรวจสอบจำนวน Element
await expect(page.locator('li.item')).toHaveCount(5);
```

> 🔒 **จำไว้เสมอ:** ใส่คำว่า `await` เสมอเมื่อเรียกใช้ Action และ Assertion ของ Playwright เพราะมันทำงานเป็น Asynchronous (Promise)!
