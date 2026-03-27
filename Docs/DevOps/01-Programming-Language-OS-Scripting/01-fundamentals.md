# DevOps Fundamentals — Language, OS, Scripting, Terminal

---

## 1. Learn a Programming Language

### ภาษาที่แนะนำสำหรับ DevOps

```
| ภาษา              | เหมาะกับ                          | ความนิยม     |
|-------------------|----------------------------------|-------------|
| Python ⭐         | Automation, scripting, tools      | สูงมาก      |
| Go                | Cloud tools (Docker, K8s เขียนด้วย Go) | สูง    |
| JavaScript/Node.js| Web tools, serverless             | สูง         |
| Rust              | Performance-critical tools        | กำลังมา     |
| Ruby              | Chef, Vagrant, DevOps tools เก่า   | ลดลง       |

แนะนำ: เริ่มจาก Python → เพิ่ม Go เมื่อพร้อม
```

### Python สำหรับ DevOps

```python
# ทำไม Python ดีสำหรับ DevOps:
# - อ่านง่าย เขียนเร็ว
# - Library เยอะ (boto3, paramiko, requests, fabric)
# - ใช้ใน Ansible, SaltStack
# - ใช้เขียน automation scripts

# ตัวอย่าง: Automation script
import subprocess
import requests
import json

# 1. รัน system command
result = subprocess.run(['df', '-h'], capture_output=True, text=True)
print(result.stdout)

# 2. Health check API
def health_check(urls):
    for url in urls:
        try:
            r = requests.get(url, timeout=5)
            status = "✅ UP" if r.status_code == 200 else f"❌ {r.status_code}"
        except:
            status = "❌ DOWN"
        print(f"{url}: {status}")

health_check([
    'https://api.example.com/health',
    'https://web.example.com',
])

# 3. อ่าน/เขียน JSON config
with open('config.json') as f:
    config = json.load(f)
config['version'] = '2.0'
with open('config.json', 'w') as f:
    json.dump(config, f, indent=2)
```

---

## 2. Operating System

### Linux — OS หลักของ DevOps

```
ทำไมต้อง Linux:
- Server 90%+ ในโลกรัน Linux
- Docker, Kubernetes ออกแบบมาสำหรับ Linux
- Cloud instances ส่วนใหญ่เป็น Linux
- ฟรี, open source, มีชุมชนใหญ่

Distributions ที่สำคัญ:
├── Ubuntu / Debian    ← แนะนำเริ่มต้น, ใช้มากที่สุดบน cloud
├── RHEL / CentOS      ← Enterprise, production
├── Amazon Linux       ← สำหรับ AWS
├── Alpine Linux       ← เล็กมาก, ใช้ใน Docker images
└── SUSE Linux         ← Enterprise

สิ่งที่ต้องรู้:
├── File system structure (/etc, /var, /home, /usr)
├── User & permissions (chmod, chown, groups)
├── Package management (apt, yum, dnf)
├── Service management (systemctl)
├── Process management (ps, top, kill)
├── Disk management (df, du, mount)
├── Network (ip, ss, netstat, iptables)
└── SSH (remote access)
```

### คำสั่ง Linux พื้นฐาน

```bash
# ===== File & Directory =====
ls -la                    # list files (รวม hidden)
cd /var/log               # เปลี่ยน directory
mkdir -p /app/config      # สร้าง directory (รวม parent)
cp -r src/ dest/          # copy directory
mv old.txt new.txt        # move/rename
rm -rf /tmp/cache         # ลบ (ระวัง!)
find / -name "*.log" -mtime +7   # หาไฟล์เก่ากว่า 7 วัน

# ===== User & Permissions =====
chmod 755 script.sh       # rwxr-xr-x
chmod 600 .env            # rw------- (owner only)
chown www-data:www-data /var/www  # เปลี่ยนเจ้าของ
useradd -m -s /bin/bash deploy    # สร้าง user

# ===== Package Management (Ubuntu/Debian) =====
sudo apt update           # อัปเดตรายการ packages
sudo apt install nginx    # ติดตั้ง
sudo apt upgrade           # อัปเดต packages ทั้งหมด
sudo apt remove nginx     # ลบ

# ===== Service Management (systemd) =====
sudo systemctl start nginx     # เริ่ม service
sudo systemctl stop nginx      # หยุด
sudo systemctl restart nginx   # restart
sudo systemctl status nginx    # ดูสถานะ
sudo systemctl enable nginx    # เปิดตอน boot
journalctl -u nginx -f         # ดู logs

# ===== Process Management =====
ps aux                    # ดู processes ทั้งหมด
ps aux | grep nginx       # หา process
top                       # real-time process monitor
htop                      # top แบบสวยกว่า
kill -9 12345             # kill process by PID
kill -15 12345            # graceful shutdown

# ===== Disk & Memory =====
df -h                     # disk usage
du -sh /var/log           # folder size
free -h                   # memory usage

# ===== Network =====
ip addr show              # ดู IP
ss -tlnp                  # ดู ports ที่เปิด
curl -v https://example.com  # test HTTP
ping 8.8.8.8              # test connectivity
dig example.com           # DNS lookup
traceroute example.com    # trace network path
```

---

## 3. Scripting — Bash & PowerShell

### Bash Scripting

```bash
#!/bin/bash
# deploy.sh — Deployment script

set -euo pipefail    # หยุดทันทีถ้ามี error

# Variables
APP_NAME="myapp"
DEPLOY_DIR="/opt/$APP_NAME"
BACKUP_DIR="/opt/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

log() { echo -e "${GREEN}[$(date '+%H:%M:%S')]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1" >&2; exit 1; }

# Functions
backup() {
    log "Creating backup..."
    mkdir -p "$BACKUP_DIR"
    tar -czf "$BACKUP_DIR/${APP_NAME}_${TIMESTAMP}.tar.gz" "$DEPLOY_DIR" \
        || error "Backup failed"
    log "Backup created: ${APP_NAME}_${TIMESTAMP}.tar.gz"
}

deploy() {
    log "Deploying $APP_NAME..."
    cd "$DEPLOY_DIR"
    git pull origin main || error "Git pull failed"
    npm ci --production || error "npm install failed"
    sudo systemctl restart "$APP_NAME" || error "Restart failed"
    log "Deploy completed!"
}

health_check() {
    log "Running health check..."
    sleep 3
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/health)
    if [ "$HTTP_CODE" -eq 200 ]; then
        log "Health check passed! ✅"
    else
        error "Health check failed! HTTP $HTTP_CODE"
    fi
}

cleanup_old_backups() {
    log "Cleaning up backups older than 7 days..."
    find "$BACKUP_DIR" -name "*.tar.gz" -mtime +7 -delete
}

# Main
log "Starting deployment for $APP_NAME"
backup
deploy
health_check
cleanup_old_backups
log "All done! 🎉"
```

### PowerShell

```powershell
# deploy.ps1 — Windows deployment script

param(
    [string]$Environment = "staging",
    [switch]$SkipTests
)

function Write-Log($Message) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Green
}

# Health Check
function Test-ServiceHealth($Url) {
    try {
        $response = Invoke-WebRequest -Uri $Url -TimeoutSec 10
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

# Deploy
Write-Log "Deploying to $Environment..."

if (-not $SkipTests) {
    Write-Log "Running tests..."
    dotnet test --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed!" }
}

dotnet publish -c Release -o "./publish"

# Check health
if (Test-ServiceHealth "http://localhost:5000/health") {
    Write-Log "Deployment successful! ✅"
} else {
    Write-Log "Health check failed! ❌" -ForegroundColor Red
}
```

---

## 4. Terminal Knowledge

```
สิ่งที่ DevOps ต้องรู้:

Process Monitoring:
├── top / htop           — real-time processes
├── ps aux               — snapshot processes
├── lsof                 — open files/ports
└── strace               — trace system calls

Performance Monitoring:
├── vmstat               — virtual memory stats
├── iostat               — disk I/O stats
├── sar                  — system activity report
├── dstat                — versatile resource stats
└── nmon                 — comprehensive monitor

Networking Tools:
├── ping                 — test connectivity
├── traceroute / mtr     — trace network path
├── dig / nslookup       — DNS lookup
├── curl / wget          — HTTP requests
├── ss / netstat         — network connections
├── tcpdump              — packet capture
├── nmap                 — port scanning
└── iptables / nftables  — firewall

Text Manipulation:
├── grep / egrep         — search text patterns
├── sed                  — stream editor
├── awk                  — text processing
├── sort / uniq          — sort & deduplicate
├── cut / tr             — extract/translate chars
├── jq                   — JSON processing
└── xargs                — build commands from input

# ตัวอย่าง: หา top 10 IPs ที่เข้ามากที่สุดจาก access log
cat /var/log/nginx/access.log \
  | awk '{print $1}' \
  | sort \
  | uniq -c \
  | sort -rn \
  | head -10

# ตัวอย่าง: ดู error ใน log ล่าสุด 100 บรรทัด
tail -100 /var/log/app/error.log | grep -i "error\|fatal\|exception"

# ตัวอย่าง: parse JSON จาก API
curl -s https://api.example.com/status | jq '.services[] | select(.status != "healthy")'
```

### Editors

```
Vim (ต้องรู้พื้นฐาน — เจอบน server ทุกเครื่อง):
├── i         — เข้า insert mode
├── Esc       — กลับ normal mode
├── :w        — save
├── :q        — quit
├── :wq       — save & quit
├── :q!       — quit ไม่ save
├── dd        — ลบบรรทัด
├── /keyword  — ค้นหา
└── :%s/old/new/g — find & replace ทั้งไฟล์

Nano (ง่ายกว่า Vim):
├── Ctrl+O    — save
├── Ctrl+X    — exit
├── Ctrl+W    — search
└── Ctrl+K    — cut line
```
