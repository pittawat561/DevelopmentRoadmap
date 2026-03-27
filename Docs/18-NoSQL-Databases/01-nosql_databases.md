# NoSQL Databases — ฐานข้อมูลที่ไม่ใช่ Relational

---

## 1. NoSQL คืออะไร

```
NoSQL = Not Only SQL
ฐานข้อมูลที่ไม่ใช้ตาราง/แถว/คอลัมน์แบบ Relational Database

เมื่อไหร่ใช้ NoSQL:
✅ ข้อมูลไม่มีโครงสร้างตายตัว (schema-less)
✅ ต้องการ scale horizontally ได้ง่าย
✅ ข้อมูลจำนวนมหาศาล (Big Data)
✅ ต้องการ latency ต่ำมาก

เมื่อไหร่ใช้ SQL:
✅ ข้อมูลมีความสัมพันธ์ซับซ้อน
✅ ต้องการ ACID transactions
✅ ข้อมูลมีโครงสร้างชัดเจน
```

---

## 2. ประเภท NoSQL

### Document DBs — MongoDB, CouchDB

```
เก็บข้อมูลเป็น JSON-like documents
เหมาะกับ: content management, user profiles, catalogs

// MongoDB Document
{
  "_id": ObjectId("507f1f77bcf86cd799439011"),
  "name": "John Doe",
  "email": "john@test.com",
  "orders": [                          // nested array
    { "product": "Laptop", "price": 30000 },
    { "product": "Mouse", "price": 500 }
  ],
  "address": {                         // nested object
    "street": "123 Main St",
    "city": "Bangkok"
  }
}

// CRUD
// Create
db.users.insertOne({ name: "John", email: "john@test.com" })

// Read
db.users.find({ email: "john@test.com" })
db.users.find({ "address.city": "Bangkok" })
db.users.find({ "orders.price": { $gt: 1000 } })

// Update
db.users.updateOne(
  { _id: ObjectId("...") },
  { $set: { name: "Jane" }, $push: { orders: { product: "Keyboard", price: 2000 } } }
)

// Delete
db.users.deleteOne({ _id: ObjectId("...") })

// Aggregation
db.orders.aggregate([
  { $group: { _id: "$user_id", totalSpent: { $sum: "$amount" } } },
  { $sort: { totalSpent: -1 } },
  { $limit: 10 }
])
```

### Key-Value — Redis, DynamoDB

```
เก็บข้อมูลเป็นคู่ key-value ง่ายที่สุด เร็วที่สุด

Redis:
SET user:123 '{"name":"John","email":"john@test.com"}'
GET user:123

DynamoDB (AWS):
- Managed, serverless
- Auto-scaling
- Single-digit millisecond latency

เหมาะกับ: session store, cache, leaderboard, shopping cart
```

### Column DBs — Cassandra, ClickHouse, ScyllaDB

```
เก็บข้อมูลเป็น columns แทน rows — เหมาะกับ analytics

Row-based (SQL):    อ่านทีละแถว (ดีสำหรับ OLTP)
Column-based:       อ่านทีละ column (ดีสำหรับ OLAP/analytics)

ตัวอย่าง: "หาค่าเฉลี่ย price ของทุก products"
Row-based:    อ่านทุกแถว ทุก column → ช้า
Column-based: อ่านเฉพาะ column price → เร็วมาก!

Cassandra:
- Distributed, no single point of failure
- High write throughput
- ใช้โดย Netflix, Instagram, Apple

เหมาะกับ: time-series data, IoT, logs, analytics
```

### Graph DBs — Neo4j, DGraph, AWS Neptune

```
เก็บข้อมูลเป็น nodes + relationships

(John) --[FRIENDS_WITH]--> (Jane)
(John) --[PURCHASED]--> (Laptop)
(Jane) --[PURCHASED]--> (Laptop)
(Jane) --[FRIENDS_WITH]--> (Bob)

// Neo4j (Cypher Query)
// หาเพื่อนของเพื่อนที่ซื้อสินค้าเดียวกัน
MATCH (me:User {name: "John"})-[:FRIENDS_WITH]-(friend)
      -[:FRIENDS_WITH]-(fof)-[:PURCHASED]->(product)
WHERE NOT (me)-[:PURCHASED]->(product)
RETURN product.name, COUNT(*) as recommendations

เหมาะกับ: social network, recommendation, fraud detection, knowledge graph
```

### Realtime DBs — Firebase, RethinkDB

```
Firebase (Firestore):
- Realtime sync — ข้อมูลเปลี่ยน → ทุก client เห็นทันที
- Serverless — ไม่ต้องเขียน backend
- ใช้กับ mobile/web apps โดยตรง

// Firebase Client
import { doc, onSnapshot } from 'firebase/firestore'

// Real-time listener
onSnapshot(doc(db, "users", "123"), (doc) => {
  console.log("Current data:", doc.data())
  // ทำงานทุกครั้งที่ข้อมูลเปลี่ยน!
})

เหมาะกับ: mobile apps, real-time collaboration, MVP/prototype
```

### Time Series — InfluxDB, TimescaleDB

```
ออกแบบมาเพื่อข้อมูลที่มี timestamp

ตัวอย่างข้อมูล:
| timestamp           | server | cpu_usage | memory |
|---------------------|--------|-----------|--------|
| 2024-01-15 10:00:00 | web-1  | 45.2      | 72.1   |
| 2024-01-15 10:00:01 | web-1  | 46.8      | 72.3   |
| 2024-01-15 10:00:02 | web-2  | 23.1      | 54.7   |

เหมาะกับ: monitoring, IoT, financial data, metrics
```

---

## 3. SQL vs NoSQL สรุป

```
| หัวข้อ          | SQL                  | NoSQL                 |
|----------------|----------------------|-----------------------|
| Schema         | Fixed (ต้อง define)   | Flexible              |
| Scaling        | Vertical (ยาก)       | Horizontal (ง่าย)     |
| Transactions   | ACID ✅              | ส่วนใหญ่ eventual     |
| Relationships  | ⭐ ดีมาก (JOINs)    | ไม่ดี (denormalized)  |
| Query Language | SQL (มาตรฐาน)       | แต่ละตัวต่างกัน        |
| Use Case       | Banking, ERP, CRM    | Social, IoT, Big Data |

กฎง่ายๆ:
- ข้อมูลมีความสัมพันธ์? → SQL (PostgreSQL)
- ข้อมูลยืดหยุ่น + scale? → Document DB (MongoDB)
- ต้องเร็วมาก + simple? → Key-Value (Redis)
- Analytics + time-series? → Column DB / Time Series
- Social/Recommendation? → Graph DB (Neo4j)
```
