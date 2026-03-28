# บทที่ 10: การนำไปใช้ร่วมกับ CI/CD Integration

ประโยชน์สูงสุดของ E2E Testing คือการเอามันไปรันแบบอัตโนมัติบนเซิร์ฟเวอร์ทุกครั้งที่มีการ Push โค้ดหรือกำลังจะ Deploy (Continuous Integration)

## 1. การรันบน GitHub Actions
Playwright เก่งมากเรื่องนี้ ตอนที่เราสั่ง `npm init playwright@latest` มันจะมีไฟล์ workflow สร้างมาให้แล้วที่ `.github/workflows/playwright.yml` หน้าตาประมาณนี้:

```yaml
name: Playwright Tests
on:
  push:
    branches: [ main, master ]
  pull_request:
    branches: [ main, master ]

jobs:
  test:
    timeout-minutes: 60
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - uses: actions/setup-node@v3
      with:
        node-version: 18
        
    - name: Install dependencies
      run: npm ci
      
    - name: Install Playwright Browsers
      run: npx playwright install --with-deps
      
    - name: Run Playwright tests
      run: npx playwright test
      
    - name: Upload Playwright report
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: playwright-report
        path: playwright-report/
        retention-days: 30
```
โค้ดนี้จะติดตั้งสภาวะแวดล้อม โหลดเบราว์เซอร์ รันเทส และ**อัปโหลดรายงานผล (Report)** เก็บไว้ให้เราโหลดมาดูบน GitHub หากเทสพัง!

## 2. การทำ Sharding (กระจายเทส)
เมื่อจำนวนเคสทดสอบของคุณเยอะขึ้น (เช่น มี 500 เคส ใช้เวลา 30 นาที) เราสามารถหั่นครึ่งให้ GitHub หลายๆ Runner ช่วยกันรันพร้อมกันได้ เรียกว่า Sharding:

```bash
# ฝั่งเครื่อง A จะรันพาร์ทที่ 1/3
npx playwright test --shard=1/3

# ฝั่งเครื่อง B จะรันพาร์ทที่ 2/3
npx playwright test --shard=2/3

# ฝั่ง máy C จะรันพาร์ทที่ 3/3
npx playwright test --shard=3/3
```
ในวงการ CI/CD เราสามารถใช้ Matrix Strategy ของ GitHub Actions เพื่อกระจายการรันนี้ ทำให้การเทสทั้งหมดเสร็จภายใน 10 นาที (ถ้ามี 3 เครื่อง)!

## 3. การใช้งาน Artifacts บน CI
ปัญหาหลักของ CI คือ เวลารันเบื้องหลัง เราไม่เห็นจอ!
ดังนั้น Playwright จึงตั้งค่า Default ไว้ว่าถ้าเทสบน CI "พัง" มันจะอัปโหลด **Trace file (ประวัติ+ภาพ)** ให้เป็น Artifact

เมื่อคุณเข้ามาที่หน้า GitHub Actions:
1. เลื่อนลงไปล่างสุดที่ส่วน Artifacts
2. ดาวน์โหลด `playwright-report.zip`
3. แตกไฟล์ออก และรัน `npx playwright show-report path/to/report` เครื่องของคุณ
4. ดูคลิปและ Trace ย้อนหลังแบบชัดเจนว่า CI ไปตายตรงไหน!
