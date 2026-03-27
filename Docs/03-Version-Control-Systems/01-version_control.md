# Version Control Systems — ระบบควบคุมเวอร์ชัน

> ครอบคลุม Git, GitHub, GitLab

---

## 1. Version Control คืออะไร

ระบบที่ช่วย **ติดตามการเปลี่ยนแปลง** ของไฟล์ตลอดเวลา เพื่อให้สามารถ:
- ย้อนกลับไปเวอร์ชันก่อนหน้าได้
- ดูว่าใครเปลี่ยนอะไร เมื่อไหร่
- ทำงานหลายคนพร้อมกันโดยไม่ทับกัน
- แตก branch ทดลองฟีเจอร์ใหม่ได้โดยไม่กระทบโค้ดหลัก

```
ไม่มี Version Control:
project_v1.zip
project_v2.zip
project_v2_final.zip
project_v2_final_REAL.zip       ← ปัญหาชัดเจน 😱

มี Version Control (Git):
commit a1b2c3 — "Add user authentication"
commit d4e5f6 — "Fix login bug"
commit g7h8i9 — "Add password reset"
                                ← ประวัติชัดเจน ✅
```

### ประเภทของ Version Control

```
1. Local VCS — เก็บเวอร์ชันบนเครื่องตัวเอง
   เช่น: RCS
   ❌ ทำงานคนเดียว, ไม่มี backup

2. Centralized VCS — เก็บบน server กลาง
   เช่น: SVN, Perforce
   ✅ ทำงานหลายคนได้
   ❌ server ล่ม = ทำงานไม่ได้

3. Distributed VCS — ทุกคนมีสำเนาทั้งหมด ← Git อยู่ตรงนี้
   เช่น: Git, Mercurial
   ✅ ทำงานออฟไลน์ได้
   ✅ ไม่มี single point of failure
   ✅ Branch/Merge เร็วมาก
```

---

## 2. Git — พื้นฐาน

### Git คืออะไร

Git คือ **Distributed Version Control System** ที่ได้รับความนิยมมากที่สุดในโลก สร้างโดย Linus Torvalds (ผู้สร้าง Linux) ในปี 2005

### การติดตั้งและตั้งค่า

```bash
# ตรวจสอบว่าติดตั้งแล้ว
git --version

# ตั้งค่าตัวตน (ทำครั้งเดียว)
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# ตั้ง default branch เป็น main
git config --global init.defaultBranch main

# ดูการตั้งค่าทั้งหมด
git config --list
```

### Git Workflow พื้นฐาน

```
Working Directory → Staging Area → Local Repository → Remote Repository

  แก้ไขไฟล์       git add         git commit         git push
  ─────────→    ─────────→      ─────────→         ─────────→

  ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ←
                                git pull / git fetch

3 สถานะของไฟล์:
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐
│  Modified   │ →  │   Staged     │ →  │   Committed     │
│  (แก้ไขแล้ว)│    │ (พร้อม commit)│    │  (บันทึกแล้ว)    │
└─────────────┘    └──────────────┘    └─────────────────┘
    git add            git commit
```

### คำสั่ง Git ที่ใช้บ่อย

#### เริ่มต้นโปรเจกต์

```bash
# สร้าง repository ใหม่
git init

# Clone repository ที่มีอยู่แล้ว
git clone https://github.com/user/repo.git
git clone git@github.com:user/repo.git          # ผ่าน SSH
```

#### ดูสถานะและประวัติ

```bash
# ดูสถานะไฟล์
git status                  # ดูทั้งหมด
git status -s               # แบบสั้น

# ดูการเปลี่ยนแปลง
git diff                    # เทียบ working vs staging
git diff --staged           # เทียบ staging vs last commit
git diff main..feature      # เทียบ 2 branches

# ดูประวัติ commit
git log                     # แบบเต็ม
git log --oneline           # บรรทัดเดียว
git log --oneline --graph   # แสดง branch graph
git log -5                  # 5 commits ล่าสุด
git log --author="John"     # เฉพาะคนนี้
git log -- path/to/file     # เฉพาะไฟล์นี้
```

#### เพิ่มและบันทึกการเปลี่ยนแปลง

```bash
# เพิ่มไฟล์เข้า staging
git add file.txt            # ไฟล์เดียว
git add src/                # ทั้ง folder
git add .                   # ทั้งหมด (ระวังไฟล์ที่ไม่ต้องการ!)
git add -p                  # เลือกเป็นส่วนๆ (interactive)

# Commit
git commit -m "Add user login feature"
git commit -am "Fix bug"    # add + commit (เฉพาะ tracked files)

# แก้ commit ล่าสุด
git commit --amend -m "New message"
git commit --amend --no-edit  # เพิ่มไฟล์โดยไม่เปลี่ยน message
```

#### ยกเลิกการเปลี่ยนแปลง

```bash
# ยกเลิกการแก้ไขไฟล์ (กลับเป็น commit ล่าสุด)
git checkout -- file.txt    # วิธีเก่า
git restore file.txt        # วิธีใหม่ (Git 2.23+)

# ยกเลิก staging
git reset HEAD file.txt     # วิธีเก่า
git restore --staged file.txt  # วิธีใหม่

# ย้อนกลับ commit (สร้าง commit ใหม่ที่ยกเลิก)
git revert abc123           # ปลอดภัย ✅

# Reset commit (อันตราย — ลบประวัติ)
git reset --soft HEAD~1     # ยกเลิก commit แต่เก็บไฟล์ใน staging
git reset --mixed HEAD~1    # ยกเลิก commit + unstage (default)
git reset --hard HEAD~1     # ⚠️ ลบทุกอย่าง! ระวัง!
```

### .gitignore

```bash
# สร้างไฟล์ .gitignore เพื่อบอก Git ว่าไม่ต้อง track ไฟล์เหล่านี้

# ตัวอย่าง .gitignore
node_modules/          # npm packages
dist/                  # build output
.env                   # environment variables (ข้อมูลลับ!)
.env.local
*.log                  # log files
*.tmp                  # temp files
.DS_Store              # macOS
Thumbs.db              # Windows
.idea/                 # JetBrains IDE
.vscode/               # VS Code settings (optional)
bin/                   # compiled binaries
obj/                   # .NET build objects
*.exe
```

---

## 3. Git Branching — การแตก Branch

### Branch คืออะไร

Branch คือ "สำเนา" ของโค้ดที่แยกออกมาทำงานอิสระ โดยไม่กระทบ branch หลัก

```
                    feature/login
                   ┌── C4 ── C5
                   │
main:  C1 ── C2 ── C3 ── C6 ── C7    (ไม่ถูกกระทบ)
                              │
                              └── C5 ← Merge feature เข้า main
```

### คำสั่ง Branch

```bash
# ดู branches
git branch                  # ดู local branches
git branch -r               # ดู remote branches
git branch -a               # ดูทั้งหมด

# สร้าง branch
git branch feature/login    # สร้าง branch ใหม่
git checkout -b feature/login  # สร้าง + สลับไปเลย
git switch -c feature/login    # วิธีใหม่ (Git 2.23+)

# สลับ branch
git checkout main           # วิธีเก่า
git switch main             # วิธีใหม่

# ลบ branch
git branch -d feature/login    # ลบ (ถ้า merge แล้ว)
git branch -D feature/login    # ลบบังคับ (ยังไม่ merge)

# เปลี่ยนชื่อ branch
git branch -m old-name new-name
```

### Merge Strategies

#### Fast-Forward Merge

```bash
# เมื่อ main ไม่มี commit ใหม่ตั้งแต่แตก branch
git checkout main
git merge feature/login

# ก่อน merge:
main:    C1 ── C2
                 └── C3 ── C4  (feature/login)

# หลัง merge (fast-forward):
main:    C1 ── C2 ── C3 ── C4  ← main pointer เลื่อนมา
```

#### 3-Way Merge

```bash
# เมื่อทั้ง main และ feature มี commits ใหม่
git checkout main
git merge feature/login

# ก่อน merge:
main:    C1 ── C2 ── C5
                 └── C3 ── C4  (feature/login)

# หลัง merge:
main:    C1 ── C2 ── C5 ── M  ← Merge commit
                 └── C3 ── C4 ┘
```

#### Rebase (ทำให้ประวัติเป็นเส้นตรง)

```bash
git checkout feature/login
git rebase main

# ก่อน rebase:
main:    C1 ── C2 ── C5
                 └── C3 ── C4  (feature)

# หลัง rebase:
main:    C1 ── C2 ── C5
                       └── C3' ── C4'  (feature — commits ใหม่)

# แล้ว merge แบบ fast-forward
git checkout main
git merge feature/login

# ⚠️ กฎสำคัญ: อย่า rebase branch ที่คนอื่นใช้อยู่!
```

### Merge Conflicts

```bash
# เกิดเมื่อ 2 branches แก้ไฟล์เดียวกันตรงที่เดียวกัน
git merge feature/login
# CONFLICT (content): Merge conflict in src/auth.js

# ดูไฟล์ที่ conflict
git status

# ไฟล์จะมีเครื่องหมายบอก:
<<<<<<< HEAD
const maxRetries = 3;         ← ของ main
=======
const maxRetries = 5;         ← ของ feature
>>>>>>> feature/login

# วิธีแก้:
# 1. เลือกอันที่ต้องการ (ลบ markers ออก)
const maxRetries = 5;

# 2. Add + Commit
git add src/auth.js
git commit -m "Resolve merge conflict in auth.js"
```

### Branching Strategy (Git Flow)

```
main (production)
  │
  ├── develop (development)
  │     │
  │     ├── feature/login     ← ฟีเจอร์ใหม่
  │     ├── feature/payment
  │     │
  │     └── release/1.0       ← เตรียม release
  │
  └── hotfix/critical-bug     ← แก้ bug ด่วน

Simplified (GitHub Flow) — แนะนำสำหรับทีมเล็ก:
main
  │
  ├── feature/login     → Pull Request → Review → Merge to main
  ├── fix/auth-bug      → Pull Request → Review → Merge to main
  └── feature/payment   → Pull Request → Review → Merge to main
```

---

## 4. Git Remote — ทำงานกับ Repository ระยะไกล

```bash
# ดู remote
git remote -v

# เพิ่ม remote
git remote add origin https://github.com/user/repo.git

# Push (ส่งขึ้น remote)
git push origin main              # push branch main
git push -u origin main           # push + ตั้ง upstream tracking
git push                          # ถ้าตั้ง upstream แล้ว

# Pull (ดึงจาก remote + merge)
git pull origin main
git pull                          # ถ้าตั้ง upstream แล้ว

# Fetch (ดึงข้อมูลจาก remote แต่ยังไม่ merge)
git fetch origin
git fetch --all

# Pull = Fetch + Merge
# แนะนำ: ใช้ fetch ก่อน แล้วค่อย merge เพื่อดูก่อน

# Push branch ใหม่ขึ้น remote
git push -u origin feature/login

# ลบ remote branch
git push origin --delete feature/login
```

---

## 5. Git ขั้นสูง

### Stash (เก็บงานชั่วคราว)

```bash
# เก็บงานที่ยังทำไม่เสร็จ
git stash                       # เก็บทั้งหมด
git stash -m "WIP: login form"  # พร้อมข้อความ
git stash -u                    # รวม untracked files

# ดู stash ทั้งหมด
git stash list

# ดึง stash กลับมา
git stash pop                   # ดึง + ลบออกจาก stash
git stash apply                 # ดึง + เก็บ stash ไว้
git stash apply stash@{2}       # เลือก stash ที่ต้องการ

# ลบ stash
git stash drop stash@{0}
git stash clear                 # ลบทั้งหมด
```

### Cherry-Pick (เลือก commit เฉพาะ)

```bash
# เอา commit จาก branch อื่นมาใช้
git cherry-pick abc123

# ตัวอย่าง: เอา bugfix จาก develop มาใส่ main
git checkout main
git cherry-pick d4e5f6          # commit ที่แก้ bug
```

### Tags (ทำเครื่องหมายเวอร์ชัน)

```bash
# สร้าง tag
git tag v1.0.0                        # lightweight tag
git tag -a v1.0.0 -m "Release 1.0.0" # annotated tag (แนะนำ)

# ดู tags
git tag
git tag -l "v1.*"               # filter

# Push tag ขึ้น remote
git push origin v1.0.0
git push origin --tags          # push ทุก tags

# Semantic Versioning: v MAJOR.MINOR.PATCH
# v1.0.0 → v1.0.1 (patch: bug fix)
# v1.0.0 → v1.1.0 (minor: new feature, backward compatible)
# v1.0.0 → v2.0.0 (major: breaking changes)
```

### Git Bisect (หา commit ที่ทำให้ bug)

```bash
# หา commit ที่ทำให้เกิด bug ด้วย binary search
git bisect start
git bisect bad                  # commit ปัจจุบันมี bug
git bisect good abc123          # commit นี้ยังปกติ

# Git จะ checkout commit ตรงกลาง
# ทดสอบ → บอก good หรือ bad
git bisect good  # หรือ
git bisect bad

# ทำซ้ำจนเจอ commit ต้นเหตุ
# จบการค้นหา
git bisect reset
```

---

## 6. GitHub

### GitHub คืออะไร

GitHub คือ **แพลตฟอร์มโฮสต์ Git repository** บนเว็บ พร้อมเครื่องมือ collaboration เช่น Pull Requests, Issues, Actions

### Pull Request (PR)

```
Pull Request = ขอให้ review โค้ดก่อน merge

ขั้นตอน:
1. สร้าง branch ใหม่
   git checkout -b feature/login

2. เขียนโค้ด + commit
   git add .
   git commit -m "Add login feature"

3. Push ขึ้น GitHub
   git push -u origin feature/login

4. สร้าง Pull Request บน GitHub
   - เขียนคำอธิบายว่าทำอะไร
   - เลือก reviewer
   - Link ไปยัง Issue (ถ้ามี)

5. Code Review
   - Reviewer อ่านโค้ด, comment, แนะนำ
   - แก้ไขตาม feedback → push อีกรอบ

6. Approve + Merge
   - Reviewer approve
   - Merge เข้า main
   - ลบ branch
```

### GitHub Issues

```
Issues = ติดตาม bugs, features, tasks

ตัวอย่างการใช้:
- Bug report: "Login fails when password contains special characters"
- Feature request: "Add dark mode toggle"
- Task: "Write unit tests for auth module"

Commit ที่ link กับ Issue:
git commit -m "Fix login bug with special characters. Closes #42"
                                                       ↑
                                            ปิด Issue #42 อัตโนมัติ
Keywords: Closes, Fixes, Resolves (+ #issue_number)
```

### GitHub Actions (CI/CD)

```yaml
# .github/workflows/ci.yml
name: CI Pipeline

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4      # ดึงโค้ด
      - uses: actions/setup-node@v4    # ติดตั้ง Node.js
        with:
          node-version: '20'
      - run: npm install               # ติดตั้ง dependencies
      - run: npm test                  # รัน tests
      - run: npm run build             # build

# ทำงานอัตโนมัติทุกครั้งที่ push หรือสร้าง PR
```

### GitHub คุณสมบัติอื่นๆ

```
GitHub Pages    — โฮสต์เว็บไซต์ static ฟรี
GitHub Packages — โฮสต์ npm/Docker packages
GitHub Copilot  — AI ช่วยเขียนโค้ด
GitHub Projects — Kanban board จัดการงาน
Dependabot      — อัปเดต dependencies อัตโนมัติ
GitHub Codespaces — dev environment บน cloud
```

---

## 7. GitLab

### GitLab vs GitHub

```
| หัวข้อ              | GitHub                | GitLab                |
|--------------------|----------------------|----------------------|
| ความนิยม            | ⭐ มากที่สุด          | อันดับ 2              |
| Self-hosted         | Enterprise เท่านั้น    | ✅ ฟรี (Community)    |
| CI/CD              | GitHub Actions        | GitLab CI/CD (built-in)|
| Container Registry  | GitHub Packages       | ✅ Built-in           |
| Issue Tracking      | Basic                 | Advanced (boards, epics)|
| DevOps Pipeline     | ต้องเสริม tools       | ✅ ครบในตัว (all-in-one)|
| ราคา (private repo) | ฟรี                   | ฟรี                   |

เลือกอะไร:
├── Open Source / Community project?          → GitHub
├── ต้องการ Self-hosted / On-premise?         → GitLab
├── ต้องการ DevOps pipeline ครบในตัว?         → GitLab
├── ต้องการ community ใหญ่, หางานง่าย?       → GitHub ✅
└── ใช้ทั้งคู่ได้!                             → mirror ข้าม platform
```

### GitLab CI/CD

```yaml
# .gitlab-ci.yml
stages:
  - build
  - test
  - deploy

build:
  stage: build
  script:
    - npm install
    - npm run build
  artifacts:
    paths:
      - dist/

test:
  stage: test
  script:
    - npm test

deploy:
  stage: deploy
  script:
    - npm run deploy
  only:
    - main               # deploy เฉพาะ branch main
```

---

## 8. สรุป Git Commands ที่ใช้บ่อย

```
| คำสั่ง                     | ทำอะไร                    |
|---------------------------|--------------------------|
| git init                  | สร้าง repo ใหม่            |
| git clone <url>           | copy repo                |
| git status                | ดูสถานะ                   |
| git add .                 | stage ทุกไฟล์              |
| git commit -m "msg"       | บันทึก                    |
| git push                  | ส่งขึ้น remote             |
| git pull                  | ดึงจาก remote             |
| git branch                | ดู branches               |
| git checkout -b name      | สร้าง + สลับ branch        |
| git merge branch          | รวม branch                |
| git log --oneline         | ดูประวัติ                  |
| git diff                  | ดูการเปลี่ยนแปลง           |
| git stash                 | เก็บงานชั่วคราว             |
| git revert <hash>         | ยกเลิก commit (ปลอดภัย)    |
| git tag v1.0.0            | ทำเครื่องหมายเวอร์ชัน        |
```
