# Networking & Protocols + Web Servers (DevOps)

---

## 1. Networking Protocols ที่ DevOps ต้องรู้

```
| Protocol  | Port  | ใช้ทำอะไร                          |
|-----------|-------|------------------------------------|
| HTTP      | 80    | Web traffic (ไม่เข้ารหัส)          |
| HTTPS     | 443   | Web traffic (เข้ารหัส TLS)         |
| SSH       | 22    | Remote access, secure shell        |
| FTP/SFTP  | 21/22 | File transfer                      |
| DNS       | 53    | Domain → IP resolution             |
| SMTP      | 25/587| ส่ง email                          |
| IMAP      | 143/993| รับ email                         |
```

### OSI Model (7 Layers)

```
Layer 7 — Application   (HTTP, HTTPS, DNS, SSH, FTP)
Layer 6 — Presentation  (SSL/TLS, encryption)
Layer 5 — Session       (จัดการ connections)
Layer 4 — Transport     (TCP, UDP — ports)
Layer 3 — Network       (IP, routing)
Layer 2 — Data Link     (MAC address, switches)
Layer 1 — Physical      (สายเคเบิล, WiFi)

DevOps ส่วนใหญ่ทำงานกับ Layer 3-7
```

### SSH — ต้องรู้!

```bash
# เชื่อมต่อ server
ssh user@192.168.1.100
ssh -i ~/.ssh/mykey.pem ec2-user@aws-instance

# สร้าง SSH Key
ssh-keygen -t ed25519 -C "your-email@example.com"
# → สร้าง ~/.ssh/id_ed25519 (private) + ~/.ssh/id_ed25519.pub (public)

# Copy public key ไป server
ssh-copy-id user@server

# SSH Config (ง่ายขึ้น!)
# ~/.ssh/config
Host prod-web
    HostName 10.0.1.50
    User deploy
    IdentityFile ~/.ssh/prod_key
    Port 22

Host staging
    HostName 10.0.2.50
    User deploy
    IdentityFile ~/.ssh/staging_key

# ใช้: ssh prod-web (แทน ssh -i ~/.ssh/prod_key deploy@10.0.1.50)

# SSH Tunneling (Port Forwarding)
ssh -L 5432:db-server:5432 user@bastion
# → เข้า localhost:5432 = เข้า db-server:5432 ผ่าน bastion

# SCP — copy file ผ่าน SSH
scp file.tar.gz user@server:/tmp/
scp -r ./config/ user@server:/etc/app/
```

### DNS สำหรับ DevOps

```
DNS Records ที่ต้องรู้:
| Record | ทำอะไร                              | ตัวอย่าง                    |
|--------|-------------------------------------|-----------------------------|
| A      | Domain → IPv4                       | example.com → 1.2.3.4      |
| AAAA   | Domain → IPv6                       | example.com → 2001:db8::1  |
| CNAME  | Alias → Domain อื่น                 | www → example.com           |
| MX     | Mail server                         | mail.example.com            |
| TXT    | Text data (SPF, DKIM, verification) | v=spf1 include:...          |
| NS     | Name server                         | ns1.cloudflare.com          |

# ตรวจสอบ DNS
dig example.com A
dig example.com MX
nslookup example.com
```

### Firewall & Security

```bash
# UFW (Ubuntu — ง่าย)
sudo ufw enable
sudo ufw allow 22/tcp       # SSH
sudo ufw allow 80/tcp       # HTTP
sudo ufw allow 443/tcp      # HTTPS
sudo ufw deny 3306/tcp      # ปิด MySQL จากภายนอก
sudo ufw status

# iptables (advanced)
sudo iptables -A INPUT -p tcp --dport 22 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 443 -j ACCEPT
sudo iptables -A INPUT -j DROP            # ปิดทุกอย่างที่เหลือ
```

## 2. Web Server (What is and how to setup X)

```
เนื้อหาหลักอยู่ใน 06-Learn-about-Web-Servers แล้ว

สิ่งที่ DevOps ต้องรู้เพิ่ม:
├── Forward Proxy   — client → proxy → internet (ปิดบัง client)
├── Reverse Proxy   — internet → proxy → server (ปิดบัง server) ← ใช้บ่อย!
├── Caching Server  — เก็บ cache ที่ proxy layer (Varnish, Nginx)
├── Load Balancer   — กระจาย traffic (L4/L7)
└── Firewall        — กรอง traffic (WAF, iptables)

Forward Proxy vs Reverse Proxy:
Client → [Forward Proxy] → Internet
         ↑ ปิดบัง client IP
         ตัวอย่าง: VPN, corporate proxy

Internet → [Reverse Proxy] → Server
           ↑ ปิดบัง server IP
           ตัวอย่าง: Nginx, Cloudflare, AWS ALB
```

### Email Protocols (ภาพรวม)

```
SMTP  — ส่ง email (port 25/587)
IMAP  — รับ email แบบ sync (port 143/993)
POP3  — รับ email แบบ download (port 110/995)

SPF   — บอกว่า IP ไหนส่ง email แทน domain ได้
DKIM  — เซ็นชื่อ email ด้วย cryptographic key
DMARC — กฎว่าจะทำอย่างไรเมื่อ SPF/DKIM ไม่ผ่าน

DevOps ต้องตั้ง DNS records เหล่านี้เพื่อป้องกัน email ถูก mark เป็น spam
```
