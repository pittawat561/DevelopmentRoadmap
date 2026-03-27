# Linux Deep Dive สำหรับ DevOps — เนื้อหาเต็ม

> อ่านจบแล้วใช้งาน Linux Server ได้จริง

---

## 1. โครงสร้างไฟล์ Linux (File System Hierarchy)

ใน Linux ทุกอย่างเริ่มจาก `/` (root directory) ไม่มี Drive Letter เหมือน Windows (ไม่มี C:\ D:\)

```
/                       ← root (จุดเริ่มต้นของทุกอย่าง)
├── /bin                ← คำสั่งพื้นฐาน (ls, cp, mv, cat) — ทุก user ใช้ได้
├── /sbin               ← คำสั่งสำหรับ admin (iptables, fdisk, reboot)
├── /etc                ← ไฟล์ config ทั้งหมดอยู่ที่นี่ ⭐
│   ├── /etc/nginx/     ← config ของ nginx
│   ├── /etc/ssh/       ← config ของ SSH
│   ├── /etc/hosts      ← จับคู่ hostname กับ IP
│   └── /etc/passwd     ← ข้อมูล users ทั้งหมด
├── /home               ← home directory ของ users ปกติ
│   ├── /home/john/
│   └── /home/deploy/
├── /root               ← home directory ของ root user
├── /var                ← ข้อมูลที่เปลี่ยนแปลงบ่อย
│   ├── /var/log/       ← log files ทั้งหมด ⭐
│   ├── /var/www/       ← เว็บไซต์ (Apache/Nginx)
│   └── /var/lib/       ← ข้อมูลของ services (Docker, MySQL)
├── /tmp                ← ไฟล์ชั่วคราว (ลบเมื่อ reboot)
├── /opt                ← ซอฟต์แวร์ third-party ที่ติดตั้งเพิ่ม
├── /usr                ← โปรแกรมและ libraries ที่ users ติดตั้ง
│   ├── /usr/bin/       ← คำสั่งที่ติดตั้งเพิ่ม
│   └── /usr/local/     ← ซอฟต์แวร์ที่ compile เอง
├── /proc               ← ข้อมูล process จาก kernel (virtual filesystem)
├── /dev                ← devices (disk, USB, etc.)
└── /mnt, /media        ← mount point สำหรับ external drives

สิ่งที่ DevOps ใช้บ่อย:
- /etc/     → แก้ config ทุกอย่าง
- /var/log/ → ดู logs หาปัญหา
- /opt/     → ติดตั้ง app ของเรา
- /tmp/     → เก็บไฟล์ชั่วคราว
```

---

## 2. Users, Groups & Permissions — ระบบสิทธิ์

### ทำความเข้าใจ Users

```bash
# Linux แยก users ออกเป็น:
# 1. root (uid=0)       — มีสิทธิ์ทำทุกอย่าง (อันตราย!)
# 2. system users       — สำหรับ services (nginx, mysql, nobody)
# 3. regular users      — คนใช้ปกติ

# ดูข้อมูล user ปัจจุบัน
whoami                  # → deploy
id                      # → uid=1000(deploy) gid=1000(deploy) groups=1000(deploy),27(sudo)

# ดู users ทั้งหมดในระบบ
cat /etc/passwd
# แต่ละบรรทัด: username:x:uid:gid:comment:home_dir:shell
# deploy:x:1000:1000:Deploy User:/home/deploy:/bin/bash

# สร้าง user ใหม่
sudo useradd -m -s /bin/bash -G sudo deploy
# -m          = สร้าง home directory
# -s /bin/bash = ใช้ bash shell
# -G sudo     = เพิ่มเข้ากลุ่ม sudo (ใช้ sudo ได้)

# ตั้ง password
sudo passwd deploy

# ลบ user
sudo userdel -r deploy  # -r = ลบ home directory ด้วย

# สลับเป็น root
sudo su -               # เข้าเป็น root
sudo -i                 # เหมือนกัน
exit                    # กลับเป็น user เดิม

# รันคำสั่งเดียวในฐานะ root
sudo apt update         # รัน apt update ด้วยสิทธิ์ root
```

### ทำความเข้าใจ Permissions

```bash
# ดูสิทธิ์ของไฟล์
ls -la
# -rw-r--r--  1 deploy deploy  1234 Jan 15 10:00 config.yml
# drwxr-xr-x  2 deploy deploy  4096 Jan 15 10:00 scripts/
#
# แยกอ่าน:
# d rwx r-x r-x
# │ │   │   │
# │ │   │   └── Others (คนอื่น): r-x = อ่าน+execute ได้ เขียนไม่ได้
# │ │   └────── Group (กลุ่ม):    r-x = อ่าน+execute ได้ เขียนไม่ได้
# │ └────────── Owner (เจ้าของ):  rwx = อ่าน+เขียน+execute ได้ทั้งหมด
# └──────────── d = directory, - = file, l = symbolic link

# r (read)    = 4   → อ่านไฟล์ / ดูรายการใน directory
# w (write)   = 2   → แก้ไขไฟล์ / สร้าง-ลบไฟล์ใน directory
# x (execute) = 1   → รัน script / เข้า directory ได้

# ตัวอย่างเลขสิทธิ์:
# 755 = rwxr-xr-x → owner ทำได้ทุกอย่าง, คนอื่นอ่าน+execute
# 644 = rw-r--r-- → owner อ่าน+เขียน, คนอื่นอ่านอย่างเดียว
# 600 = rw------- → owner อ่าน+เขียน, คนอื่นทำอะไรไม่ได้เลย
# 700 = rwx------ → owner ทำได้ทุกอย่าง, คนอื่นทำอะไรไม่ได้เลย

# เปลี่ยนสิทธิ์
chmod 755 deploy.sh     # ใช้ตัวเลข
chmod +x deploy.sh      # เพิ่มสิทธิ์ execute ให้ทุกคน
chmod u+w file.txt      # เพิ่มสิทธิ์ write ให้ owner
chmod go-w file.txt     # ลบสิทธิ์ write จาก group และ others
chmod -R 755 /opt/app/  # -R = recursive (ทั้ง folder)

# เปลี่ยนเจ้าของ
sudo chown deploy:deploy /opt/app/
sudo chown -R www-data:www-data /var/www/  # ให้ nginx/apache เข้าถึงได้

# Best Practice สำหรับ DevOps:
# - Config files:    chmod 644 (owner read+write, others read)
# - Scripts:         chmod 755 (owner all, others read+execute)
# - Secret files:    chmod 600 (owner only!)
# - .ssh/id_rsa:     chmod 600 (SSH จะไม่ทำงานถ้าสิทธิ์ไม่ถูก!)
# - .ssh/ directory:  chmod 700
```

---

## 3. Package Management — ติดตั้งซอฟต์แวร์

```bash
# ===== APT (Ubuntu / Debian) =====

# อัปเดตรายการ packages (ทำก่อนติดตั้งเสมอ!)
sudo apt update

# อัปเดต packages ทั้งหมดเป็นเวอร์ชันล่าสุด
sudo apt upgrade -y             # -y = ตอบ yes อัตโนมัติ
sudo apt full-upgrade -y        # อัปเดตรวมถึง kernel

# ติดตั้ง package
sudo apt install nginx -y
sudo apt install nginx curl wget git -y   # หลายตัวพร้อมกัน

# ลบ package
sudo apt remove nginx           # ลบแต่เก็บ config
sudo apt purge nginx            # ลบทั้ง config
sudo apt autoremove             # ลบ dependencies ที่ไม่ใช้

# ค้นหา package
apt search nginx
apt show nginx                  # ข้อมูลรายละเอียด

# ดู packages ที่ติดตั้งแล้ว
apt list --installed
dpkg -l | grep nginx

# ===== YUM / DNF (RHEL / CentOS / Fedora) =====
sudo yum update                 # อัปเดต
sudo yum install nginx          # ติดตั้ง
sudo yum remove nginx           # ลบ

# ===== ติดตั้งจากไฟล์ .deb =====
wget https://example.com/app.deb
sudo dpkg -i app.deb
sudo apt install -f             # แก้ dependencies ที่ขาด
```

---

## 4. Systemd — จัดการ Services

Systemd คือ init system ที่จัดการ services ทั้งหมดใน Linux สมัยใหม่

```bash
# ===== คำสั่ง systemctl ที่ใช้ทุกวัน =====

# ดูสถานะ service
sudo systemctl status nginx
# ● nginx.service - A high performance web server
#    Loaded: loaded (/lib/systemd/system/nginx.service; enabled)
#    Active: active (running) since Mon 2024-01-15 10:00:00 UTC
#    Main PID: 12345 (nginx)

# เริ่ม / หยุด / restart
sudo systemctl start nginx      # เริ่ม
sudo systemctl stop nginx       # หยุด
sudo systemctl restart nginx    # หยุดแล้วเริ่มใหม่
sudo systemctl reload nginx     # โหลด config ใหม่ (ไม่ downtime)

# เปิด/ปิด auto-start ตอน boot
sudo systemctl enable nginx     # เปิดตอน boot
sudo systemctl disable nginx    # ไม่เปิดตอน boot

# ดู services ทั้งหมด
sudo systemctl list-units --type=service
sudo systemctl list-units --type=service --state=running

# ===== สร้าง Service ใหม่ (สำหรับ app ของเรา) =====

# สร้างไฟล์: /etc/systemd/system/myapp.service
sudo nano /etc/systemd/system/myapp.service
```

```ini
# /etc/systemd/system/myapp.service
[Unit]
Description=My Node.js Application
After=network.target             # เริ่มหลังจาก network พร้อม

[Service]
Type=simple
User=deploy                       # รันในฐานะ user deploy (ไม่ใช่ root!)
WorkingDirectory=/opt/myapp
ExecStart=/usr/bin/node server.js
Restart=on-failure                # restart อัตโนมัติถ้า crash
RestartSec=5                      # รอ 5 วินาทีก่อน restart

# Environment variables
Environment=NODE_ENV=production
Environment=PORT=3000

# Logging
StandardOutput=journal            # ส่ง stdout ไป journalctl
StandardError=journal

[Install]
WantedBy=multi-user.target        # เริ่มตอน boot
```

```bash
# หลังสร้างไฟล์ service แล้ว:
sudo systemctl daemon-reload      # โหลด config ใหม่
sudo systemctl start myapp        # เริ่ม service
sudo systemctl enable myapp       # เปิดตอน boot
sudo systemctl status myapp       # ตรวจสอบ

# ดู logs ของ service
sudo journalctl -u myapp -f       # follow (real-time)
sudo journalctl -u myapp --since "1 hour ago"
sudo journalctl -u myapp --since "2024-01-15" --until "2024-01-16"
sudo journalctl -u myapp -n 50    # 50 บรรทัดล่าสุด
```

---

## 5. การจัดการ Disk & Storage

```bash
# ===== ดูพื้นที่ดิสก์ =====

# ดูพื้นที่ทั้งหมด
df -h
# Filesystem      Size  Used Avail Use% Mounted on
# /dev/sda1        50G   15G   33G  31% /
# /dev/sdb1       100G   45G   50G  47% /data

# ดูขนาด folder
du -sh /var/log/           # ขนาดรวมของ /var/log
du -sh /var/log/*          # ขนาดแต่ละไฟล์/folder ข้างใน
du -sh /var/* | sort -rh | head -10   # 10 folders ใหญ่สุดใน /var

# ===== หาไฟล์ใหญ่ =====
# หาไฟล์ที่ใหญ่กว่า 100MB
find / -type f -size +100M -exec ls -lh {} \; 2>/dev/null

# หา log files ที่เก่ากว่า 30 วัน
find /var/log -name "*.log" -mtime +30

# ลบ log files เก่ากว่า 30 วัน
find /var/log -name "*.log.gz" -mtime +30 -delete

# ===== Mount Disk ใหม่ =====
# ดู disks ทั้งหมด
lsblk
# NAME    SIZE TYPE MOUNTPOINT
# sda      50G disk
# └─sda1   50G part /
# sdb     100G disk          ← disk ใหม่ ยังไม่ mount

# Format disk
sudo mkfs.ext4 /dev/sdb

# สร้าง mount point
sudo mkdir /data

# Mount
sudo mount /dev/sdb /data

# Mount ถาวร (survive reboot)
echo '/dev/sdb /data ext4 defaults 0 0' | sudo tee -a /etc/fstab
```

---

## 6. Networking บน Linux

```bash
# ===== ดูข้อมูล Network =====

# ดู IP address
ip addr show
# หรือ
hostname -I                 # แสดง IP อย่างเดียว

# ดู network interfaces
ip link show

# ดู routing table
ip route show
# default via 10.0.0.1 dev eth0  ← default gateway

# ===== ตรวจสอบ Ports =====

# ดู ports ที่เปิดอยู่
sudo ss -tlnp
# State   Recv-Q  Send-Q  Local Address:Port  Peer Address:Port  Process
# LISTEN  0       511     0.0.0.0:80           0.0.0.0:*          nginx
# LISTEN  0       128     0.0.0.0:22           0.0.0.0:*          sshd
# LISTEN  0       511     127.0.0.1:3000       0.0.0.0:*          node

# อธิบาย flags:
# -t = TCP
# -l = LISTEN only
# -n = แสดง port numbers (ไม่แปลงเป็นชื่อ)
# -p = แสดง process name

# ===== ตรวจสอบ Connectivity =====

# Ping
ping -c 4 8.8.8.8              # ส่ง 4 packets ไป Google DNS
ping -c 4 google.com           # ทดสอบ DNS ด้วย

# Traceroute (ดูเส้นทาง network)
traceroute google.com

# DNS lookup
dig google.com                  # ดู DNS record
dig google.com +short           # แสดงแค่ IP
nslookup google.com

# ทดสอบ HTTP
curl -I https://example.com     # ดูเฉพาะ headers
curl -v https://example.com     # verbose (ดูทุกอย่าง)
curl -o file.zip https://example.com/file.zip  # download

# ===== Firewall (UFW) =====
sudo ufw status                 # ดูสถานะ
sudo ufw enable                 # เปิด firewall

# อนุญาต ports
sudo ufw allow 22/tcp           # SSH
sudo ufw allow 80/tcp           # HTTP
sudo ufw allow 443/tcp          # HTTPS
sudo ufw allow from 10.0.0.0/24 to any port 5432  # PostgreSQL จาก internal network เท่านั้น

# ปิด port
sudo ufw deny 3306/tcp          # ปิด MySQL จากภายนอก

# ดูกฎทั้งหมด
sudo ufw status numbered
sudo ufw delete 3               # ลบกฎข้อที่ 3
```

---

## 7. Log Files — หาปัญหาจาก Logs

```bash
# ===== Log Files สำคัญ =====
# /var/log/syslog          ← system log หลัก (Ubuntu)
# /var/log/messages        ← system log หลัก (CentOS)
# /var/log/auth.log        ← authentication logs (login attempts)
# /var/log/nginx/access.log ← Nginx access log
# /var/log/nginx/error.log  ← Nginx error log
# /var/log/kern.log        ← kernel logs

# ===== อ่าน Logs =====

# ดู log ล่าสุด (real-time)
tail -f /var/log/syslog             # follow mode — เห็นทันที
tail -f /var/log/nginx/error.log

# ดู 50 บรรทัดล่าสุด
tail -50 /var/log/syslog

# ค้นหาใน log
grep "error" /var/log/syslog
grep -i "fail" /var/log/auth.log    # -i = case insensitive
grep -c "404" /var/log/nginx/access.log  # นับจำนวน 404

# ค้นหาหลายคำ
grep -E "error|fail|critical" /var/log/syslog

# ดู log ตามช่วงเวลา (journalctl)
sudo journalctl --since "2024-01-15 10:00" --until "2024-01-15 12:00"
sudo journalctl -u nginx --since "1 hour ago"
sudo journalctl -p err              # เฉพาะ error level

# ===== วิเคราะห์ Nginx Access Log =====

# Top 10 IPs ที่เข้ามากที่สุด
awk '{print $1}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head -10

# Top 10 URLs ที่ถูกเรียกมากที่สุด
awk '{print $7}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head -10

# นับ HTTP status codes
awk '{print $9}' /var/log/nginx/access.log | sort | uniq -c | sort -rn

# หา 500 errors
grep " 500 " /var/log/nginx/access.log | tail -20

# ===== Log Rotation =====
# ป้องกัน log files โตจนเต็ม disk
# config อยู่ที่ /etc/logrotate.d/

# ตัวอย่าง: /etc/logrotate.d/myapp
# /var/log/myapp/*.log {
#     daily              # rotate ทุกวัน
#     rotate 14          # เก็บไว้ 14 ไฟล์
#     compress           # บีบอัดไฟล์เก่า (.gz)
#     missingok          # ไม่ error ถ้าไม่มีไฟล์
#     notifempty         # ไม่ rotate ถ้าว่าง
#     create 644 deploy deploy  # สร้างไฟล์ใหม่ด้วยสิทธิ์นี้
# }
```

---

## 8. SSH ละเอียด — Remote Access

```bash
# ===== การเชื่อมต่อ =====

# เชื่อมต่อพื้นฐาน
ssh deploy@10.0.1.50

# ใช้ SSH key (ปลอดภัยกว่า password)
ssh -i ~/.ssh/mykey.pem deploy@10.0.1.50

# ===== สร้าง SSH Key =====
ssh-keygen -t ed25519 -C "deploy@mycompany.com"
# กด Enter ใช้ path default (~/.ssh/id_ed25519)
# ใส่ passphrase (แนะนำ!) หรือกด Enter ข้ามไป

# ไฟล์ที่ได้:
# ~/.ssh/id_ed25519       ← Private key (ห้ามแชร์!)
# ~/.ssh/id_ed25519.pub   ← Public key (แชร์ได้)

# Copy public key ไป server
ssh-copy-id deploy@10.0.1.50
# หรือทำ manual:
cat ~/.ssh/id_ed25519.pub | ssh deploy@10.0.1.50 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"

# ===== SSH Config File (ง่ายมาก!) =====
# สร้างไฟล์ ~/.ssh/config

# ~/.ssh/config
Host prod-web
    HostName 10.0.1.50
    User deploy
    IdentityFile ~/.ssh/prod_key
    Port 22

Host staging-web
    HostName 10.0.2.50
    User deploy
    IdentityFile ~/.ssh/staging_key

Host db-prod
    HostName 10.0.1.100
    User admin
    IdentityFile ~/.ssh/prod_key
    # เข้าผ่าน bastion/jump host
    ProxyJump prod-web

# ใช้งาน: แค่พิมพ์
ssh prod-web                    # แทน ssh -i ~/.ssh/prod_key deploy@10.0.1.50
ssh staging-web
ssh db-prod                     # เข้าผ่าน prod-web อัตโนมัติ

# ===== SSH Tunneling (Port Forwarding) =====

# Local forwarding: เข้าถึง service บน remote server ผ่าน localhost
ssh -L 5432:localhost:5432 prod-web
# ตอนนี้ localhost:5432 = prod-web:5432
# → psql -h localhost -p 5432   เข้า database บน prod-web ได้!

# ตัวอย่างจริง: เข้า database ผ่าน bastion
ssh -L 5432:db-internal:5432 bastion-server
# bastion → เชื่อมต่อ → db-internal:5432
# localhost:5432 → bastion → db-internal:5432

# ===== Security: ปิด Password Authentication =====
# แก้ไฟล์: /etc/ssh/sshd_config

# PasswordAuthentication no        ← ปิด password (ใช้ key อย่างเดียว)
# PermitRootLogin no               ← ห้าม login เป็น root
# PubkeyAuthentication yes         ← เปิด key authentication

# Restart SSH หลังแก้ config
sudo systemctl restart sshd
```

---

## 9. Cron Jobs — ตั้งเวลาทำงานอัตโนมัติ

```bash
# Cron = scheduler ที่รัน script ตามเวลาที่กำหนด

# แก้ไข crontab ของ user ปัจจุบัน
crontab -e

# ดู crontab ปัจจุบัน
crontab -l

# รูปแบบ:
# ┌───────── minute (0-59)
# │ ┌─────── hour (0-23)
# │ │ ┌───── day of month (1-31)
# │ │ │ ┌─── month (1-12)
# │ │ │ │ ┌─ day of week (0-7, 0 and 7 = Sunday)
# │ │ │ │ │
# * * * * *  command

# ตัวอย่าง:
# ทุกนาที
* * * * * /opt/scripts/health-check.sh

# ทุก 5 นาที
*/5 * * * * /opt/scripts/monitor.sh

# ทุกชั่วโมง ตรง (เช่น 1:00, 2:00, 3:00)
0 * * * * /opt/scripts/cleanup-temp.sh

# ทุกวัน ตี 2
0 2 * * * /opt/scripts/backup-db.sh

# ทุกวันจันทร์ 9:00
0 9 * * 1 /opt/scripts/weekly-report.sh

# ทุกวันที่ 1 ของเดือน ตอน 0:00
0 0 1 * * /opt/scripts/monthly-cleanup.sh

# สิ่งสำคัญ:
# - ใช้ absolute path เสมอ (ไม่ใช่ relative path)
# - redirect output เพื่อดู log:
0 2 * * * /opt/scripts/backup.sh >> /var/log/backup.log 2>&1
# - 2>&1 = redirect stderr ไป stdout (เก็บทั้ง output และ error)
```

---

## 10. Process Management ละเอียด

```bash
# ===== ดู Processes =====

# ps — snapshot ของ processes
ps aux                          # ดูทั้งหมด
ps aux | grep nginx             # หาเฉพาะ nginx
ps aux --sort=-%mem | head -10  # Top 10 ใช้ RAM มากสุด
ps aux --sort=-%cpu | head -10  # Top 10 ใช้ CPU มากสุด

# อธิบาย columns:
# USER  PID  %CPU %MEM  VSZ   RSS  TTY  STAT  START  TIME  COMMAND
# root  123  0.5  2.1   1234  5678 ?    Ss    Jan15  0:10  nginx: master

# top / htop — real-time monitoring
top                             # built-in
htop                            # สวยกว่า (ต้อง install: apt install htop)

# ===== จัดการ Processes =====

# Kill process
kill 12345                      # ส่ง SIGTERM (graceful shutdown)
kill -9 12345                   # ส่ง SIGKILL (force kill — ใช้เมื่อ SIGTERM ไม่ได้ผล)
kill -15 12345                  # เหมือน kill ปกติ (SIGTERM)

# Kill by name
pkill nginx                     # kill ทุก process ชื่อ nginx
killall node                    # kill ทุก process ชื่อ node

# ===== Background Processes =====

# รันใน background
./long-task.sh &                # & = รันใน background
nohup ./long-task.sh &          # nohup = ไม่หยุดเมื่อปิด terminal

# ดู background jobs
jobs

# เอา background job มา foreground
fg %1

# ===== Resource Usage =====

# Memory
free -h
#               total        used        free      shared  buff/cache   available
# Mem:          7.8Gi       3.2Gi       1.1Gi       256Mi       3.4Gi       4.0Gi
# Swap:         2.0Gi       0.0Ki       2.0Gi

# ดูว่า process ไหนใช้ memory เยอะ
ps aux --sort=-%mem | head -5

# CPU info
nproc                           # จำนวน CPU cores
lscpu                           # ข้อมูล CPU ละเอียด

# Uptime + Load Average
uptime
# 10:30:00 up 45 days, 3:15, 2 users, load average: 0.52, 0.48, 0.45
# load average: 1min, 5min, 15min
# ค่าควรน้อยกว่าจำนวน CPU cores (เช่น 4 cores → load < 4.0)
```
