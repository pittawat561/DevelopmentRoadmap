# Ansible Deep Dive — Configuration Management ฉบับเต็ม

> อ่านจบแล้วเขียน Ansible Playbook จัดการ servers ได้จริง

---

## 1. Ansible คืออะไร ทำไมต้องใช้

```
ปัญหา: มี 50 servers ต้องทำสิ่งเหล่านี้:
- ติดตั้ง Nginx
- Copy config file
- สร้าง user "deploy"
- ตั้ง firewall rules
- ติดตั้ง monitoring agent

วิธีเก่า: SSH ไปทีละเครื่อง × 50 = ตาย 💀

Ansible: เขียน playbook 1 ครั้ง → รันกับ 50 servers พร้อมกัน ✅

Ansible ดียังไง:
✅ Agentless — ไม่ต้องติดตั้งอะไรบน server (ใช้ SSH!)
✅ YAML — อ่านง่าย เรียนง่าย
✅ Idempotent — รันกี่ครั้งก็ได้ ผลลัพธ์เหมือนกัน
✅ Push-based — สั่งจากเครื่องเรา ไม่ต้องรอ agent pull
```

### ติดตั้ง

```bash
# macOS
brew install ansible

# Ubuntu/Debian
sudo apt update
sudo apt install ansible

# pip (ทุก OS)
pip install ansible

# ตรวจสอบ
ansible --version
```

---

## 2. Inventory — รายชื่อ Servers

```yaml
# inventory.yml — บอก Ansible ว่ามี servers อะไรบ้าง

all:
  children:
    # กลุ่ม web servers
    webservers:
      hosts:
        web1:
          ansible_host: 10.0.1.10
        web2:
          ansible_host: 10.0.1.11
        web3:
          ansible_host: 10.0.1.12
      vars:                           # ตัวแปรสำหรับทั้งกลุ่ม
        nginx_port: 80

    # กลุ่ม database servers
    databases:
      hosts:
        db-primary:
          ansible_host: 10.0.2.10
          db_role: primary
        db-replica:
          ansible_host: 10.0.2.11
          db_role: replica

    # กลุ่ม monitoring
    monitoring:
      hosts:
        prometheus:
          ansible_host: 10.0.3.10

  vars:                               # ตัวแปรสำหรับทุก hosts
    ansible_user: deploy
    ansible_ssh_private_key_file: ~/.ssh/prod_key
    ansible_python_interpreter: /usr/bin/python3
```

```bash
# ทดสอบว่า Ansible เข้าถึง servers ได้
ansible all -i inventory.yml -m ping
# web1 | SUCCESS => { "ping": "pong" }
# web2 | SUCCESS => { "ping": "pong" }
# db-primary | SUCCESS => { "ping": "pong" }

# รันคำสั่งบนทุก servers
ansible all -i inventory.yml -m shell -a "uptime"

# รันเฉพาะกลุ่ม webservers
ansible webservers -i inventory.yml -m shell -a "nginx -v"
```

---

## 3. Playbook — เขียน Automation

### Playbook แรก — Setup Web Server

```yaml
# setup-webserver.yml
---
- name: Setup Web Servers          # ชื่อ play
  hosts: webservers                 # รันกับกลุ่มไหน
  become: yes                       # ใช้ sudo

  vars:
    app_name: myapp
    app_port: 3000
    node_version: "20"

  tasks:
    # ===== 1. อัปเดต system =====
    - name: Update apt cache
      apt:
        update_cache: yes
        cache_valid_time: 3600       # cache 1 ชม. ไม่ต้อง update ซ้ำ

    - name: Upgrade all packages
      apt:
        upgrade: dist

    # ===== 2. ติดตั้ง packages =====
    - name: Install required packages
      apt:
        name:
          - nginx
          - curl
          - wget
          - git
          - ufw
          - htop
        state: present               # ถ้ามีแล้วก็ข้าม (idempotent)

    # ===== 3. สร้าง deploy user =====
    - name: Create deploy user
      user:
        name: deploy
        shell: /bin/bash
        groups: sudo
        append: yes                  # เพิ่มเข้ากลุ่ม ไม่ลบกลุ่มเก่า
        create_home: yes

    - name: Add SSH key for deploy user
      authorized_key:
        user: deploy
        key: "{{ lookup('file', '~/.ssh/deploy_key.pub') }}"

    # ===== 4. ติดตั้ง Node.js =====
    - name: Install Node.js repository
      shell: curl -fsSL https://deb.nodesource.com/setup_{{ node_version }}.x | bash -
      args:
        creates: /etc/apt/sources.list.d/nodesource.list   # ถ้ามีแล้วข้าม

    - name: Install Node.js
      apt:
        name: nodejs
        state: present

    # ===== 5. Setup Nginx =====
    - name: Copy Nginx config
      template:
        src: templates/nginx.conf.j2    # ใช้ Jinja2 template
        dest: /etc/nginx/sites-available/{{ app_name }}
        owner: root
        group: root
        mode: '0644'
      notify: Reload Nginx              # trigger handler เมื่อ config เปลี่ยน

    - name: Enable site
      file:
        src: /etc/nginx/sites-available/{{ app_name }}
        dest: /etc/nginx/sites-enabled/{{ app_name }}
        state: link
      notify: Reload Nginx

    - name: Remove default site
      file:
        path: /etc/nginx/sites-enabled/default
        state: absent
      notify: Reload Nginx

    # ===== 6. Firewall =====
    - name: Allow SSH
      ufw:
        rule: allow
        port: '22'
        proto: tcp

    - name: Allow HTTP
      ufw:
        rule: allow
        port: '80'
        proto: tcp

    - name: Allow HTTPS
      ufw:
        rule: allow
        port: '443'
        proto: tcp

    - name: Enable UFW
      ufw:
        state: enabled
        policy: deny                  # default deny

    # ===== 7. Ensure services are running =====
    - name: Ensure Nginx is running
      service:
        name: nginx
        state: started
        enabled: yes

  # ===== Handlers (ทำเมื่อถูก notify) =====
  handlers:
    - name: Reload Nginx
      service:
        name: nginx
        state: reloaded

    - name: Restart Nginx
      service:
        name: nginx
        state: restarted
```

### Jinja2 Template

```nginx
# templates/nginx.conf.j2

server {
    listen {{ nginx_port | default(80) }};
    server_name {{ ansible_hostname }};

    location / {
        proxy_pass http://localhost:{{ app_port }};
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    # Health check endpoint
    location /health {
        access_log off;
        return 200 "OK";
        add_header Content-Type text/plain;
    }
}
```

### รัน Playbook

```bash
# รัน playbook
ansible-playbook -i inventory.yml setup-webserver.yml

# Dry run (ดูว่าจะทำอะไร ไม่ทำจริง)
ansible-playbook -i inventory.yml setup-webserver.yml --check

# รันเฉพาะบาง tags
ansible-playbook -i inventory.yml setup-webserver.yml --tags "nginx,firewall"

# รันเฉพาะบาง hosts
ansible-playbook -i inventory.yml setup-webserver.yml --limit web1

# Verbose mode (debug)
ansible-playbook -i inventory.yml setup-webserver.yml -vvv
```

---

## 4. Roles — จัดโครงสร้างให้เป็นระเบียบ

```
Role = แยก playbook เป็นส่วนๆ ที่ reuse ได้

โครงสร้าง Role:
roles/
├── nginx/
│   ├── tasks/
│   │   └── main.yml          ← tasks
│   ├── handlers/
│   │   └── main.yml          ← handlers
│   ├── templates/
│   │   └── nginx.conf.j2     ← templates
│   ├── files/
│   │   └── ssl-cert.pem      ← static files
│   ├── vars/
│   │   └── main.yml          ← variables
│   └── defaults/
│       └── main.yml          ← default values
├── nodejs/
│   ├── tasks/main.yml
│   └── defaults/main.yml
└── firewall/
    ├── tasks/main.yml
    └── defaults/main.yml
```

```yaml
# roles/nginx/tasks/main.yml
---
- name: Install Nginx
  apt:
    name: nginx
    state: present

- name: Copy config
  template:
    src: nginx.conf.j2
    dest: /etc/nginx/sites-available/default
  notify: Reload Nginx

- name: Ensure Nginx is running
  service:
    name: nginx
    state: started
    enabled: yes
```

```yaml
# roles/nginx/defaults/main.yml
---
nginx_port: 80
nginx_worker_processes: auto
```

```yaml
# site.yml — ใช้ roles
---
- name: Setup Web Servers
  hosts: webservers
  become: yes
  roles:
    - nginx
    - nodejs
    - firewall

- name: Setup Database Servers
  hosts: databases
  become: yes
  roles:
    - postgresql
    - firewall
```

---

## 5. Ansible Vault — จัดการ Secrets

```bash
# สร้างไฟล์ encrypted
ansible-vault create secrets.yml
# → ใส่ password → เปิด editor → เขียน secrets

# เนื้อหา secrets.yml:
# db_password: "super_secret_123"
# api_key: "sk-abc123..."

# แก้ไข
ansible-vault edit secrets.yml

# ดู (decrypt แสดง)
ansible-vault view secrets.yml

# รัน playbook ที่ใช้ vault
ansible-playbook -i inventory.yml site.yml --ask-vault-pass
# หรือ
ansible-playbook -i inventory.yml site.yml --vault-password-file ~/.vault_pass
```

---

## 6. ตัวอย่างจริง: Deploy Application

```yaml
# deploy-app.yml — Deploy app ไป servers
---
- name: Deploy Application
  hosts: webservers
  become: yes
  vars:
    app_name: myapp
    app_dir: /opt/{{ app_name }}
    app_repo: https://github.com/myorg/myapp.git
    app_branch: main

  tasks:
    - name: Clone/Update repository
      git:
        repo: "{{ app_repo }}"
        dest: "{{ app_dir }}"
        version: "{{ app_branch }}"
        force: yes
      register: git_result            # เก็บผลลัพธ์

    - name: Install dependencies
      npm:
        path: "{{ app_dir }}"
        production: yes
      when: git_result.changed        # ทำเมื่อ code เปลี่ยนเท่านั้น

    - name: Copy environment file
      template:
        src: templates/env.j2
        dest: "{{ app_dir }}/.env"
        mode: '0600'

    - name: Restart application
      systemd:
        name: "{{ app_name }}"
        state: restarted
      when: git_result.changed

    - name: Wait for app to start
      wait_for:
        port: 3000
        delay: 3
        timeout: 30

    - name: Health check
      uri:
        url: "http://localhost:3000/health"
        status_code: 200
      register: health
      retries: 5
      delay: 5
      until: health.status == 200
```

---

## 7. Ansible vs Terraform — ใช้ร่วมกัน

```
Terraform สร้าง infrastructure:
├── VPC, Subnets
├── EC2 instances
├── RDS databases
├── Load Balancers
└── Security Groups

Ansible ตั้งค่า servers:
├── ติดตั้ง packages
├── Configure services
├── Deploy applications
├── Manage users
└── Setup monitoring

Workflow ที่ดี:
1. Terraform สร้าง servers    → terraform apply
2. Ansible ตั้งค่า servers     → ansible-playbook setup.yml
3. Ansible deploy app         → ansible-playbook deploy.yml

"Terraform provisions, Ansible configures."
```
