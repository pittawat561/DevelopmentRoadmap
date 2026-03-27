# Cloud Providers & Serverless

---

## 1. Cloud Providers หลัก

```
| Provider        | ส่วนแบ่งตลาด | เด่นเรื่อง                     |
|----------------|-------------|-------------------------------|
| AWS ⭐          | ~31%        | บริการครบที่สุด, ใหญ่ที่สุด      |
| Azure           | ~25%        | ดีกับ .NET / Microsoft ecosystem|
| Google Cloud    | ~11%        | ดีกับ AI/ML, Kubernetes (GKE)  |
| Digital Ocean   | เล็ก         | ง่าย, ราคาถูก, developer-friendly|
| Hetzner/Contabo | เล็ก         | ราคาถูกมาก, bare metal          |

เลือกอะไร:
├── ต้องการบริการครบ + หางานง่าย?     → AWS
├── ใช้ .NET / Microsoft stack?        → Azure
├── เน้น AI/ML หรือ Kubernetes?        → GCP
├── โปรเจกต์เล็ก, งบน้อย?             → Digital Ocean / Hetzner
└── ไม่แน่ใจ?                          → AWS (ตลาดงานใหญ่สุด)
```

### บริการ Cloud ที่ DevOps ต้องรู้

```
| หมวด          | AWS              | Azure                | GCP                |
|--------------|------------------|----------------------|--------------------|
| Compute      | EC2              | Virtual Machines     | Compute Engine     |
| Containers   | ECS, EKS         | AKS                  | GKE                |
| Serverless   | Lambda           | Functions            | Cloud Functions    |
| Storage      | S3               | Blob Storage         | Cloud Storage      |
| Database     | RDS, DynamoDB    | SQL Database, CosmosDB| Cloud SQL, Firestore|
| Networking   | VPC, ALB         | VNet, App Gateway    | VPC, Cloud LB      |
| CDN          | CloudFront       | Azure CDN            | Cloud CDN          |
| DNS          | Route 53         | Azure DNS            | Cloud DNS          |
| Monitoring   | CloudWatch       | Monitor              | Cloud Monitoring   |
| IAM          | IAM              | Entra ID (AAD)       | IAM                |
| IaC          | CloudFormation   | ARM/Bicep            | Deployment Manager |
```

---

## 2. Serverless

```
ไม่ต้องจัดการ server เลย — cloud รัน code ให้ จ่ายเฉพาะตอนใช้

AWS Lambda:
├── รันได้นานสุด 15 นาที
├── Memory: 128 MB - 10 GB
├── Trigger: API Gateway, S3, SQS, Schedule, etc.
├── ภาษา: Node.js, Python, Java, Go, .NET, Ruby
└── จ่ายต่อ: จำนวน requests + เวลาที่ใช้

Azure Functions:
├── คล้าย Lambda แต่สำหรับ Azure
├── Durable Functions (stateful workflows)
└── ดีกับ .NET

Serverless Framework:
├── Vercel    — frontend + API routes
├── Netlify   — static sites + functions
└── Cloudflare Workers — edge computing
```

```javascript
// AWS Lambda Example
exports.handler = async (event) => {
    const { name } = JSON.parse(event.body)

    // ทำอะไรก็ได้ — เรียก DB, ส่ง email, etc.
    const result = await processData(name)

    return {
        statusCode: 200,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: `Hello ${name}`, data: result })
    }
}
```

```
เมื่อไหร่ใช้ Serverless:
✅ API ที่ traffic ไม่สม่ำเสมอ (spike/idle)
✅ Event processing (S3 upload → resize image)
✅ Scheduled tasks (cron jobs)
✅ MVP / prototype

เมื่อไหร่ไม่ควร:
❌ Long-running processes (> 15 min)
❌ WebSocket connections
❌ Latency-sensitive (cold start ~100-500ms)
❌ Predictable high traffic (EC2/containers ถูกกว่า)
```
