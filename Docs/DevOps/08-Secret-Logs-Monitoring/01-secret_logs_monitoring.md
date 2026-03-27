# Secret Management, Logs Management & Infrastructure Monitoring

---

## 1. Secret Management

### ทำไมต้องจัดการ Secrets

```
Secrets = API keys, passwords, tokens, certificates

❌ ไม่ดี:
- เก็บ secrets ใน code / Git → ถูก hack!
- ใส่ใน environment variables บน server → จัดการยาก
- Copy-paste ให้กัน → ไม่ปลอดภัย

✅ ดี:
- ใช้ Secret Management tool
- Secrets เข้ารหัส, audit trail, rotate อัตโนมัติ
```

### เครื่องมือ

```
| เครื่องมือ        | ประเภท       | เด่นเรื่อง                 |
|------------------|-------------|---------------------------|
| HashiCorp Vault ⭐| Open source | ครบที่สุด, dynamic secrets  |
| AWS Secrets Mgr  | AWS         | ง่าย, rotate อัตโนมัติ      |
| Azure Key Vault  | Azure       | ดีกับ .NET / Azure         |
| Sealed Secrets   | Kubernetes  | encrypt secrets ใน Git     |
| SOPS             | Open source | encrypt files ด้วย KMS     |
| Doppler          | Cloud       | ง่ายมาก, centralized       |

# Vault ตัวอย่าง
vault kv put secret/myapp/db username=admin password=s3cret
vault kv get secret/myapp/db

# Kubernetes Secrets
kubectl create secret generic db-secret \
  --from-literal=username=admin \
  --from-literal=password=s3cret
```

---

## 2. Logs Management

### Centralized Logging

```
ปัญหา: 50 servers, log อยู่คนละเครื่อง → หา bug ไม่เจอ
คำตอบ: รวม logs ทั้งหมดไว้ที่เดียว

App Logs ──→ ┌──────────┐    ┌──────────────┐    ┌──────────┐
Sys Logs ──→ │ Collector│ →  │ Storage/Index │ →  │Dashboard │
K8s Logs ──→ │ (Fluentd)│    │ (Elasticsearch)│   │(Kibana)  │
             └──────────┘    └──────────────┘    └──────────┘
```

### เครื่องมือ

```
| Stack         | ประกอบด้วย                      | เด่นเรื่อง          |
|--------------|--------------------------------|---------------------|
| ELK ⭐       | Elasticsearch + Logstash + Kibana| full-text search    |
| PLG (Loki)   | Promtail + Loki + Grafana       | เบากว่า ELK มาก     |
| Graylog      | MongoDB + Elasticsearch + Graylog| ง่าย, alerting      |
| Splunk       | Splunk (all-in-one)             | enterprise, แพง      |
| Papertrail   | Cloud                           | ง่ายที่สุด, เล็ก     |

แนะนำ:
├── ทีมเล็ก/เริ่มต้น?     → Loki + Grafana (เบา)
├── ต้อง full-text search? → ELK Stack
└── Enterprise?            → Splunk
```

---

## 3. Infrastructure Monitoring

```
เครื่องมือ:

| เครื่องมือ       | ประเภท       | เด่นเรื่อง                    |
|-----------------|-------------|-------------------------------|
| Prometheus ⭐    | Open source | Metrics collection, alerting   |
| Grafana ⭐       | Open source | Dashboard, visualization       |
| Datadog         | Cloud       | ครบในตัว (metrics, logs, traces)|
| Zabbix          | Open source | Traditional monitoring         |
| Nagios          | Open source | เก่าแก่, enterprise            |

Prometheus + Grafana (stack ที่นิยมที่สุด):

Apps → [Prometheus] → scrape metrics ทุก 15 วินาที
                   → เก็บ time-series data
                   → alert rules
                   ↓
           [Grafana] → dashboards สวยงาม
                    → alerting (Slack, PagerDuty, email)
```

### Alert ที่สำคัญ

```yaml
# Prometheus Alert Rules
groups:
  - name: critical
    rules:
      - alert: HighCPU
        expr: node_cpu_usage > 80
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "CPU usage > 80% on {{ $labels.instance }}"

      - alert: DiskAlmostFull
        expr: node_filesystem_avail_bytes / node_filesystem_size_bytes < 0.1
        for: 10m
        labels:
          severity: critical

      - alert: ServiceDown
        expr: up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "{{ $labels.job }} is DOWN"
```
