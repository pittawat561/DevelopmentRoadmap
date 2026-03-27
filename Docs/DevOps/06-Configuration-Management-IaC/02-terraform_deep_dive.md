# Terraform Deep Dive — Infrastructure as Code ฉบับเต็ม

> อ่านจบแล้วเขียน Terraform สร้าง infrastructure จริงได้

---

## 1. Terraform คืออะไร ทำไมต้องใช้

### ปัญหาก่อนมี Terraform

```
สมมติต้องสร้าง infrastructure สำหรับ web app:
- 2 EC2 instances (web servers)
- 1 RDS database
- 1 Load Balancer
- Security Groups
- VPC + Subnets

วิธีเก่า (คลิกใน Console):
1. เข้า AWS Console → คลิกสร้าง VPC → ตั้งค่า CIDR
2. คลิกสร้าง Subnet → เลือก AZ
3. คลิกสร้าง Security Group → เพิ่ม rules
4. คลิกสร้าง EC2 → เลือก AMI → เลือก instance type
5. ... อีก 20 ขั้นตอน

ปัญหา:
❌ ใช้เวลา 1-2 ชั่วโมงต่อ environment
❌ ลืมว่าตั้งค่าอะไรไว้
❌ ทำ staging ให้เหมือน production ยากมาก
❌ ไม่มี version control
❌ ไม่สามารถ review ก่อนสร้างได้

วิธีใหม่ (Terraform):
เขียนโค้ด → terraform plan (ดูก่อน) → terraform apply (สร้าง)
✅ ใช้เวลา 5 นาที
✅ สร้าง staging/production เหมือนกัน 100%
✅ Version control (Git)
✅ Review ได้ (Pull Request)
✅ ทำซ้ำได้ทุกครั้ง
```

### ติดตั้ง Terraform

```bash
# macOS
brew install terraform

# Ubuntu/Debian
wget -O- https://apt.releases.hashicorp.com/gpg | sudo gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/hashicorp.list
sudo apt update && sudo apt install terraform

# Windows (Chocolatey)
choco install terraform

# ตรวจสอบ
terraform version
```

---

## 2. Terraform Workflow — ขั้นตอนการทำงาน

```
Write → Plan → Apply → (แก้ไข) → Plan → Apply → ... → Destroy

┌──────────┐    ┌──────────┐    ┌──────────┐
│  Write   │ →  │  Plan    │ →  │  Apply   │
│ .tf files│    │ Preview  │    │ สร้างจริง │
│          │    │ changes  │    │          │
└──────────┘    └──────────┘    └──────────┘

terraform init    → ดาวน์โหลด providers (ครั้งแรก)
terraform plan    → ดูว่าจะสร้าง/แก้/ลบอะไร (dry run)
terraform apply   → สร้าง infrastructure จริง
terraform destroy → ลบทุกอย่างที่สร้าง
```

---

## 3. เขียน Terraform แรก — ทีละขั้นตอน

### โครงสร้างไฟล์

```
my-infrastructure/
├── main.tf              ← resource definitions หลัก
├── variables.tf         ← ประกาศ variables
├── outputs.tf           ← ค่าที่ต้องการแสดงผล
├── terraform.tfvars     ← ค่า variables จริง
├── providers.tf         ← provider configs
└── .terraform/          ← Terraform cache (อย่าใส่ Git!)
```

### Step 1: ตั้งค่า Provider

```hcl
# providers.tf — บอกว่าใช้ cloud อะไร

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"        # ใช้ version 5.x.x
    }
  }
}

provider "aws" {
  region = var.aws_region        # ใช้ variable

  # Terraform จะดึง credentials จาก:
  # 1. Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
  # 2. AWS CLI config (~/.aws/credentials)
  # 3. IAM Role (EC2 instance role)
  # ไม่ต้องใส่ key ในโค้ด!
}
```

### Step 2: ประกาศ Variables

```hcl
# variables.tf — ประกาศ variables ที่ใช้

variable "aws_region" {
  description = "AWS region to deploy to"
  type        = string
  default     = "ap-southeast-1"     # Singapore
}

variable "environment" {
  description = "Environment name (dev/staging/prod)"
  type        = string
  default     = "dev"
}

variable "instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t3.micro"
}

variable "app_port" {
  description = "Application port"
  type        = number
  default     = 3000
}

variable "allowed_cidr_blocks" {
  description = "CIDR blocks allowed to access the app"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default = {
    Project   = "my-app"
    ManagedBy = "terraform"
  }
}
```

### Step 3: สร้าง Resources

```hcl
# main.tf — สร้าง infrastructure

# ===== VPC =====
resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = merge(var.tags, {
    Name = "${var.environment}-vpc"
  })
}

# ===== Subnets =====
resource "aws_subnet" "public_a" {
  vpc_id                  = aws_vpc.main.id     # อ้างอิง VPC ด้านบน
  cidr_block              = "10.0.1.0/24"
  availability_zone       = "${var.aws_region}a"
  map_public_ip_on_launch = true

  tags = merge(var.tags, {
    Name = "${var.environment}-public-a"
  })
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.2.0/24"
  availability_zone       = "${var.aws_region}b"
  map_public_ip_on_launch = true

  tags = merge(var.tags, {
    Name = "${var.environment}-public-b"
  })
}

# ===== Internet Gateway =====
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(var.tags, {
    Name = "${var.environment}-igw"
  })
}

# ===== Route Table =====
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(var.tags, {
    Name = "${var.environment}-public-rt"
  })
}

resource "aws_route_table_association" "public_a" {
  subnet_id      = aws_subnet.public_a.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "public_b" {
  subnet_id      = aws_subnet.public_b.id
  route_table_id = aws_route_table.public.id
}

# ===== Security Group =====
resource "aws_security_group" "web" {
  name_prefix = "${var.environment}-web-"
  description = "Security group for web servers"
  vpc_id      = aws_vpc.main.id

  # HTTP
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTP from anywhere"
  }

  # HTTPS
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTPS from anywhere"
  }

  # SSH (จำกัด IP!)
  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
    description = "SSH from allowed IPs"
  }

  # App port
  ingress {
    from_port   = var.app_port
    to_port     = var.app_port
    protocol    = "tcp"
    cidr_blocks = ["10.0.0.0/16"]    # internal only
    description = "App port from VPC"
  }

  # Outbound — allow all
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(var.tags, {
    Name = "${var.environment}-web-sg"
  })

  # ป้องกัน Terraform ลบ SG ที่ใช้อยู่แล้วสร้างใหม่
  lifecycle {
    create_before_destroy = true
  }
}

# ===== EC2 Instance =====
resource "aws_instance" "web" {
  count = 2                          # สร้าง 2 instances

  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  subnet_id              = aws_subnet.public_a.id
  vpc_security_group_ids = [aws_security_group.web.id]
  key_name               = "my-keypair"

  # User data — script ที่รันตอน instance เริ่ม
  user_data = <<-EOF
    #!/bin/bash
    apt update
    apt install -y nginx
    systemctl start nginx
    echo "Hello from $(hostname)" > /var/www/html/index.html
  EOF

  tags = merge(var.tags, {
    Name = "${var.environment}-web-${count.index + 1}"
  })
}

# ===== Data Source — หา AMI ล่าสุด =====
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"]     # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }
}

# ===== Load Balancer =====
resource "aws_lb" "web" {
  name               = "${var.environment}-web-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.web.id]
  subnets            = [aws_subnet.public_a.id, aws_subnet.public_b.id]

  tags = var.tags
}

resource "aws_lb_target_group" "web" {
  name     = "${var.environment}-web-tg"
  port     = 80
  protocol = "HTTP"
  vpc_id   = aws_vpc.main.id

  health_check {
    path                = "/"
    healthy_threshold   = 2
    unhealthy_threshold = 5
    timeout             = 5
    interval            = 30
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.web.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web.arn
  }
}

# Attach instances to target group
resource "aws_lb_target_group_attachment" "web" {
  count            = 2
  target_group_arn = aws_lb_target_group.web.arn
  target_id        = aws_instance.web[count.index].id
  port             = 80
}
```

### Step 4: Outputs

```hcl
# outputs.tf — แสดงค่าที่สำคัญหลัง apply

output "vpc_id" {
  description = "VPC ID"
  value       = aws_vpc.main.id
}

output "web_instance_ips" {
  description = "Public IPs of web servers"
  value       = aws_instance.web[*].public_ip
}

output "lb_dns_name" {
  description = "Load Balancer DNS name"
  value       = aws_lb.web.dns_name
}

output "lb_url" {
  description = "Application URL"
  value       = "http://${aws_lb.web.dns_name}"
}
```

### Step 5: ตั้งค่า Variables จริง

```hcl
# terraform.tfvars — ค่าจริง (อย่าใส่ secrets!)

aws_region    = "ap-southeast-1"
environment   = "production"
instance_type = "t3.small"
app_port      = 3000

allowed_cidr_blocks = [
  "203.0.113.0/24"    # Office IP
]

tags = {
  Project     = "my-app"
  Environment = "production"
  Team        = "platform"
  ManagedBy   = "terraform"
}
```

### Step 6: รัน Terraform

```bash
# 1. Initialize (ครั้งแรก)
terraform init
# ดาวน์โหลด AWS provider
# สร้าง .terraform/ directory

# 2. Format โค้ดให้สวย
terraform fmt

# 3. Validate syntax
terraform validate

# 4. Plan — ดูว่าจะทำอะไร
terraform plan
# Plan: 12 to add, 0 to change, 0 to destroy.
# + aws_vpc.main
# + aws_subnet.public_a
# + aws_subnet.public_b
# + aws_internet_gateway.main
# + aws_security_group.web
# + aws_instance.web[0]
# + aws_instance.web[1]
# + aws_lb.web
# ...

# 5. Apply — สร้างจริง
terraform apply
# ดูอีกครั้ง → พิมพ์ "yes" → สร้าง!

# 6. ดู output
terraform output
# lb_url = "http://production-web-alb-123456789.ap-southeast-1.elb.amazonaws.com"

# 7. ดู state ปัจจุบัน
terraform state list         # ดู resources ทั้งหมด
terraform state show aws_instance.web[0]  # ดูรายละเอียด

# 8. ลบทุกอย่าง
terraform destroy
```

---

## 4. Terraform State — หัวใจสำคัญ

```
State = ไฟล์ที่บันทึกว่า Terraform สร้างอะไรไปแล้วบ้าง

ไฟล์: terraform.tfstate (JSON)

ทำไมสำคัญ:
- Terraform เทียบ state กับ code → รู้ว่าต้องสร้าง/แก้/ลบอะไร
- ถ้า state หาย → Terraform ไม่รู้ว่ามี resources อยู่แล้ว → สร้างซ้ำ!

⚠️ ห้ามเก็บ state บนเครื่อง local ใน production!
ใช้ Remote Backend แทน:
```

```hcl
# backend.tf — เก็บ state ใน S3
terraform {
  backend "s3" {
    bucket         = "my-terraform-state"
    key            = "production/terraform.tfstate"
    region         = "ap-southeast-1"
    dynamodb_table = "terraform-locks"    # ป้องกัน 2 คนรันพร้อมกัน
    encrypt        = true
  }
}
```

---

## 5. Modules — โค้ดที่ reuse ได้

```hcl
# modules/vpc/main.tf — สร้าง module สำหรับ VPC
variable "environment" { type = string }
variable "vpc_cidr" { type = string }

resource "aws_vpc" "this" {
  cidr_block = var.vpc_cidr
  tags = { Name = "${var.environment}-vpc" }
}

output "vpc_id" { value = aws_vpc.this.id }

# ใช้ module:
module "vpc_production" {
  source      = "./modules/vpc"
  environment = "production"
  vpc_cidr    = "10.0.0.0/16"
}

module "vpc_staging" {
  source      = "./modules/vpc"
  environment = "staging"
  vpc_cidr    = "10.1.0.0/16"
}

# → สร้าง 2 VPCs จาก code เดียวกัน!
```

---

## 6. Best Practices

```
✅ ควรทำ:
- ใช้ Remote State (S3 + DynamoDB)
- ใช้ Variables (ไม่ hardcode ค่า)
- ใช้ Modules (reuse code)
- เก็บ .tf files ใน Git
- รัน terraform plan ก่อน apply เสมอ
- ใช้ terraform fmt จัด format
- Tag ทุก resource
- Lock provider versions
- แยก state per environment (dev/staging/prod)

❌ ไม่ควร:
- เก็บ secrets ในโค้ด (ใช้ Vault หรือ env vars)
- แก้ infrastructure manual บน Console (drift!)
- เก็บ .terraform/ ใน Git
- เก็บ terraform.tfstate ใน Git
- รัน apply โดยไม่ดู plan
```
