# Containers for DevOps — Docker & LXC

> เนื้อหา Docker/K8s พื้นฐานอยู่ใน 12-Containerization แล้ว ส่วนนี้เสริมมุม DevOps

---

## 1. Docker สำหรับ DevOps

### Multi-stage Build (Production-ready)

```dockerfile
# Stage 1: Build
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2: Production (เฉพาะ output)
FROM node:20-alpine AS production
WORKDIR /app
COPY --from=builder /app/dist ./dist
COPY --from=builder /app/node_modules ./node_modules
COPY package*.json ./

# Non-root user (security!)
RUN addgroup -g 1001 appgroup && \
    adduser -u 1001 -G appgroup -s /bin/sh -D appuser
USER appuser

EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s \
  CMD wget --quiet --tries=1 --spider http://localhost:3000/health || exit 1

CMD ["node", "dist/server.js"]
```

### Docker Best Practices

```
✅ ควรทำ:
- ใช้ specific version tags (node:20-alpine ไม่ใช่ node:latest)
- Multi-stage builds (image เล็กลง)
- .dockerignore (ไม่ copy node_modules, .git)
- Non-root user
- HEALTHCHECK
- 1 process per container
- ใช้ Alpine images (เล็ก, ปลอดภัย)

❌ ไม่ควร:
- ใช้ :latest tag ใน production
- รัน root ใน container
- เก็บ secrets ใน image
- ไม่มี .dockerignore
```

### Docker Networking & Volumes

```bash
# Networks
docker network create app-network
docker run --network app-network --name db postgres:16
docker run --network app-network --name app myapp
# → app สามารถเข้า db ด้วยชื่อ "db"

# Volumes (persistent data)
docker volume create pgdata
docker run -v pgdata:/var/lib/postgresql/data postgres:16

# Bind mount (development)
docker run -v $(pwd):/app -w /app node:20 npm run dev
```

## 2. LXC/LXD (Linux Containers)

```
LXC = lightweight OS-level virtualization

Docker vs LXC:
| หัวข้อ       | Docker              | LXC                  |
|-------------|---------------------|----------------------|
| จุดประสงค์   | Application container| System container     |
| เปรียบเหมือน| 1 app ใน 1 กล่อง    | 1 OS ใน 1 กล่อง (เบาๆ)|
| Init system | ไม่มี               | มี (systemd)         |
| Use case    | Microservices, CI/CD | VM replacement       |

# LXD commands
lxc launch ubuntu:22.04 my-container
lxc list
lxc exec my-container -- bash
lxc stop my-container
lxc delete my-container
```
