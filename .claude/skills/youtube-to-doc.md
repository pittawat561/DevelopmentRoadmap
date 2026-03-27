---
name: YouTube to Document
description: ดึง Transcript จาก YouTube แล้วสรุปเป็นเอกสาร Markdown อัตโนมัติ เหมาะสำหรับจัดทำสื่อการเรียนรู้
command: yt-doc
argument-hint: "<YouTube URL> [output-path]"
allowed-tools:
  - mcp__Claude_in_Chrome__tabs_context_mcp
  - mcp__Claude_in_Chrome__tabs_create_mcp
  - mcp__Claude_in_Chrome__tabs_close_mcp
  - mcp__Claude_in_Chrome__navigate
  - mcp__Claude_in_Chrome__computer
  - mcp__Claude_in_Chrome__find
  - mcp__Claude_in_Chrome__read_page
  - mcp__Claude_in_Chrome__get_page_text
  - mcp__Claude_in_Chrome__javascript_tool
  - Write
  - Read
  - Bash
  - Glob
---

# YouTube to Document Skill

คุณคือผู้เชี่ยวชาญในการดึงเนื้อหาจากวิดีโอ YouTube แล้วจัดทำเป็นเอกสาร Markdown ที่มีคุณภาพสูง เหมาะสำหรับใช้เป็นสื่อการเรียนรู้

## Input
- **argument แรก:** YouTube URL (required) — เช่น `https://www.youtube.com/watch?v=xxxxx`
- **argument ที่สอง:** Output path (optional) — เช่น `Docs/my-notes.md` (ถ้าไม่ระบุ จะบันทึกใน `Docs/` โดยใช้ชื่อวิดีโอเป็นชื่อไฟล์)

## ขั้นตอนการทำงาน

### Step 1: เปิด YouTube ใน Chrome
1. ใช้ `tabs_context_mcp` เพื่อดู tab ที่มีอยู่ (สร้าง group ใหม่ถ้ายังไม่มี โดยใช้ `createIfEmpty: true`)
2. ใช้ `tabs_create_mcp` สร้าง tab ใหม่
3. ใช้ `navigate` ไปยัง YouTube URL ที่ผู้ใช้ระบุ
4. รอให้หน้าโหลดเสร็จ (ใช้ `computer` action: `wait` ประมาณ 3 วินาที)

### Step 2: ดึงข้อมูลวิดีโอ
1. ใช้ `get_page_text` เพื่ออ่านข้อมูลหลักของหน้า (Title, Channel name)
2. ใช้ `find` ค้นหา video title element
3. ใช้ `find` ค้นหา channel name
4. ใช้ `read_page` เพื่ออ่าน description (กด "...more" ถ้าจำเป็น)
5. เก็บข้อมูล: **Title**, **Channel**, **URL**, **Description**

### Step 3: ดึง Transcript
ลองดึง Transcript ด้วยวิธีเหล่านี้ตามลำดับ:

**วิธี A: ใช้ JavaScript ดึง Transcript จาก YouTube Data (แนะนำ)**
```javascript
// ลองดึง transcript จาก ytInitialPlayerResponse
const playerResponse = ytInitialPlayerResponse;
const captions = playerResponse?.captions?.playerCaptionsTracklistRenderer?.captionTracks;
if (captions && captions.length > 0) {
    captions[0].baseUrl; // URL ของ transcript
}
```
ใช้ `javascript_tool` เพื่อรันโค้ดนี้ จากนั้นใช้ `navigate` ไปยัง transcript URL เพื่ออ่านเนื้อหา XML

**วิธี B: กดปุ่ม Transcript บนหน้าเว็บ**
1. ใช้ `computer` action: `screenshot` เพื่อดูหน้าเว็บ
2. ใช้ `find` ค้นหาปุ่ม "More actions" หรือ "..." (three dots menu) ใต้วิดีโอ
3. คลิกปุ่มนั้น แล้วค้นหา "Show transcript" หรือ "Open transcript"
4. คลิก "Show transcript"
5. รอให้ Transcript panel เปิด (wait 2 วินาที)
6. ใช้ `find` ค้นหา transcript panel/container
7. ใช้ `read_page` หรือ `get_page_text` อ่านเนื้อหา transcript ทั้งหมด
8. ถ้า transcript ยาว ใช้ `computer` action: `scroll` เลื่อนลงใน transcript panel แล้วอ่านเพิ่ม

**วิธี C: ดึงจาก Description/Comments (Fallback)**
ถ้าไม่มี Transcript ให้แจ้งผู้ใช้ว่าวิดีโอนี้ไม่มี subtitle/transcript แล้วเสนอทางเลือก:
- ใช้เนื้อหาจาก Description แทน
- ให้ผู้ใช้เปิด transcript เองแล้วสั่งอีกครั้ง

### Step 4: จัดรูปแบบเอกสาร Markdown
สร้างเอกสาร Markdown ตามโครงสร้างนี้:

```markdown
# [ชื่อวิดีโอ]

> 🎥 **แหล่งที่มา:** [Channel Name](YouTube URL)
> 📅 **วันที่จัดทำเอกสาร:** [วันที่ปัจจุบัน]

---

## สรุปเนื้อหา (Summary)
[สรุปเนื้อหาหลักของวิดีโอ 3-5 ประโยค]

---

## เนื้อหาหลัก

### [หัวข้อที่ 1]
[เนื้อหาที่สรุปจาก transcript พร้อม timestamp]

### [หัวข้อที่ 2]
[เนื้อหาที่สรุปจาก transcript พร้อม timestamp]

(... แบ่งตามหัวข้อที่เหมาะสม ...)

---

## ประเด็นสำคัญ (Key Takeaways)
* [ประเด็นที่ 1]
* [ประเด็นที่ 2]
* [ประเด็นที่ 3]

---

## คำศัพท์สำคัญ (Key Terms)
| คำศัพท์ | ความหมาย |
|---------|----------|
| [Term 1] | [Definition] |
| [Term 2] | [Definition] |

---

## Transcript ฉบับเต็ม
<details>
<summary>คลิกเพื่อดู Transcript ทั้งหมด</summary>

[Timestamp] ข้อความ...
[Timestamp] ข้อความ...

</details>
```

### กฎการจัดทำเอกสาร:
1. **ภาษา:** ใช้ภาษาเดียวกับ Transcript (ถ้า Transcript เป็นภาษาอังกฤษ ให้เขียนเป็นภาษาอังกฤษ แต่เพิ่มคำอธิบายภาษาไทยกำกับในวงเล็บสำหรับคำศัพท์สำคัญ)
2. **หัวข้อ:** แบ่งเนื้อหาเป็นหัวข้อย่อยตามเนื้อหาที่พูดถึง ไม่ใช่ตามเวลา
3. **Timestamp:** ใส่ timestamp [MM:SS] กำกับในส่วนที่เหมาะสม
4. **Key Terms:** รวบรวมคำศัพท์เทคนิคทั้งหมดพร้อมคำอธิบาย
5. **สรุป:** ต้องสรุปเนื้อหาอย่างชัดเจน ไม่ใช่แค่ copy transcript

### Step 5: บันทึกไฟล์
1. กำหนดชื่อไฟล์จากชื่อวิดีโอ (แปลงเป็น snake_case, ลบอักขระพิเศษ)
2. ถ้าผู้ใช้ระบุ output path ให้ใช้ path นั้น
3. ถ้าไม่ระบุ ให้บันทึกใน `Docs/` directory ของ project
4. ใช้ `Write` tool บันทึกไฟล์
5. แจ้งผู้ใช้ว่าบันทึกไฟล์เรียบร้อย พร้อม path ของไฟล์

### Step 6: ปิด Tab (Cleanup)
1. ใช้ `tabs_close_mcp` ปิด tab YouTube ที่เปิดไว้
2. แจ้งสรุปผลลัพธ์ให้ผู้ใช้

## ตัวอย่างการใช้งาน
```
/yt-doc https://www.youtube.com/watch?v=dQw4w9WgXcQ
/yt-doc https://www.youtube.com/watch?v=dQw4w9WgXcQ Docs/my-video-notes.md
```

## Error Handling
- ถ้า URL ไม่ใช่ YouTube → แจ้งผู้ใช้ว่ารองรับเฉพาะ YouTube URL
- ถ้าไม่พบ Transcript → แจ้งผู้ใช้และเสนอทางเลือก (ใช้ Description หรือเปิด transcript เอง)
- ถ้า Chrome ไม่เชื่อมต่อ → แจ้งให้ผู้ใช้เปิด Chrome พร้อม extension Claude in Chrome
- ถ้าวิดีโอเป็น Private/Restricted → แจ้งผู้ใช้ว่าไม่สามารถเข้าถึงได้
