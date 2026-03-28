# บทที่ 1: ปูพื้นฐาน E2E Testing และ Playwright

## 1. End-to-End (E2E) Testing คืออะไร?
**End-to-End Testing (E2E Testing)** คือการจำลองพฤติกรรมของผู้ใช้งานจริง (User Flow) เพื่อทดสอบว่าระบบสามารถทำงานร่วมกันได้อย่างสมบูรณ์ตั้งแต่ต้นจนจบ (เช่น ตั้งแต่เปิดหน้าเว็บ -> ล็อกอิน -> กดหยิบสินค้าใส่ตะกร้า -> เช็คเอาท์ -> ตรวจสอบว่ามีอีเมลส่งไปหาผู้ใช้)
*   **ข้อดี:** สร้างความมั่นใจว่าผู้ใช้จะใช้งานระบบได้จริง
*   **ข้อเสีย:** ทำงานช้ากว่า Unit Test และอาจเกิด Flaky Test (เทสผ่านบ้างไม่ผ่านบ้าง) ได้ง่าย หากเครื่องมือหรือเซิร์ฟเวอร์ไม่เสถียร

---

## 2. ทำไมควรเลือกใช้ Playwright?
Playwright เป็นเครื่องมือ E2E Testing รุ่นใหม่จาก Microsoft ที่ออกแบบมาเพื่อแก้ไขข้อจำกัดของเฟรมเวิร์กเก่าๆ อย่าง Selenium หรือ Cypress
*   **ดีกว่า Selenium ยังไง?** Playwright ไม่ต้องใช้ WebDriver ที่ยุ่งยาก และทำงานแบบ Asynchronous อัตโนมัติ (Auto-wait) ทำให้เขียนเทสได้เสถียรกว่ามาก
*   **ดีกว่า Cypress ยังไง?** Playwright รองรับการทำ Multiple Tabs/Windows, รองรับ IFrames ได้ดีกว่า และสนับสนุน WebKit (Safari) แบบ Native อย่างเต็มรูปแบบ รวมถึงรองรับหลายภาษา (Node.js, Python, Java, .NET)

---

## 3. ความสามารถหลักของ Playwright
1.  **Cross-browser:** รองรับ Chromium (Chrome, Edge), WebKit (Safari), และ Firefox ใน API เดียว
2.  **Auto-wait:** ทุกการคลิกหรือพิมพ์ Playwright จะรอจนกว่า Element นั้นจะพร้อม (Visible, Enabled, Stable) อัตโนมัติ ทำให้ไม่ต้องเขียน `sleep(5000)` เหมือนสมัยก่อน
3.  **Network Interception:** สามารถดักจับ ปลอมแปลง (Mock) หรือแก้ไข Network Requests/Responses ได้ เพื่อจำลองสถานการณ์ต่างๆ เช่น Server Error (500)
4.  **Test Isolation:** ทุกเทสจะรันใน Browser Context ที่ถูกสร้างขึ้นมาใหม่ (เหมือนโหมด Incognito ใหม่ทุกเทส) ทำให้เทสแต่ละตัวไม่กวนกัน (ไม่มี Cache/Cookies ตกค้าง)
