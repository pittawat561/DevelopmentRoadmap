# Back-End Web Development - A Complete Overview

> 📺 **แหล่งที่มา:** [YouTube - Back-End Web Development (Tutorial for Beginners)](https://www.youtube.com/watch?v=XBu54nfzxAQ)
>
> **Tech Stack ที่ใช้ในวิดีโอ:** Node.js, Express.js, SQLite

---

## 📋 สารบัญ (Timestamps)

| เวลา | หัวข้อ |
|-------|--------|
| 0:00 | Intro |
| 2:58 | Getting Started |
| 12:49 | Basic Styling |
| 17:49 | Letting Users Register |
| 37:00 | SQLite Database Integration |
| 47:47 | Cookies |
| 53:04 | JSON Web Tokens (JWT) |
| 1:07:16 | Letting Users Log In |
| 1:28:22 | Blog Posts CRUD |
| 2:25:38 | Deploying Our App (Going Live / Public) |

---

## 1. Intro - ภาพรวม Backend Web Development

Backend (ฝั่ง Server-Side) คือส่วนที่ทำงานอยู่เบื้องหลังของเว็บแอปพลิเคชัน ผู้ใช้ไม่เห็นโดยตรง แต่เป็นส่วนสำคัญที่ทำให้เว็บทำงานได้ ครอบคลุมเรื่อง:

- **การจัดการข้อมูล** (Data Management)
- **Business Logic** (ตรรกะทางธุรกิจ)
- **การยืนยันตัวตน** (Authentication & Authorization)
- **ความปลอดภัย** (Security)
- **การสื่อสารระหว่าง Client-Server** (API)

### Client-Server Model

```
┌──────────┐     HTTP Request      ┌──────────┐
│          │  ─────────────────►   │          │
│  Client  │                       │  Server  │
│ (Browser)│  ◄─────────────────   │ (Node.js)│
│          │     HTTP Response     │          │
└──────────┘                       └──────────┘
```

- **Client:** ส่ง Request ไปยัง Server (เช่น เว็บเบราว์เซอร์)
- **Server:** รับ Request → ประมวลผล → ส่ง Response กลับ

### Request-Response Cycle

1. ผู้ใช้กระทำบางอย่าง (เช่น พิมพ์ URL, คลิกลิงก์, ส่ง Form)
2. Client สร้าง **HTTP Request** ส่งไปยัง Server
3. Server **ประมวลผล** (Query ฐานข้อมูล, คำนวณ Logic)
4. Server สร้าง **HTTP Response** ส่งกลับไปยัง Client
5. Client แสดงผลลัพธ์ให้ผู้ใช้เห็น

---

## 2. Getting Started - เริ่มต้นกับ Node.js และ Express.js

### Node.js คืออะไร?

- **JavaScript Runtime** ที่ทำให้เราเขียน JavaScript บนฝั่ง Server ได้
- ใช้ **V8 Engine** ของ Google Chrome
- เป็น **Non-blocking, Event-driven** ทำให้รองรับ Request จำนวนมากได้พร้อมกัน

### Express.js คืออะไร?

- **Web Framework** สำหรับ Node.js
- ช่วยให้สร้าง Web Server และ API ได้ง่ายและรวดเร็ว
- เป็น **Minimalist** แต่ยืดหยุ่นสูง

### การเริ่มต้นโปรเจกต์

```bash
# สร้างโฟลเดอร์โปรเจกต์
mkdir my-backend-app
cd my-backend-app

# เริ่มต้น Node.js project
npm init -y

# ติดตั้ง Express.js
npm install express
```

### สร้าง Server เบื้องต้น

```javascript
const express = require('express');
const app = express();

// กำหนด Route แรก
app.get('/', (req, res) => {
  res.send('Hello, World!');
});

// เริ่ม Server
app.listen(3000, () => {
  console.log('Server is running on port 3000');
});
```

### Middleware

Middleware คือฟังก์ชันที่ทำงานระหว่าง Request และ Response:

```javascript
// Middleware สำหรับ parse JSON body
app.use(express.json());

// Middleware สำหรับ parse URL-encoded body
app.use(express.urlencoded({ extended: false }));

// Custom Middleware
app.use((req, res, next) => {
  console.log(`${req.method} ${req.url}`);
  next(); // ส่งต่อไปยัง middleware/route ถัดไป
});
```

### Routing

```javascript
// GET - ดึงข้อมูล
app.get('/api/users', (req, res) => {
  res.json({ users: [] });
});

// POST - สร้างข้อมูลใหม่
app.post('/api/users', (req, res) => {
  const newUser = req.body;
  res.status(201).json(newUser);
});
```

### Template Engine (HTML Views)

Express สามารถ render HTML ผ่าน Template Engine ได้:

```javascript
// ตั้งค่า view engine
app.set('view engine', 'ejs');

// render template
app.get('/', (req, res) => {
  res.render('homepage', { title: 'My App' });
});
```

---

## 3. Basic Styling - การจัดรูปแบบหน้าเว็บ

### การ Serve Static Files

```javascript
// ให้ Express serve ไฟล์ static (CSS, JS, Images)
app.use(express.static('public'));
```

### โครงสร้างโฟลเดอร์

```
my-backend-app/
├── public/
│   ├── css/
│   │   └── style.css
│   ├── js/
│   │   └── main.js
│   └── images/
├── views/
│   ├── homepage.ejs
│   └── layout.ejs
├── server.js
└── package.json
```

### ตัวอย่าง CSS พื้นฐาน

```css
/* public/css/style.css */
body {
  font-family: Arial, sans-serif;
  margin: 0;
  padding: 20px;
  background-color: #f5f5f5;
}

.container {
  max-width: 800px;
  margin: 0 auto;
  background: white;
  padding: 20px;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}
```

---

## 4. Letting Users Register - ระบบสมัครสมาชิก

### แนวคิดหลัก

- รับข้อมูลจากผู้ใช้ผ่าน HTML Form
- **Hash รหัสผ่าน** ก่อนบันทึกลงฐานข้อมูล (ห้ามเก็บ plain text!)
- ใช้ `bcrypt` สำหรับ Hash Password

### ติดตั้ง bcrypt

```bash
npm install bcrypt
```

### Registration Route

```javascript
const bcrypt = require('bcrypt');

app.post('/register', async (req, res) => {
  const { username, email, password } = req.body;

  // Hash password ด้วย bcrypt
  const salt = await bcrypt.genSalt(10);
  const hashedPassword = await bcrypt.hash(password, salt);

  // บันทึกลงฐานข้อมูล
  // INSERT INTO users (username, email, password) VALUES (?, ?, ?)
  // ใช้ hashedPassword แทน password

  res.redirect('/login');
});
```

### Validation (การตรวจสอบข้อมูล)

```javascript
app.post('/register', async (req, res) => {
  const errors = [];
  const { username, email, password } = req.body;

  // ตรวจสอบข้อมูล
  if (!username || username.trim() === '') {
    errors.push('Username is required');
  }
  if (!email || !email.includes('@')) {
    errors.push('Valid email is required');
  }
  if (!password || password.length < 6) {
    errors.push('Password must be at least 6 characters');
  }

  if (errors.length > 0) {
    return res.render('register', { errors });
  }

  // ดำเนินการลงทะเบียน...
});
```

---

## 5. SQLite Database Integration - การเชื่อมต่อฐานข้อมูล

### SQLite คืออะไร?

- ฐานข้อมูลแบบ **Serverless** (ไม่ต้องติดตั้ง Database Server แยก)
- เก็บข้อมูลเป็น **ไฟล์เดียว** (.db)
- เหมาะสำหรับ **โปรเจกต์ขนาดเล็ก-กลาง**, Prototyping, และการเรียนรู้
- ใช้ **SQL** ในการจัดการข้อมูล

### ติดตั้งและเชื่อมต่อ

```bash
npm install better-sqlite3
```

```javascript
const Database = require('better-sqlite3');
const db = new Database('myapp.db');

// เปิดใช้ WAL mode สำหรับ performance ที่ดีขึ้น
db.pragma('journal_mode = WAL');
```

### สร้างตาราง

```javascript
// สร้างตาราง users
db.exec(`
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL UNIQUE,
    password TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  )
`);

// สร้างตาราง posts
db.exec(`
  CREATE TABLE IF NOT EXISTS posts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    body TEXT NOT NULL,
    author_id INTEGER NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (author_id) REFERENCES users(id)
  )
`);
```

### CRUD Operations

```javascript
// CREATE - เพิ่มข้อมูล
const insertUser = db.prepare(
  'INSERT INTO users (username, email, password) VALUES (?, ?, ?)'
);
insertUser.run('john', 'john@email.com', hashedPassword);

// READ - อ่านข้อมูล
const getUser = db.prepare('SELECT * FROM users WHERE username = ?');
const user = getUser.get('john');

const getAllUsers = db.prepare('SELECT * FROM users');
const users = getAllUsers.all();

// UPDATE - แก้ไขข้อมูล
const updateUser = db.prepare(
  'UPDATE users SET email = ? WHERE id = ?'
);
updateUser.run('newemail@email.com', 1);

// DELETE - ลบข้อมูล
const deleteUser = db.prepare('DELETE FROM users WHERE id = ?');
deleteUser.run(1);
```

---

## 6. Cookies - คุกกี้

### Cookies คืออะไร?

- **ข้อมูลขนาดเล็ก** ที่ Server ส่งไปเก็บไว้ที่ Browser ของผู้ใช้
- ถูกส่งกลับมาพร้อมกับทุก Request อัตโนมัติ
- ใช้สำหรับ: **Session Management**, **User Preferences**, **Tracking**

### การใช้งาน Cookies กับ Express

```bash
npm install cookie-parser
```

```javascript
const cookieParser = require('cookie-parser');
app.use(cookieParser());

// ตั้งค่า Cookie
app.get('/set-cookie', (req, res) => {
  res.cookie('username', 'john', {
    httpOnly: true,    // ป้องกันการเข้าถึงจาก JavaScript (XSS)
    secure: true,      // ส่งเฉพาะผ่าน HTTPS
    maxAge: 86400000,  // อายุ 1 วัน (มิลลิวินาที)
    sameSite: 'strict' // ป้องกัน CSRF
  });
  res.send('Cookie has been set');
});

// อ่าน Cookie
app.get('/get-cookie', (req, res) => {
  const username = req.cookies.username;
  res.send(`Hello, ${username}`);
});

// ลบ Cookie
app.get('/clear-cookie', (req, res) => {
  res.clearCookie('username');
  res.send('Cookie has been cleared');
});
```

### Cookie Options ที่สำคัญ

| Option | คำอธิบาย |
|--------|----------|
| `httpOnly` | ป้องกันไม่ให้ JavaScript เข้าถึง (ป้องกัน XSS) |
| `secure` | ส่ง Cookie เฉพาะผ่าน HTTPS เท่านั้น |
| `maxAge` | กำหนดอายุของ Cookie (มิลลิวินาที) |
| `sameSite` | ป้องกัน CSRF (`strict`, `lax`, `none`) |
| `path` | กำหนดเส้นทางที่ Cookie จะถูกส่ง |
| `domain` | กำหนดโดเมนที่ Cookie ใช้ได้ |

---

## 7. JSON Web Tokens (JWT)

### JWT คืออะไร?

- **มาตรฐานสำหรับ Token-based Authentication**
- เป็น String ที่ถูกเข้ารหัส ประกอบด้วย 3 ส่วน:

```
xxxxx.yyyyy.zzzzz
  │       │       │
  │       │       └── Signature (ลายเซ็นยืนยัน)
  │       └────────── Payload (ข้อมูล)
  └────────────────── Header (อัลกอริทึมที่ใช้)
```

### ข้อดีของ JWT

- **Stateless** - Server ไม่ต้องเก็บ Session
- **Scalable** - รองรับหลาย Server ได้ง่าย
- **Portable** - ใช้ได้ข้าม Domain / Service
- **Self-contained** - มีข้อมูลที่จำเป็นในตัว

### การใช้งาน JWT

```bash
npm install jsonwebtoken
```

```javascript
const jwt = require('jsonwebtoken');
const SECRET_KEY = 'your-secret-key'; // ควรเก็บใน env variable

// สร้าง Token
function generateToken(user) {
  return jwt.sign(
    {
      userId: user.id,
      username: user.username
    },
    SECRET_KEY,
    { expiresIn: '24h' }  // Token หมดอายุใน 24 ชั่วโมง
  );
}

// ยืนยัน Token (Middleware)
function authenticateToken(req, res, next) {
  const token = req.cookies.token;

  if (!token) {
    return res.status(401).json({ error: 'Access denied' });
  }

  try {
    const decoded = jwt.verify(token, SECRET_KEY);
    req.user = decoded; // เพิ่มข้อมูล user ลงใน request
    next();
  } catch (err) {
    return res.status(403).json({ error: 'Invalid token' });
  }
}
```

### Flow การทำงานของ JWT

```
1. ผู้ใช้ Login ด้วย username/password
           │
           ▼
2. Server ตรวจสอบ credentials
           │
           ▼
3. Server สร้าง JWT Token
           │
           ▼
4. ส่ง Token กลับไปเก็บใน Cookie (httpOnly)
           │
           ▼
5. ทุก Request ต่อไป → Cookie ส่ง Token มาอัตโนมัติ
           │
           ▼
6. Server ตรวจสอบ Token → อนุญาตหรือปฏิเสธ
```

---

## 8. Letting Users Log In - ระบบเข้าสู่ระบบ

### Login Route

```javascript
app.post('/login', async (req, res) => {
  const { username, password } = req.body;

  // 1. หา user จากฐานข้อมูล
  const lookupUser = db.prepare('SELECT * FROM users WHERE username = ?');
  const user = lookupUser.get(username);

  if (!user) {
    return res.render('login', {
      errors: ['Invalid username or password']
    });
  }

  // 2. เปรียบเทียบ password กับ hash ที่เก็บไว้
  const isMatch = await bcrypt.compare(password, user.password);

  if (!isMatch) {
    return res.render('login', {
      errors: ['Invalid username or password']
    });
  }

  // 3. สร้าง JWT Token
  const token = generateToken(user);

  // 4. เก็บ Token ใน Cookie
  res.cookie('token', token, {
    httpOnly: true,
    secure: true,
    maxAge: 86400000 // 1 วัน
  });

  // 5. Redirect ไปหน้าหลัก
  res.redirect('/dashboard');
});
```

### Logout Route

```javascript
app.get('/logout', (req, res) => {
  res.clearCookie('token');
  res.redirect('/');
});
```

### Protected Routes (เส้นทางที่ต้อง Login)

```javascript
// ใช้ middleware authenticateToken สำหรับ route ที่ต้องการ auth
app.get('/dashboard', authenticateToken, (req, res) => {
  res.render('dashboard', { user: req.user });
});

app.get('/profile', authenticateToken, (req, res) => {
  const user = db.prepare('SELECT * FROM users WHERE id = ?')
    .get(req.user.userId);
  res.render('profile', { user });
});
```

---

## 9. Blog Posts CRUD - ระบบจัดการบทความ

### CRUD คืออะไร?

| Operation | HTTP Method | คำอธิบาย |
|-----------|-------------|----------|
| **C**reate | POST | สร้างข้อมูลใหม่ |
| **R**ead | GET | ดึง/อ่านข้อมูล |
| **U**pdate | PUT/PATCH | แก้ไขข้อมูล |
| **D**elete | DELETE | ลบข้อมูล |

### CREATE - สร้างบทความ

```javascript
// หน้า Form สร้างบทความ
app.get('/posts/create', authenticateToken, (req, res) => {
  res.render('create-post');
});

// บันทึกบทความ
app.post('/posts/create', authenticateToken, (req, res) => {
  const { title, body } = req.body;
  const errors = [];

  if (!title || title.trim() === '') errors.push('Title is required');
  if (!body || body.trim() === '') errors.push('Body is required');

  if (errors.length > 0) {
    return res.render('create-post', { errors });
  }

  const insertPost = db.prepare(
    'INSERT INTO posts (title, body, author_id) VALUES (?, ?, ?)'
  );
  const result = insertPost.run(title, body, req.user.userId);

  res.redirect(`/posts/${result.lastInsertRowid}`);
});
```

### READ - อ่านบทความ

```javascript
// แสดงบทความทั้งหมด
app.get('/posts', (req, res) => {
  const posts = db.prepare(`
    SELECT posts.*, users.username 
    FROM posts 
    JOIN users ON posts.author_id = users.id 
    ORDER BY posts.created_at DESC
  `).all();

  res.render('posts', { posts });
});

// แสดงบทความเดียว
app.get('/posts/:id', (req, res) => {
  const post = db.prepare(`
    SELECT posts.*, users.username 
    FROM posts 
    JOIN users ON posts.author_id = users.id 
    WHERE posts.id = ?
  `).get(req.params.id);

  if (!post) return res.status(404).send('Post not found');

  res.render('single-post', { post });
});
```

### UPDATE - แก้ไขบทความ

```javascript
// หน้า Form แก้ไข
app.get('/posts/:id/edit', authenticateToken, (req, res) => {
  const post = db.prepare('SELECT * FROM posts WHERE id = ?')
    .get(req.params.id);

  // ตรวจสอบว่าเป็นเจ้าของบทความ
  if (post.author_id !== req.user.userId) {
    return res.status(403).send('Forbidden');
  }

  res.render('edit-post', { post });
});

// บันทึกการแก้ไข
app.post('/posts/:id/edit', authenticateToken, (req, res) => {
  const { title, body } = req.body;
  const post = db.prepare('SELECT * FROM posts WHERE id = ?')
    .get(req.params.id);

  if (post.author_id !== req.user.userId) {
    return res.status(403).send('Forbidden');
  }

  db.prepare('UPDATE posts SET title = ?, body = ? WHERE id = ?')
    .run(title, body, req.params.id);

  res.redirect(`/posts/${req.params.id}`);
});
```

### DELETE - ลบบทความ

```javascript
app.post('/posts/:id/delete', authenticateToken, (req, res) => {
  const post = db.prepare('SELECT * FROM posts WHERE id = ?')
    .get(req.params.id);

  if (post.author_id !== req.user.userId) {
    return res.status(403).send('Forbidden');
  }

  db.prepare('DELETE FROM posts WHERE id = ?').run(req.params.id);

  res.redirect('/posts');
});
```

---

## 10. Deploying Our App - การ Deploy แอปพลิเคชัน

### การเตรียมแอปสำหรับ Production

#### 1. Environment Variables

```bash
npm install dotenv
```

```javascript
// .env
PORT=3000
SECRET_KEY=my-super-secret-key
NODE_ENV=production
DATABASE_URL=./myapp.db
```

```javascript
// server.js
require('dotenv').config();

const PORT = process.env.PORT || 3000;
const SECRET_KEY = process.env.SECRET_KEY;
```

#### 2. Security Headers

```bash
npm install helmet
```

```javascript
const helmet = require('helmet');
app.use(helmet()); // เพิ่ม Security Headers อัตโนมัติ
```

#### 3. Rate Limiting

```bash
npm install express-rate-limit
```

```javascript
const rateLimit = require('express-rate-limit');

const limiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 นาที
  max: 100 // จำกัด 100 requests ต่อ IP
});

app.use(limiter);
```

### การ Deploy บน Linux Server

#### ขั้นตอนการตั้งค่า Server

```bash
# 1. อัปเดต Server
sudo apt update && sudo apt upgrade -y

# 2. ติดตั้ง Node.js
curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
sudo apt install -y nodejs

# 3. Clone โปรเจกต์
git clone https://github.com/your-repo/my-backend-app.git
cd my-backend-app

# 4. ติดตั้ง Dependencies
npm install --production

# 5. ตั้งค่า Environment Variables
cp .env.example .env
nano .env
```

#### ใช้ PM2 สำหรับ Process Management

```bash
# ติดตั้ง PM2
npm install -g pm2

# เริ่ม App
pm2 start server.js --name "my-app"

# ให้ App เริ่มอัตโนมัติเมื่อ Server restart
pm2 startup
pm2 save

# คำสั่งที่ใช้บ่อย
pm2 status        # ดูสถานะ
pm2 logs           # ดู log
pm2 restart all    # restart ทั้งหมด
pm2 stop my-app    # หยุด app
```

#### ตั้งค่า Nginx เป็น Reverse Proxy

```nginx
# /etc/nginx/sites-available/myapp
server {
    listen 80;
    server_name yourdomain.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
# เปิดใช้งาน Site
sudo ln -s /etc/nginx/sites-available/myapp /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

#### ตั้งค่า SSL ด้วย Let's Encrypt

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d yourdomain.com
```

---

## 📝 สรุปสิ่งที่เรียนรู้

```
Backend Development Flow:
═══════════════════════════════════════════════════════

  [Client/Browser]
        │
        │  HTTP Request
        ▼
  [Express.js Server]
        │
        ├── Middleware (cookie-parser, helmet, etc.)
        │
        ├── Routes (/register, /login, /posts, etc.)
        │       │
        │       ├── Authentication (JWT + Cookies)
        │       │
        │       └── Business Logic
        │               │
        │               ▼
        │         [SQLite Database]
        │               │
        │               ▼
        │         CRUD Operations
        │
        ▼
  [HTTP Response → Client]
```

### เทคโนโลยีที่ใช้ในวิดีโอ

| เทคโนโลยี | ประเภท | หน้าที่ |
|-----------|--------|---------|
| **Node.js** | Runtime | รัน JavaScript บน Server |
| **Express.js** | Framework | จัดการ Routes, Middleware |
| **SQLite** | Database | เก็บข้อมูลผู้ใช้และบทความ |
| **bcrypt** | Library | Hash รหัสผ่าน |
| **JWT** | Standard | Token-based Authentication |
| **cookie-parser** | Middleware | จัดการ Cookies |
| **dotenv** | Library | จัดการ Environment Variables |
| **helmet** | Middleware | Security Headers |
| **PM2** | Tool | Process Management |
| **Nginx** | Web Server | Reverse Proxy |
| **Let's Encrypt** | Service | SSL Certificate |

### Concepts สำคัญ

- ✅ **Client-Server Architecture** - สถาปัตยกรรมแบบ Client-Server
- ✅ **RESTful Routing** - การจัดการเส้นทาง URL
- ✅ **Middleware Pattern** - การใช้ Middleware ในการจัดการ Request
- ✅ **Password Hashing** - การ Hash รหัสผ่านเพื่อความปลอดภัย
- ✅ **Token-based Auth (JWT)** - การยืนยันตัวตนด้วย Token
- ✅ **Cookie Management** - การจัดการ Cookie อย่างปลอดภัย
- ✅ **Database CRUD** - การจัดการข้อมูลพื้นฐาน
- ✅ **Input Validation** - การตรวจสอบข้อมูลก่อนประมวลผล
- ✅ **Authorization** - การตรวจสอบสิทธิ์การเข้าถึง
- ✅ **Deployment** - การนำแอปขึ้น Production Server
- ✅ **Security Best Practices** - แนวปฏิบัติด้านความปลอดภัย

---

> 💡 **Tip:** วิดีโอนี้เหมาะสำหรับผู้เริ่มต้นที่ต้องการเข้าใจภาพรวมของ Backend Development โดยใช้ Node.js + Express.js เป็นจุดเริ่มต้น แล้วค่อยขยายไปใช้ Framework อื่นๆ หรือ Database ที่ซับซ้อนกว่านี้ได้
