# Configuration Management & Infrastructure as Code (IaC)

---

## 1. Configuration Management

### คืออะไร

จัดการ configuration ของ servers หลายเครื่อง **อัตโนมัติ ไม่ต้อง SSH ไปตั้งทีละเครื่อง**

```
ไม่มี Config Management:
SSH server 1 → apt install nginx → แก้ config
SSH server 2 → apt install nginx → แก้ config
SSH server 3 → apt install nginx → แก้ config  ← ซ้ำซาก, ผิดพลาดง่าย

มี Config Management:
เขียน playbook 1 ครั้ง → รันกับ 100 servers ได้ทันที ✅
```

### Ansible ⭐ (แนะนำ — ง่ายที่สุด)

```yaml
# inventory.yml — รายชื่อ servers
all:
  hosts:
    web1:
      ansible_host: 10.0.1.10
    web2:
      ansible_host: 10.0.1.11
    db1:
      ansible_host: 10.0.2.10
  children:
    webservers:
      hosts:
        web1:
        web2:
    databases:
      hosts:
        db1:

# playbook.yml — สิ่งที่ต้องทำ
---
- name: Setup web servers
  hosts: webservers
  become: yes               # run as root

  tasks:
    - name: Update packages
      apt:
        update_cache: yes
        upgrade: dist

    - name: Install Nginx
      apt:
        name: nginx
        state: present

    - name: Copy Nginx config
      template:
        src: templates/nginx.conf.j2
        dest: /etc/nginx/sites-available/default
      notify: Restart Nginx

    - name: Ensure Nginx is running
      service:
        name: nginx
        state: started
        enabled: yes

  handlers:
    - name: Restart Nginx
      service:
        name: nginx
        state: restarted

# รัน: ansible-playbook -i inventory.yml playbook.yml
```

### เปรียบเทียบ CM Tools

```
| เครื่องมือ  | ภาษา    | Agent? | Learning Curve | ความนิยม  |
|------------|---------|--------|---------------|-----------|
| Ansible ⭐ | YAML    | ❌ ไม่ต้อง | ง่าย         | สูงมาก    |
| Puppet     | Ruby DSL| ✅ ต้อง  | ยาก           | ลดลง      |
| Chef       | Ruby    | ✅ ต้อง  | ยาก           | ลดลง      |
| Salt       | YAML    | ✅/❌   | ปานกลาง       | ปานกลาง   |

แนะนำ: Ansible (agentless, YAML, เรียนง่าย, ใช้มากที่สุด)
```

---

## 2. Infrastructure as Code (IaC) — Provisioning

### คืออะไร

**สร้างและจัดการ infrastructure (servers, networks, databases) ด้วยโค้ด** แทนการ click ใน web console

```
ไม่มี IaC:
เข้า AWS Console → คลิกสร้าง EC2 → ตั้งค่า security group → คลิกๆๆ
❌ ลืมว่าตั้งค่าอะไร, ทำซ้ำยาก, ไม่มีประวัติ

มี IaC:
เขียน code → commit Git → รัน → infrastructure สร้างอัตโนมัติ
✅ ทำซ้ำได้ 100%, มีประวัติ, review ได้, ทดสอบได้
```

### Terraform ⭐ (แนะนำ — cloud-agnostic)

```hcl
# main.tf — สร้าง AWS EC2 + Security Group

# Provider
provider "aws" {
  region = "ap-southeast-1"   # Singapore
}

# Security Group
resource "aws_security_group" "web" {
  name        = "web-sg"
  description = "Allow HTTP and SSH"

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["YOUR_IP/32"]    # เฉพาะ IP คุณ
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# EC2 Instance
resource "aws_instance" "web" {
  ami           = "ami-0c55b159cbfafe1f0"
  instance_type = "t3.micro"
  key_name      = "my-key"

  vpc_security_group_ids = [aws_security_group.web.id]

  tags = {
    Name        = "web-server"
    Environment = "production"
  }
}

# Output
output "public_ip" {
  value = aws_instance.web.public_ip
}
```

```bash
# Terraform Commands
terraform init        # ดาวน์โหลด provider plugins
terraform plan        # ดูว่าจะสร้าง/แก้อะไร (preview)
terraform apply       # สร้าง infrastructure จริง
terraform destroy     # ลบทั้งหมด
terraform state list  # ดู resources ที่มี
```

### IaC Tools เปรียบเทียบ

```
| เครื่องมือ        | Cloud           | ภาษา     | ข้อดี                    |
|------------------|-----------------|----------|--------------------------|
| Terraform ⭐     | Multi-cloud     | HCL      | Cloud-agnostic, community|
| Pulumi           | Multi-cloud     | TS/Py/Go | ใช้ภาษาจริง, type-safe   |
| CloudFormation   | AWS only        | YAML/JSON| Native AWS, ฟรี          |
| AWS CDK          | AWS only        | TS/Py    | ใช้ภาษาจริง + CloudFormation|
| Bicep            | Azure only      | Bicep    | Native Azure             |

แนะนำ:
├── Multi-cloud หรือเริ่มต้น? → Terraform
├── AWS เท่านั้น? → AWS CDK หรือ CloudFormation
├── Azure เท่านั้น? → Bicep
└── ชอบเขียน TypeScript? → Pulumi หรือ AWS CDK
```
