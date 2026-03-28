# บทที่ 6: การจัดการ Network & API Testing

Playwright เก่งมากในเรื่องการสกัดกั้นหรือตรวจสอบการพูดคุยกันระหว่าง Browser กับ Server (Network Requests) 

## 1. การดักจับ Request และ Response (Intercepting)
เบสิกที่สุดคือการรอดูว่าตอนโหลดหน้านี้ มี API ตัวไหนถูกเรียกบ้าง หรือดูสเตตัสของ API:

```typescript
// รอจดกว่า API นี้จะตอบกลับมาเรียบร้อย
const responsePromise = page.waitForResponse('**/api/v1/users');
await page.getByRole('button', { name: 'Load Users' }).click();

const response = await responsePromise;
expect(response.status()).toBe(200);

// ดึง Response Body มาตรวจสอบ
const responseBody = await response.json();
expect(responseBody.users.length).toBeGreaterThan(0);
```

---

## 2. การ Mock ข้อมูล API (Mocking API responses)
หลายครั้ง Backend ทำงานช้า หรือเราต้องการเทสกรณีแปลกๆ (Edge cases) เช่น หน้าเว็บรับมือกับ Server Error ยังไง? เราสามารถ "ปลอม" ข้อมูลกลับไปได้โดยไม่ต้องพึ่ง Backend จริง!:

```typescript
await page.route('**/api/v1/products', async route => {
  // สร้างข้อมูลเทียมส่งกลับไป
  const mockResponse = {
    products: [
      { id: 1, name: 'Fake Laptop', price: 9999 },
    ]
  };

  // ดักเอาไว้ แล้วส่งข้อมูลนี้กลับไปแทนของจริง!
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(mockResponse),
  });
});

await page.goto('/products');
// UI ควรจะแสดง 'Fake Laptop' ขึ้นมาทันที
```

---

## 3. การทำ API Testing แบบไร้ UI
Playwright ทำ API Testing ตรงๆ ได้เลย (ข้าม Browser ไปยิง API ตรงๆ เหมาะกับการสร้าง State ล่วงหน้าหรือเทสหลังบ้าน) ผ่าน `request` fixture:

```typescript
import { test, expect } from '@playwright/test';

test('Should create a new user via API', async ({ request }) => {
  
  // สั่งยิง POST request ไปที่ /api/users
  const newUser = await request.post('/api/users', {
    data: {
      name: 'John Doe',
      email: 'john@example.com' // payload ของเรา
    }
  });

  // เช็ค Http Status
  expect(newUser.status()).toBe(201); // Created

  // เช็ค Response Body
  const body = await newUser.json();
  expect(body.name).toEqual('John Doe');
});
```

> 💡 **ใช้ร่วมกันได้!** เราสามารถใช้ `request` เพื่อสร้างยิงข้อมูลตั้งต้นอย่างรวดเร็ว (ไม่ต้องไปกด UI สร้างของเอง) แล้วค่อยเปิด Browser มาเทสด้วยความรวดเร็ว ถือเป็นเทคนิคที่ดีมาก
