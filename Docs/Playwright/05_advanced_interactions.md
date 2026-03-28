# บทที่ 5: การโต้ตอบแบบขั้นสูง (Advanced Interactions)

## 1. การจัดการ IFrames (กรอบย่อยในเว็บ)
เวลาหน้าเว็บมี IFrame ซ้อนอยู่ข้างใน (เช่น วิดีโอ YouTube หรือฟอร์มจ่ายเงิน) เราไม่สามารถใช้ `page.locator()` หา Element ในนั้นตรงๆ ได้ ต้องเจาะเข้าไปที่ Frame ก่อน:

```typescript
// หากรอบ IFrame ด้วย Locator (เช่น หา IFrame ที่มีชื่อ title หรือ name)
const frame = page.frameLocator('iframe[title="Secure Payment"]');

// ทะลวงเข้าไปคลิกปุ่มหรือกรอกข้อมูลด้านใน
await frame.getByLabel('Card Number').fill('4111222233334444');
await frame.getByRole('button', { name: 'Pay Now' }).click();
```

---

## 2. การจัดการ Popups, Dialogs, และ Alerts
Playwright จะปิด Dialog อัตโนมัติ (คลิก Cancel/Dismiss ให้เอง) ถ้าไม่สั่งอะไร แต่ถ้าหน้าเว็บมี Alert (เช่น "คุณแน่ใจหรือไม่ที่จะลบ?") และเราต้องการกด "OK" ต้องดักฟัง Event ก่อนที่จะคลิกปุ่มที่ทำให้เกิด Alert:

```typescript
// ดักจับ Event 'dialog' ที่จะเกิดขึ้น
page.on('dialog', async dialog => {
  console.log(dialog.message()); // พิมพ์ข้อความบน Alert
  
  // ยืนยัน (OK/Accept)
  await dialog.accept('Prompt input value here if any'); 
  
  // หรือ ปฏิเสธ (Cancel/Dismiss)
  // await dialog.dismiss();
});

// คลิกปุ่มที่ลั่น Dialog (ต้องทำหลัง page.on เสมอ)
await page.getByRole('button', { name: 'Delete Item' }).click();
```

---

## 3. การจัดการหน้าต่างใหม่ (Multiple Tabs/Windows)
เวลาคลิกลิงก์ที่เปิดหน้าแท็บใหม่ (`target="_blank"`) ต้องจับคู่ Context ว่าหน้าจอนั้นคืออะไร:

```typescript
// สั่งคลิกพร้อมกับรอ Event 'page' เพื่อเก็บ Object ของหน้าใหม่
const [newPage] = await Promise.all([
  context.waitForEvent('page'), // ฝั่งนี้รอ...
  page.getByRole('link', { name: 'Open New Tab' }).click() // ฝั่งนี้คลิก
]);

// ตอนนี้เราคุมหน้าแท็บใหม่ได้แล้ว
await newPage.waitForLoadState();
await expect(newPage).toHaveTitle('Title of New Tab');
```

---

## 4. การ Upload และ Download ไฟล์

**การ Upload ไฟล์:** หากช่องเป็น `<input type="file">`
```typescript
await page.getByLabel('Upload resume').setInputFiles('path/to/resume.pdf');

// อัปโหลดหลายไฟล์พร้อมกัน
await page.getByLabel('Upload images').setInputFiles(['img1.png', 'img2.png']);

// ล้างไฟล์ที่อัปโหลดไปแล้ว (เคลียร์ทิ้ง)
await page.getByLabel('Upload resume').setInputFiles([]);
```

**การ Download ไฟล์:** ต้องจับ Event การดาวน์โหลดคล้ายแท็บใหม่
```typescript
const [download] = await Promise.all([
  page.waitForEvent('download'), 
  page.getByRole('button', { name: 'Download Invoice' }).click()
]);

// ตรวจสอบชื่อไฟล์ หรือเซฟเก็บมาไว้ตรวจสอบ
console.log(await download.suggestedFilename());
await download.saveAs('downloads/invoice_test.pdf');
```

---

## 5. การลากและวาง (Drag and Drop)
เทคนิคจับวางง่ายๆ ของ Playwright สมมติคุณมีกล่องต้นทาง (Source) และกล่องปลายทาง (Target):

```typescript
// ลาก 'draggable-item' ไปหย่อนใส่ 'drop-zone'
await page.locator('#draggable-item').dragTo(page.locator('#drop-zone'));
```
