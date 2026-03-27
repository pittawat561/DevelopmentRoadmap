# บทที่ 9: Visual Regression Testing

นอกจากจะเทสว่าระบบพังหรือเข้าใช้งานได้ไหม Playwright ยังช่วยตอบคำถามว่า "หน้าจอแสดงผลเพี้ยนไปจากเดิมหรือเปล่า?" ได้ด้วย ซึ่งเรียกว่า **Visual Regression Testing** หรือ Visual Comparisons

## 1. การตรวจสอบด้วยภาพหน้าจอ (toHaveScreenshot)
สมมติว่าคุณต้องการรักษาให้ปุ่ม หรือหน้าตารางมีหน้าตาเป๊ะเหมือนที่ Designer ออกแบบมาเสมอ:

```typescript
import { test, expect } from '@playwright/test';

test('Homepage visual looks correct', async ({ page }) => {
  await page.goto('/');

  // แจ้งให้ Playwright ถ่ายภาพแล้วเทียบกับต้นฉบับ
  await expect(page).toHaveScreenshot('landing-page.png');
});
```
**กลไกการทำงาน:**
1. ครั้งแรกที่รัน โค้ดจะ Error เพราะยังไม่มีรูประดับ Base (ภาพต้นฉบับ)
2. ถ้าอยากใช้ภาพปัจจุบันเป็นต้นฉบับ ให้รันอัปเดต: `npx playwright test --update-snapshots`
3. ครั้งต่อไปที่รัน Playwright จะถ่ายภาพปัจจุบัน เอามาเทียบแบบพิกเซล-ต่อ-พิกเซลกับรูปต้นฉบับ ถ้าเพี้ยนเกินค่ากำหนด (เช่น สีเพี้ยน หรือตำแหน่งเลื่อน) เทสจะตกทันที!

*Tips:* สามารถตั้งค่าได้ว่าจะอนุญาตให้เพี้ยนได้กี่พิกเซล (Max Diff Pixels) ป้องกันการพังจาก Rendering เล็กๆ น้อยๆ

## 2. การซ่อนสิ่งที่ไม่คงที่ (Masking)
บนหน้าจออาจจะมีป้ายโฆษณาที่เปลี่ยนรูปทุกครั้ง หรือนาฬิกาบอกเวลาปัจจุบัน ภาพมันจะต่างกันทุกรอบที่เทส วิธีแก้คือให้ Playwright เอาแถบสีชมพูมาทับปิดก่อนแคปภาพ (Mask):

```typescript
await expect(page).toHaveScreenshot('dashboard.png', {
  mask: [page.locator('.dynamic-clock'), page.locator('.ad-banner')]
});
```

## 3. การบันทึกภาพหน้าจอและวิดีโอเพื่อการตรวจสอบ
เอาไว้ใช้ในกรณีที่แค่ต้องการเซฟรูปหรือวิดีโอเฉยๆ ไม่ได้จะเอาไปเทียบกับอะไร

*   **แคปเจอร์รูปธรรมดา:**
    ```typescript
    await page.screenshot({ path: 'screenshots/screenshot.png', fullPage: true });
    ```
    
*   **เปิดบันทึกวิดีโอตอนรันเทส:**
    ตั้งค่าใน `playwright.config.ts`:
    ```typescript
    use: {
      video: 'retain-on-failure', // ถ่ายวิดีโอตลอด แต่เก็บไว้เฉพาะตอนเทสพัง
      // หรือ video: 'on' (เก็บตลอด)
    }
    ```
    วิดีโอจะถูกเก็บไว้ในโฟลเดอร์ `test-results/` ทันทีที่เทสเสร็จ ให้เราไปเปิดดูได้เลยว่าเกิดอะไรขึ้น!
