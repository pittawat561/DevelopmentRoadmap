# Web Servers — เว็บเซิร์ฟเวอร์

> ครอบคลุม Nginx, Apache, Caddy, MS IIS

---

## 1. Web Server คืออะไร

Web Server คือซอฟต์แวร์ที่ **รับ HTTP request จาก client (browser) แล้วส่ง response กลับ** เช่น HTML, CSS, JS, รูปภาพ, JSON

```
Browser (Client)
    │
    │  HTTP Request: GET /index.html
    ▼
┌─────────────────┐
│   Web Server    │  ← รับ request, ประมวลผล, ส่ง response
│  (Nginx/Apache) │
└────────┬────────┘
         │
    ┌────┴────┐
    │ Static  │  → ส่งไฟล์โดยตรง (HTML, CSS, images)
    │ Dynamic │  → ส่งต่อไปยัง application server (Node.js, .NET, Python)
    └─────────┘
```

### หน้าที่หลัก

```
1. Serve Static Files     — ส่งไฟล์ HTML, CSS, JS, รูปภาพ
2. Reverse Proxy          — ส่งต่อ request ไปยัง backend app
3. Load Balancing         — กระจาย traffic ไปหลาย servers
4. SSL/TLS Termination    — จัดการ HTTPS certificates
5. Caching                — cache response เพื่อเร่งความเร็ว
6. Compression            — บีบอัด response (gzip, brotli)
7. Security               — ป้องกัน DDoS, rate limiting, access control
8. URL Rewriting          — เปลี่ยน URL (redirect, rewrite)
```

### Web Server vs Application Server

```
| Web Server              | Application Server         |
|------------------------|---------------------------|
| Nginx, Apache          | Node.js, Kestrel (.NET)   |
| ส่ง static files        | รันโค้ด business logic     |
| Reverse proxy          | จัดการ database            |
| Load balancing         | Authentication            |
| SSL termination        | API endpoints             |

ใช้ร่วมกัน:
Client → Nginx (Web Server) → Node.js/Kestrel (App Server) → Database

ทำไมต้องมี Web Server หน้า App Server?
- Nginx จัดการ static files ได้เร็วกว่ามาก
- Nginx รับ connection พร้อมกันได้มากกว่า
- SSL termination ที่ Nginx ลด load ของ App Server
- Load balancing กระจายไปหลาย App Server instances
```

---

## 2. Nginx

### Nginx คืออะไร

Nginx (อ่านว่า "Engine-X") คือ web server ที่ **ได้รับความนิยมมากที่สุด** ออกแบบมาให้รองรับ connection จำนวนมากด้วยการใช้ memory น้อย

```
สถิติ:
- เว็บไซต์ที่มี traffic สูงส่วนใหญ่ใช้ Nginx
- ใช้ event-driven architecture (ไม่สร้าง thread per connection)
- รองรับ 10,000+ concurrent connections ด้วย RAM น้อย
```

### Architecture

```
Apache (process-per-connection):
Request 1 → Thread 1
Request 2 → Thread 2        ← ใช้ memory มากขึ้นตาม connection
Request 3 → Thread 3
...
Request 10000 → Thread 10000  ← ❌ memory เต็ม!

Nginx (event-driven):
                    ┌── Request 1
Worker Process 1 ←──┼── Request 2     ← 1 process รับหลาย connections
                    ├── Request 3
                    └── Request ...

                    ┌── Request 5001
Worker Process 2 ←──┼── Request 5002   ← ใช้ memory น้อยมาก ✅
                    └── Request ...

ผลลัพธ์: Nginx ใช้ RAM ~2-3 MB ต่อ 10,000 connections
```

### การติดตั้ง

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install nginx
sudo systemctl start nginx
sudo systemctl enable nginx

# CentOS/RHEL
sudo yum install nginx

# macOS
brew install nginx

# Windows — ดาวน์โหลดจาก nginx.org

# ตรวจสอบ
nginx -v
curl http://localhost
```

### Configuration พื้นฐาน

```nginx
# /etc/nginx/nginx.conf

# จำนวน worker processes (ตั้งตาม CPU cores)
worker_processes auto;

events {
    worker_connections 1024;    # connections ต่อ worker
}

http {
    include       mime.types;
    default_type  application/octet-stream;

    # Performance
    sendfile        on;
    keepalive_timeout  65;
    gzip  on;
    gzip_types text/plain text/css application/json application/javascript;

    # รวม config จาก sites-enabled
    include /etc/nginx/sites-enabled/*;
}
```

### Serve Static Files

```nginx
# /etc/nginx/sites-available/mysite.conf

server {
    listen 80;
    server_name example.com www.example.com;

    # Document root
    root /var/www/mysite;
    index index.html;

    # Static files
    location / {
        try_files $uri $uri/ =404;
    }

    # Cache static assets
    location ~* \.(css|js|png|jpg|jpeg|gif|ico|svg|woff2)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # ป้องกันไม่ให้เข้าถึงไฟล์ที่ซ่อน
    location ~ /\. {
        deny all;
    }
}
```

### Reverse Proxy (สำคัญมาก!)

```nginx
# ส่งต่อ request ไปยัง Node.js app ที่พอร์ต 3000
server {
    listen 80;
    server_name api.example.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;

        # ส่ง headers ที่จำเป็น
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket support
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}

# ส่งต่อไปยัง .NET Kestrel (พอร์ต 5000)
server {
    listen 80;
    server_name app.example.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Load Balancing

```nginx
# กระจาย traffic ไปหลาย servers
upstream backend_servers {
    # Round Robin (default) — วนรอบ
    server 10.0.0.1:3000;
    server 10.0.0.2:3000;
    server 10.0.0.3:3000;
}

# Weighted — server ที่แรงกว่ารับมากกว่า
upstream backend_weighted {
    server 10.0.0.1:3000 weight=5;    # รับ 5 ส่วน
    server 10.0.0.2:3000 weight=3;    # รับ 3 ส่วน
    server 10.0.0.3:3000 weight=1;    # รับ 1 ส่วน
}

# Least Connections — ส่งไป server ที่ว่างที่สุด
upstream backend_least {
    least_conn;
    server 10.0.0.1:3000;
    server 10.0.0.2:3000;
}

# IP Hash — client เดิมไป server เดิมเสมอ (sticky sessions)
upstream backend_sticky {
    ip_hash;
    server 10.0.0.1:3000;
    server 10.0.0.2:3000;
}

server {
    listen 80;

    location / {
        proxy_pass http://backend_servers;
    }
}
```

### HTTPS / SSL

```nginx
server {
    listen 443 ssl http2;
    server_name example.com;

    # SSL Certificate (ใช้ Let's Encrypt ฟรี)
    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;

    # SSL Security Settings
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # HSTS
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    location / {
        proxy_pass http://localhost:3000;
    }
}

# Redirect HTTP → HTTPS
server {
    listen 80;
    server_name example.com;
    return 301 https://$server_name$request_uri;
}
```

### Rate Limiting

```nginx
# จำกัด request rate
http {
    # กำหนด zone: 10 requests/วินาที per IP
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;

    server {
        location /api/ {
            limit_req zone=api_limit burst=20 nodelay;
            # burst=20: อนุญาตให้เกิน 20 requests ก่อนจะ reject
            # nodelay: ไม่ต้องรอ queue
            proxy_pass http://localhost:3000;
        }
    }
}
```

### คำสั่ง Nginx ที่ใช้บ่อย

```bash
# ตรวจสอบ config ถูกต้อง
sudo nginx -t

# Reload config (ไม่ต้อง restart)
sudo nginx -s reload

# Start/Stop/Restart
sudo systemctl start nginx
sudo systemctl stop nginx
sudo systemctl restart nginx

# ดู logs
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log

# Enable site config
sudo ln -s /etc/nginx/sites-available/mysite.conf /etc/nginx/sites-enabled/
```

---

## 3. Apache HTTP Server

### Apache คืออะไร

Apache (httpd) คือ web server เก่าแก่ที่ใช้กันมายาวนานที่สุด มีความยืดหยุ่นสูงด้วยระบบ modules

```
ข้อดี:
✅ Module system — เพิ่มความสามารถได้ง่าย
✅ .htaccess — ตั้งค่าได้ per-directory
✅ เอกสาร/ตัวอย่างมากมาย
✅ รองรับ PHP ดีมาก (mod_php)

ข้อเสีย:
❌ ใช้ memory มากกว่า Nginx (process/thread per connection)
❌ ช้ากว่า Nginx สำหรับ static files
❌ ตั้งค่า .htaccess ทำให้ช้าลง
```

### Configuration

```apache
# /etc/apache2/sites-available/mysite.conf

<VirtualHost *:80>
    ServerName example.com
    ServerAlias www.example.com
    DocumentRoot /var/www/mysite

    <Directory /var/www/mysite>
        AllowOverride All
        Require all granted
    </Directory>

    # Logging
    ErrorLog ${APACHE_LOG_DIR}/error.log
    CustomLog ${APACHE_LOG_DIR}/access.log combined
</VirtualHost>
```

### Reverse Proxy

```apache
# เปิด modules
# sudo a2enmod proxy proxy_http

<VirtualHost *:80>
    ServerName api.example.com

    ProxyPreserveHost On
    ProxyPass / http://localhost:3000/
    ProxyPassReverse / http://localhost:3000/
</VirtualHost>
```

### .htaccess (per-directory config)

```apache
# .htaccess — ใช้ได้ทุก directory

# Redirect HTTP → HTTPS
RewriteEngine On
RewriteCond %{HTTPS} off
RewriteRule ^(.*)$ https://%{HTTP_HOST}%{REQUEST_URI} [L,R=301]

# URL Rewriting (SPA — React/Vue/Angular)
RewriteEngine On
RewriteBase /
RewriteRule ^index\.html$ - [L]
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule . /index.html [L]

# ป้องกันเข้าถึงไฟล์
<Files .env>
    Require all denied
</Files>
```

---

## 4. Caddy

### Caddy คืออะไร

Caddy คือ web server สมัยใหม่ที่เด่นเรื่อง **HTTPS อัตโนมัติ** ไม่ต้องตั้งค่า SSL certificate เลย

```
ข้อดี:
✅ HTTPS อัตโนมัติ (Let's Encrypt built-in!)
✅ Config ง่ายมาก (Caddyfile)
✅ HTTP/2 & HTTP/3 default
✅ เขียนด้วย Go — binary ไฟล์เดียว ไม่ต้อง install dependencies

ข้อเสีย:
❌ Community เล็กกว่า Nginx/Apache
❌ Module ecosystem น้อยกว่า
❌ อาจไม่เหมาะกับ use case ที่ซับซ้อนมาก
```

### Caddyfile (Config ง่ายมาก!)

```
# Caddyfile

# Static site — แค่ 2 บรรทัด!
example.com {
    root * /var/www/mysite
    file_server
}
# HTTPS อัตโนมัติ! ไม่ต้องตั้งค่า SSL!

# Reverse Proxy — 2 บรรทัด
api.example.com {
    reverse_proxy localhost:3000
}

# Load Balancing
app.example.com {
    reverse_proxy localhost:3001 localhost:3002 localhost:3003
}

# SPA (React/Vue/Angular)
myapp.example.com {
    root * /var/www/myapp
    try_files {path} /index.html
    file_server
}

# Multiple sites ใน Caddyfile เดียว
:80 {
    respond "Hello, World!"
}

blog.example.com {
    root * /var/www/blog
    file_server
}
```

### เทียบ Config: Caddy vs Nginx

```
# Caddy — Reverse Proxy + HTTPS อัตโนมัติ
api.example.com {
    reverse_proxy localhost:3000
}

# Nginx — ต้องตั้งค่าเยอะกว่า
server {
    listen 443 ssl http2;
    server_name api.example.com;
    ssl_certificate /etc/letsencrypt/live/api.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.example.com/privkey.pem;
    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 5. Microsoft IIS (Internet Information Services)

### IIS คืออะไร

IIS คือ web server ของ Microsoft ที่มาพร้อม Windows Server เหมาะสำหรับ .NET applications

```
ข้อดี:
✅ รวมกับ Windows Server / Active Directory
✅ GUI จัดการง่าย (IIS Manager)
✅ รองรับ ASP.NET / .NET Core ดีที่สุด
✅ Windows Authentication built-in

ข้อเสีย:
❌ Windows only
❌ ใช้ resource มากกว่า Nginx
❌ License cost (Windows Server)
```

### IIS กับ .NET

```
สถาปัตยกรรม:

Client → IIS → ASP.NET Core Module → Kestrel → App

IIS ทำหน้าที่:
- รับ HTTP/HTTPS requests
- SSL termination
- Static file serving
- ส่งต่อไปยัง Kestrel (reverse proxy)

Kestrel ทำหน้าที่:
- รัน .NET application
- ประมวลผล business logic
```

### web.config

```xml
<!-- web.config สำหรับ ASP.NET Core -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*"
           modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments=".\MyApp.dll"
                stdoutLogEnabled="false"
                hostingModel="InProcess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>

    <!-- URL Rewrite (HTTPS redirect) -->
    <rewrite>
      <rules>
        <rule name="HTTPS Redirect" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="^OFF$" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

---

## 6. เปรียบเทียบทั้ง 4 ตัว

```
| หัวข้อ              | Nginx          | Apache         | Caddy          | IIS            |
|--------------------|----------------|----------------|----------------|----------------|
| Architecture       | Event-driven   | Process/Thread | Event-driven   | Process/Thread |
| Performance        | ⭐⭐⭐⭐⭐     | ⭐⭐⭐         | ⭐⭐⭐⭐       | ⭐⭐⭐         |
| Memory Usage       | ต่ำมาก         | สูง            | ต่ำ             | สูง            |
| Config ง่าย        | ปานกลาง        | ปานกลาง        | ⭐⭐⭐⭐⭐     | GUI ง่าย       |
| HTTPS อัตโนมัติ     | ❌ (ใช้ certbot)| ❌ (ใช้ certbot)| ✅ Built-in    | ❌ (manual)     |
| Static Files       | ⭐⭐⭐⭐⭐     | ⭐⭐⭐         | ⭐⭐⭐⭐       | ⭐⭐⭐         |
| Reverse Proxy      | ⭐⭐⭐⭐⭐     | ⭐⭐⭐         | ⭐⭐⭐⭐       | ⭐⭐⭐         |
| Load Balancing     | ⭐⭐⭐⭐⭐     | ⭐⭐           | ⭐⭐⭐         | ⭐⭐⭐         |
| OS Support         | Linux/Mac/Win  | Linux/Mac/Win  | Linux/Mac/Win  | Windows only   |
| .NET Support       | Reverse Proxy  | Reverse Proxy  | Reverse Proxy  | ⭐⭐⭐⭐⭐     |
| PHP Support        | FastCGI        | mod_php ⭐     | FastCGI        | FastCGI        |
| Community          | ⭐⭐⭐⭐⭐     | ⭐⭐⭐⭐       | ⭐⭐           | ⭐⭐⭐         |

เลือกอะไร:
├── High performance + reverse proxy?        → Nginx ✅
├── Shared hosting + PHP?                    → Apache
├── ง่ายที่สุด + HTTPS อัตโนมัติ?              → Caddy
├── Windows + .NET production?               → IIS
├── ไม่แน่ใจ?                                 → Nginx ✅ (ใช้มากที่สุด)
└── โปรเจกต์เล็ก/ส่วนตัว?                     → Caddy (ง่าย + HTTPS ฟรี)
```

---

## 7. สรุป

```
สิ่งที่ต้องรู้เป็นอย่างน้อย:
1. Nginx — ใช้มากที่สุด, ต้องรู้ reverse proxy + config พื้นฐาน
2. แนวคิด reverse proxy, load balancing, SSL termination
3. เลือก 1 ตัวให้เชี่ยวชาญ (แนะนำ Nginx)

สำหรับ .NET Developer:
- Development: Kestrel (built-in)
- Production Linux: Nginx + Kestrel
- Production Windows: IIS + Kestrel
```
