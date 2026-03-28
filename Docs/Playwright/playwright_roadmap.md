# Playwright Learning Roadmap (เส้นทางการเรียนรู้ Playwright)

> สารบัญและเส้นทางการเรียนรู้เครื่องมือทดสอบอัตโนมัติ (End-to-End Testing) สำหรับ Web Application อย่าง Playwright อย่างเป็นระบบ 🎭

---

## [บทที่ 1: ปูพื้นฐาน E2E Testing และ Playwright](./01_introduction_to_playwright.md)
- [x] What is End-to-End (E2E) Testing?
- [x] ทำไมควรเลือกใช้ Playwright? (เปรียบเทียบกับ Selenium, Cypress, Puppeteer)
- [x] ความสามารถหลักของ Playwright (Cross-browser, Auto-wait, Network Interception)

## [บทที่ 2: การติดตั้งและการตั้งค่าพื้นฐาน](./02_setup_and_installation.md)
- [x] การติดตั้ง Node.js และ Playwright (`npm init playwright@latest`)
- [x] โครงสร้างโฟลเดอร์ของโปรเจกต์ (tests/, playwright.config.ts)
- [x] เข้าใจไฟล์ Configuration เบื้องต้น (`playwright.config.ts`)
- [x] การรันเทสผ่าน Command Line (UI mode, Headed, Headless mode)

## [บทที่ 3: คอนเซ็ปต์หลักที่ต้องรู้ (Core Concepts)](./03_core_concepts.md)
- [x] **Browser, Context, และ Page:** ความสัมพันธ์และวิธีการแยก Environment ของแต่ละเทส (Test Isolation)
- [x] **Locators (การหา Element):** วิธีต่างๆ เช่น `getByRole`, `getByText`, `getByLabel`, CSS selector, XPath
- [x] **Actions (การกระทำ):** การคลิก (Click), พิมพ์ข้อความ (Fill), กดปุ่มคีย์บอร์ด (Press), Hover
- [x] **Assertions (การตรวจสอบผลลัพธ์):** การใช้ `expect` เช่น `toBeVisible`, `toHaveText`, `toHaveAttribute`
- [x] **Auto-waiting:** กลไกการรอ Element อัตโนมัติลดโอกาสเทสพัง (Flaky test)

## [บทที่ 4: การเขียนเทสและจัดการ Test Suite](./04_writing_tests.md)
- [x] โครงสร้างไฟล์เทส (`test()` และ `test.describe()`)
- [x] การใช้ Hooks: `beforeAll`, `afterAll`, `beforeEach`, `afterEach`
- [x] การเขียน Parameterized Tests (รันเทสเดียวกับข้อมูลหลายๆ ชุด)
- [x] การจัดกลุ่มเทส, ข้ามเทส (`test.skip`), หรือบังคับรันเฉพาะบางเทส (`test.only`)

## [บทที่ 5: การโต้ตอบแบบขั้นสูง (Advanced Interactions)](./05_advanced_interactions.md)
- [x] การจัดการ IFrames (`frameLocator`)
- [x] การจัดการ Popups, Dialogs, และ Alerts (เช่น `.accept()`, `.dismiss()`)
- [x] การจัดการหน้าต่างใหม่ (Handling Multiple Tabs/Windows)
- [x] การ Upload และ Download ไฟล์
- [x] การลากและวาง (Drag and Drop)

## [บทที่ 6: การจัดการ Network & API Testing](./06_network_and_api.md)
- [x] การดักจับ Request และ Response (Intercepting Requests)
- [x] การ Mock ข้อมูล API (Mocking API responses) เพื่อให้เทสเร็วและเสถียร
- [x] การเขียน API Testing ตรงๆ ผ่าน Playwright (`request` context) ไร้ UI

## [บทที่ 7: Design Patterns - Page Object Model (POM)](./07_design_patterns_pom.md)
- [x] **Page Object Model (POM):** การจัดระเบียบโค้ดเทส ลดความซ้ำซ้อน และเพิ่มความง่ายในการดูแลรักษา (Maintainability)
- [x] การแยก Test Data และ Environment Variables (`.env`)

## [บทที่ 8: การดีบักและค้นหาข้อผิดพลาด (Debugging & Troubleshooting)](./08_debugging_and_troubleshooting.md)
- [x] การใช้ UI Mode (`npx playwright test --ui`)
- [x] การใช้ Playwright Inspector (`--debug` flag)
- [x] การใช้งาน **Trace Viewer** วิเคราะห์ปัญหาแบบย้อนหลัง (ดู Network, DOM snapshot สลับไป-มาได้)
- [x] การจำลองสภาวะแวดล้อมต่างๆ เช่น มือถือ (Mobile Emulation) การจำลองพิกัด (Geolocation)

## [บทที่ 9: Visual Regression Testing](./09_visual_regression.md)
- [x] การถ่ายภาพหน้าจอ (Screenshots) และบันทึกวิดีโอ (Video Recording) ระหว่างเทส
- [x] การเปรียบเทียบความถูกต้องของ UI (Visual Comparisons ด้วย `expect(page).toHaveScreenshot()`)

## [บทที่ 10: การนำไปใช้ร่วมกับ CI/CD Integration](./10_ci_cd_integration.md)
- [x] การนำ Playwright ไปรันบน GitHub Actions
- [x] การนำ Playwright ไปรันบน GitLab CI หรือ Jenkins
- [x] การตั้งค่า Artifacts (เก็บ Trace และ Report หลังระบบเทสผ่านฉลุยหรือร่วง)
- [x] การทำ Sharding (กระจายเทสไปรันขนานกันบนหลายเครื่องเพื่อลดเวลา)

## [บทที่ 11: ฟีเจอร์ขั้นสูง (Advanced Features)](./11_advanced_features.md)
- [x] Global Setup & Teardown (การเซ็ตอัพสิ่งที่ต้องทำแค่ตอนเริ่มทั้งหมด เช่น ล็อกอินล่วงหน้าเก็บ Session)
- [x] Authenticated State (การบันทึก Session ล็อกอิน เพื่อแชร์ให้ทุกเทสไม่ต้องล็อกอินซ้ำ ลดเวลาได้มาก)
- [x] การเขียนเทส Accessibility (a11y) ควบคู่ไปกับ axe-core
