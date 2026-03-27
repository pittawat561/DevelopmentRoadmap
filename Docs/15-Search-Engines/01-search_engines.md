# Search Engines — Elasticsearch & Solr

---

## 1. ทำไมต้องใช้ Search Engine

```
SQL LIKE ไม่เพียงพอ:
SELECT * FROM products WHERE name LIKE '%laptop%'

ปัญหา:
❌ ช้ามากกับข้อมูลจำนวนมาก (full table scan)
❌ ไม่รองรับ typo ("laptp" → ไม่เจอ)
❌ ไม่เข้าใจ synonyms ("notebook" = "laptop")
❌ ไม่มี relevance ranking
❌ ไม่รองรับ faceted search (filter by brand, price range)

Search Engine แก้ทุกปัญหา:
✅ เร็วมาก (inverted index)
✅ Fuzzy search (typo tolerance)
✅ Relevance scoring
✅ Full-text search
✅ Facets, aggregations, autocomplete
```

---

## 2. Elasticsearch

### คืออะไร

Elasticsearch คือ **distributed search & analytics engine** สร้างบน Apache Lucene

```
หลักการ: Inverted Index
ปกติ:     Document → Words
Inverted: Word → Documents

Document 1: "Redis is a fast cache"
Document 2: "Cache improves performance"
Document 3: "Redis and Memcached are caches"

Inverted Index:
| Word        | Documents   |
|-------------|-------------|
| redis       | 1, 3        |
| fast        | 1           |
| cache       | 1, 2, 3     |
| performance | 2           |
| memcached   | 3           |

ค้นหา "redis cache" → เจอ Document 1, 3 (ทันที!)
```

### CRUD Operations

```javascript
// Elasticsearch REST API

// สร้าง Index (เหมือนสร้างตาราง)
PUT /products
{
  "mappings": {
    "properties": {
      "name": { "type": "text" },
      "description": { "type": "text" },
      "price": { "type": "float" },
      "category": { "type": "keyword" },     // exact match
      "created_at": { "type": "date" }
    }
  }
}

// เพิ่มเอกสาร
POST /products/_doc
{
  "name": "MacBook Pro 16",
  "description": "Laptop for professional developers",
  "price": 79900,
  "category": "laptops",
  "created_at": "2024-01-15"
}

// ค้นหา
GET /products/_search
{
  "query": {
    "multi_match": {
      "query": "developer laptop",
      "fields": ["name", "description"],
      "fuzziness": "AUTO"            // รองรับ typo
    }
  }
}

// Filter + Sort
GET /products/_search
{
  "query": {
    "bool": {
      "must": [
        { "match": { "description": "laptop" } }
      ],
      "filter": [
        { "term": { "category": "laptops" } },
        { "range": { "price": { "gte": 30000, "lte": 100000 } } }
      ]
    }
  },
  "sort": [{ "price": "asc" }]
}

// Aggregations (สถิติ)
GET /products/_search
{
  "size": 0,
  "aggs": {
    "categories": {
      "terms": { "field": "category" }
    },
    "avg_price": {
      "avg": { "field": "price" }
    }
  }
}
```

### Node.js Client

```javascript
const { Client } = require('@elastic/elasticsearch')
const client = new Client({ node: 'http://localhost:9200' })

// ค้นหาสินค้า
async function searchProducts(query, filters = {}) {
  const { body } = await client.search({
    index: 'products',
    body: {
      query: {
        bool: {
          must: [
            { multi_match: { query, fields: ['name^2', 'description'], fuzziness: 'AUTO' } }
          ],
          filter: [
            ...(filters.category ? [{ term: { category: filters.category } }] : []),
            ...(filters.maxPrice ? [{ range: { price: { lte: filters.maxPrice } } }] : [])
          ]
        }
      },
      highlight: {
        fields: { name: {}, description: {} }  // highlight คำที่ match
      }
    }
  })

  return body.hits.hits.map(hit => ({
    ...hit._source,
    score: hit._score,
    highlights: hit.highlight
  }))
}
```

---

## 3. Solr

```
Solr คือ search platform จาก Apache (สร้างบน Lucene เหมือน Elasticsearch)

Elasticsearch vs Solr:
| หัวข้อ          | Elasticsearch      | Solr              |
|----------------|--------------------|--------------------|
| API            | REST (JSON)        | REST (XML/JSON)    |
| ความนิยม        | ⭐ สูงกว่า         | ลดลง               |
| Real-time      | ✅ Near real-time  | ✅                  |
| Cloud-native   | ✅ ออกแบบมาเลย    | เพิ่มทีหลัง (SolrCloud)|
| Learning Curve | ง่ายกว่า           | ยากกว่า             |
| Ecosystem      | ELK Stack ⭐       | ใช้คู่กับ Hadoop    |
| Schema         | Dynamic mapping    | ต้อง define ก่อน    |

แนะนำ: Elasticsearch (นิยมกว่า, เอกสารมากกว่า, community ใหญ่กว่า)
```

---

## 4. ELK Stack

```
ELK = Elasticsearch + Logstash + Kibana

Logstash: รวบรวม, แปลง, ส่ง logs
Elasticsearch: เก็บและค้นหา
Kibana: แสดงผล (dashboard, visualizations)

App Logs → [Logstash] → [Elasticsearch] → [Kibana Dashboard]

ใช้สำหรับ:
- Centralized logging (รวม logs จากทุก services)
- Log analysis & search
- Monitoring dashboard
- Alerting (แจ้งเตือนเมื่อ error เยอะ)
```
